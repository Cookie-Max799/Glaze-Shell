using System.Diagnostics;
using GlazeShell.App.Theming;
using GlazeShell.Core.Display;
using MonitorInfo = GlazeShell.Core.Models.MonitorInfo;
using GlazeShell.Windows.WindowManagement;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace GlazeShell.App;

/// <summary>
/// Всплывающий индикатор громкости: плашка под верхней панелью со значком динамика,
/// ползунком уровня и числом. Показывается при изменении громкости — так же, как
/// Windows показывает свой индикатор, — и живёт короткое время.
/// </summary>
/// <remarks>
/// <para>
/// Повторное изменение уровня не перезапускает показ: плашка остаётся на месте, ползунок
/// плавно перетекает к новому значению, а время удержания продлевается. Поэтому быстрые
/// нажатия клавиш громкости не вызывают мигания — индикатор «тянется» за уровнем.
/// </para>
/// <para>
/// Окно не активируется: показ индикатора не должен забирать фокус у активного
/// приложения. Фон — тот же Acrylic, что у верхней панели, поэтому плашка читается
/// как её продолжение. Регион окна скругляется по маске: <c>CornerRadius</c> в XAML
/// красит только фон, а видимую форму задаёт регион.
/// </para>
/// </remarks>
internal sealed class VolumeToastWindow : Window
{
    private const int WidthDips = 250;
    private const int HeightDips = 52;

    /// <summary>Радиус скругления плашки в DIP.</summary>
    private const int CornerRadiusDips = 14;

    /// <summary>Отступ от правого края рабочей области.</summary>
    private const int MarginDips = 12;

    /// <summary>Отступ сверху: верхняя панель занимает 30 DIP, плашка идёт сразу под ней.</summary>
    private const int TopDips = 36;

    private const double ShowMilliseconds = 120;
    private const double HoldMilliseconds = 1100;
    private const double FadeMilliseconds = 180;

    /// <summary>Скорость, с которой ползунок догоняет новое значение (доля за кадр).</summary>
    private const double BarEasing = 0.35;

    private static readonly TimeSpan ScreenRefresh = TimeSpan.FromMilliseconds(16);

    private readonly Border _root;
    private readonly FontIcon _icon;
    private readonly ProgressBar _bar;
    private readonly TextBlock _value;
    private readonly ThemeApplicator _themes;
    private readonly DispatcherQueueTimer _timer;
    private readonly Stopwatch _watch = new();
    private readonly nint _hwnd;

    private PanelBackdrop? _backdrop;
    private bool _closed;

    /// <summary>Плашка показана: повторный вызов не должен её пересоздавать.</summary>
    private bool _visible;

    private double _targetPercent;
    private double _displayedPercent;

    /// <summary>Момент, до которого плашка держится на экране.</summary>
    private long _holdUntilTick;

    public VolumeToastWindow(ThemeApplicator themes)
    {
        _themes = themes ?? throw new ArgumentNullException(nameof(themes));

        _icon = new FontIcon
        {
            FontSize = 15,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 4,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _value = new TextBlock
        {
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 30,
        };

        var content = new Grid
        {
            ColumnSpacing = 12,
        };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_icon, 0);
        Grid.SetColumn(_bar, 1);
        Grid.SetColumn(_value, 2);
        content.Children.Add(_icon);
        content.Children.Add(_bar);
        content.Children.Add(_value);

        _root = new Border
        {
            Width = WidthDips,
            Height = HeightDips,
            Padding = new Thickness(14, 8, 14, 8),
            CornerRadius = new CornerRadius(CornerRadiusDips),
            BorderThickness = new Thickness(1),
            Child = content,
        };

        Content = _root;
        AppWindow.IsShownInSwitchers = false;
        Title = "Glaze Shell — Громкость";

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        OverlayWindow.MakeTopMostToolWindow(_hwnd);
        OverlayWindow.SetNonActivating(_hwnd, true);

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = ScreenRefresh;
        _timer.Tick += OnTick;

        Closed += (_, _) =>
        {
            _closed = true;
            _timer.Stop();
            _timer.Tick -= OnTick;
            _backdrop?.Dispose();
            _backdrop = null;
        };
    }

    /// <summary>
    /// Показывает уровень. Если плашка уже на экране, значение обновляется на месте,
    /// а удержание продлевается: индикатор не мигает при серии нажатий.
    /// </summary>
    public void ShowVolume(int percent, bool muted, MonitorInfo monitor, bool topBarVisible = true)
    {
        if (_closed)
        {
            return;
        }

        _targetPercent = muted ? 0 : Math.Clamp(percent, 0, 100);

        var value = _targetPercent.ToString(System.Globalization.CultureInfo.CurrentCulture);
        if (!string.Equals(_value.Text, value, StringComparison.Ordinal))
        {
            _value.Text = value;
        }

        var glyph = muted ? "\uE74F" : "\uE767";
        if (!string.Equals(_icon.Glyph, glyph, StringComparison.Ordinal))
        {
            _icon.Glyph = glyph;
        }

        _holdUntilTick = Environment.TickCount64 + (long)HoldMilliseconds;

        if (!_visible)
        {
            _displayedPercent = _targetPercent;
            _bar.Value = _displayedPercent;
            ApplyBrushes();
            ApplyBackdrop();
            PlaceOver(monitor, topBarVisible);

            // MoveAndResize применяется асинхронно: маска, посчитанная до фактического
            // изменения размера, могла бы остаться от прежнего прямоугольника.
            _ = DispatcherQueue.TryEnqueue(ApplyRegion);

            _visible = true;
            _watch.Restart();
            _root.Opacity = _themes.ReduceMotion ? 1 : 0;
            OverlayWindow.ShowWithoutActivation(_hwnd);
        }

        if (_themes.ReduceMotion)
        {
            _bar.Value = _targetPercent;
            _displayedPercent = _targetPercent;
            _root.Opacity = 1;
        }

        _timer.Start();
    }

    /// <summary>
    /// Полоса сдвинулась по вертикали: плашка едет вместе с ней тем же шагом, поэтому
    /// при автоскрытии не «висит» в воздухе отдельно от панели.
    /// </summary>
    public void ShiftBy(int deltaPixels)
    {
        if (_closed || !_visible || deltaPixels == 0)
        {
            return;
        }

        var position = AppWindow.Position;
        AppWindow.Move(new PointInt32(position.X, position.Y + deltaPixels));
    }

    private void ApplyBrushes()
    {
        _root.Background = (Brush)Application.Current.Resources["GlazeTopBarGlassBrush"];
        _root.BorderBrush = (Brush)Application.Current.Resources["GlazeBorderBrush"];
        _icon.Foreground = (Brush)Application.Current.Resources["GlazeGlassTextPrimaryBrush"];
        _value.Foreground = (Brush)Application.Current.Resources["GlazeGlassTextPrimaryBrush"];
        _themes.ApplyFromSettings(_root, _root);
    }

    private void PlaceOver(MonitorInfo monitor, bool topBarVisible)
    {
        var scale = monitor.ScaleFactor;
        var area = monitor.Bounds;
        var right = area.X + area.Width - (int)Math.Round(MarginDips * scale);
        var topOffset = topBarVisible ? TopDips : 8;
        var top = area.Y + (int)Math.Round(topOffset * scale);
        var width = (int)Math.Round(WidthDips * scale);
        var height = (int)Math.Round(HeightDips * scale);

        AppWindow.MoveAndResize(new RectInt32(right - width, top, width, height));
        ApplyRegion();
    }

    /// <summary>
    /// Скругляет регион окна: <c>CornerRadius</c> в XAML задаёт только фон, а видимую
    /// форму окна определяет региональная маска.
    /// </summary>
    private void ApplyRegion()
    {
        if (_hwnd == 0)
        {
            return;
        }

        var scale = OverlayWindow.GetScaleFactor(_hwnd);

        if (!OverlayWindow.TryGetClientSize(_hwnd, out var width, out var height) || width <= 0 || height <= 0)
        {
            return;
        }

        OverlayWindow.ApplyRoundedRegions(
            _hwnd,
            [new PixelRect(0, 0, width, height)],
            (int)Math.Round(CornerRadiusDips * scale));
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        if (_closed)
        {
            _timer.Stop();
            return;
        }

        // Ползунок догоняет новое значение: при быстрой серии нажатий плашка не прыгает,
        // а плавно перетекает к каждому уровню.
        if (Math.Abs(_targetPercent - _displayedPercent) > 0.05)
        {
            _displayedPercent += (_targetPercent - _displayedPercent) * BarEasing;
            if (Math.Abs(_targetPercent - _displayedPercent) < 0.05)
            {
                _displayedPercent = _targetPercent;
            }

            _bar.Value = _displayedPercent;
        }
        else
        {
            _bar.Value = _displayedPercent;
        }

        var elapsed = _watch.Elapsed;
        var remaining = _holdUntilTick - Environment.TickCount64;

        if (remaining <= 0)
        {
            var fadeProgress = Math.Clamp(-remaining / FadeMilliseconds, 0, 1);
            _root.Opacity = 1 - fadeProgress;

            if (fadeProgress >= 1)
            {
                Hide();
            }

            return;
        }

        if (_themes.ReduceMotion)
        {
            _root.Opacity = 1;
            return;
        }

        // Появление: короткое проявление в начале, дальше плашка полностью видна до
        // истечения удержания.
        var fadeIn = Math.Clamp(elapsed.TotalMilliseconds / ShowMilliseconds, 0, 1);
        _root.Opacity = fadeIn;
    }

    /// <summary>Прячет плашку, сохраняя окно для следующего показа.</summary>
    private void Hide()
    {
        _timer.Stop();
        _visible = false;
        _root.Opacity = 0;
        AppWindow.Hide();
    }

    /// <summary>Тот же Acrylic-фон, что у верхней панели: плашка следует её прозрачности.</summary>
    private void ApplyBackdrop()
    {
        if (_closed)
        {
            return;
        }

        var previous = _backdrop;
        SystemBackdrop = null;
        previous?.Dispose();
        _backdrop = _themes.CreatePanelBackdrop(PanelSurface.TopBar) as PanelBackdrop;
        SystemBackdrop = _backdrop;
    }
}
