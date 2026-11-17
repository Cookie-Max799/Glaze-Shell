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
/// как её продолжение. Скругление задаёт система (DWM) с полноценным сглаживанием:
/// региональная маска рисовала бы углы ступеньками.
/// </para>
/// <para>
/// Появление и уход — движение на несколько DIP с плавным проявлением, а не мгновенная
/// смена прозрачности: backdrop создаётся один раз и прогревается заранее, поэтому
/// первый кадр уже со стеклом, без рывка.
/// </para>
/// </remarks>
internal sealed class VolumeToastWindow : Window
{
    private const int WidthDips = 250;
    private const int HeightDips = 52;

    /// <summary>Отступ от правого края рабочей области.</summary>
    private const int MarginDips = 12;

    /// <summary>Насколько плашка выезжает снизу при появлении, DIP.</summary>
    private const int SlideDips = 10;

    private const int OffscreenX = -30000;
    private const int OffscreenY = -30000;

    private const double ShowMilliseconds = 220;
    private const double HoldMilliseconds = 1100;
    private const double FadeMilliseconds = 190;

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

    private bool _closed;
    private bool _preloaded;

    /// <summary>Плашка показана: повторный вызов не должен её пересоздавать.</summary>
    private bool _visible;

    /// <summary>Конечное положение плашки; от него считается выезд снизу.</summary>
    private RectInt32 _placement;

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
            CornerRadius = new CornerRadius(8),
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
        OverlayWindow.UseRoundedCorners(_hwnd);

        // Backdrop создаётся один раз: пересоздание на каждом показе перезапускало
        // подключение Acrylic, и первый кадр появлялся плотным и с рывком.
        var backdrop = _themes.CreatePanelBackdrop(PanelSurface.TopBar) as PanelBackdrop;
        SystemBackdrop = backdrop;

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = ScreenRefresh;
        _timer.Tick += OnTick;

        Closed += (_, _) =>
        {
            _closed = true;
            _timer.Stop();
            _timer.Tick -= OnTick;
            backdrop?.Dispose();
        };
    }

    /// <summary>
    /// Прогревает окно вне экрана: XAML и Acrylic подключаются до первого показа,
    /// поэтому плашка появляется сразу со стеклом.
    /// </summary>
    public void Prewarm(double scale)
    {
        if (_closed || _preloaded)
        {
            return;
        }

        _preloaded = true;
        _root.Opacity = 0;

        var width = Math.Max(1, (int)Math.Round(WidthDips * scale));
        var height = Math.Max(1, (int)Math.Round(HeightDips * scale));
        AppWindow.MoveAndResize(new RectInt32(OffscreenX, OffscreenY, width, height));
        OverlayWindow.ShowWithoutActivation(_hwnd);
    }

    /// <summary>
    /// Показывает уровень. Если плашка уже на экране, значение обновляется на месте,
    /// а удержание продлевается: индикатор не мигает при серии нажатий.
    /// </summary>
    /// <param name="topOffsetDips">
    /// Верх плашки в DIP от края монитора: под открытой панелью верхней полосы,
    /// чтобы плашка не оказывалась за её окном.
    /// </param>
    public void ShowVolume(int percent, bool muted, MonitorInfo monitor, double topOffsetDips)
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

            _placement = ComputePlacement(monitor, topOffsetDips);

            // Окно показывается уже на конечной позиции по X и чуть ниже по Y, но
            // прозрачным, а движение начинается с первого кадра таймера: так не бывает
            // кадра, где плашка видна, но ещё стоит на месте.
            _root.Opacity = 0;
            var slide = (int)Math.Round(SlideDips * monitor.ScaleFactor);
            AppWindow.MoveAndResize(new RectInt32(_placement.X, _placement.Y + slide, _placement.Width, _placement.Height));
            _visible = true;
            _watch.Restart();
            OverlayWindow.ShowWithoutActivation(_hwnd);
        }

        if (_themes.ReduceMotion)
        {
            _bar.Value = _targetPercent;
            _displayedPercent = _targetPercent;
            _root.Opacity = 1;
            PlaceAt(0);
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
        _placement = new RectInt32(_placement.X, _placement.Y + deltaPixels, _placement.Width, _placement.Height);
    }

    private void ApplyBrushes()
    {
        _root.Background = (Brush)Application.Current.Resources["GlazeTopBarGlassBrush"];
        _root.BorderBrush = (Brush)Application.Current.Resources["GlazeBorderBrush"];
        _icon.Foreground = (Brush)Application.Current.Resources["GlazeGlassTextPrimaryBrush"];
        _value.Foreground = (Brush)Application.Current.Resources["GlazeGlassTextPrimaryBrush"];
        _themes.ApplyFromSettings(_root, _root);
    }

    private static RectInt32 ComputePlacement(MonitorInfo monitor, double topOffsetDips)
    {
        var scale = monitor.ScaleFactor;
        var area = monitor.Bounds;
        var right = area.X + area.Width - (int)Math.Round(MarginDips * scale);
        var top = area.Y + (int)Math.Round(Math.Max(0, topOffsetDips) * scale);
        var width = (int)Math.Round(WidthDips * scale);
        var height = (int)Math.Round(HeightDips * scale);

        return new RectInt32(right - width, top, width, height);
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

        if (_themes.ReduceMotion)
        {
            _root.Opacity = 1;
            PlaceAt(0);
            return;
        }

        var remaining = _holdUntilTick - Environment.TickCount64;

        if (remaining <= 0)
        {
            // Уход: плашка гаснет и одновременно опускается, повторяя появление.
            var fadeProgress = Math.Clamp(-remaining / FadeMilliseconds, 0, 1);
            var easedFade = fadeProgress * fadeProgress;
            _root.Opacity = 1 - easedFade;
            PlaceAt(easedFade);

            if (fadeProgress >= 1)
            {
                Hide();
            }

            return;
        }

        // Появление: плашка поднимается снизу и проявляется за доли секунды.
        var showProgress = Math.Clamp(_watch.Elapsed.TotalMilliseconds / ShowMilliseconds, 0, 1);
        var easedShow = 1 - Math.Pow(1 - showProgress, 3);
        _root.Opacity = easedShow;
        PlaceAt(1 - easedShow);
    }

    /// <summary>
    /// Ставит окно ниже конечного положения: <paramref name="down"/> = 0 — плашка на месте,
    /// 1 — опущена на <see cref="SlideDips"/>. Размер окна не меняется, поэтому маска
    /// и раскладка не пересчитываются на каждом кадре.
    /// </summary>
    private void PlaceAt(double down)
    {
        var scale = OverlayWindow.GetScaleFactor(_hwnd);
        var offset = (int)Math.Round(SlideDips * scale * Math.Clamp(down, 0, 1));
        var targetY = _placement.Y + offset;
        var position = AppWindow.Position;

        if (position.X != _placement.X || position.Y != targetY)
        {
            AppWindow.Move(new PointInt32(_placement.X, targetY));
        }
    }

    /// <summary>Прячет плашку, сохраняя окно для следующего показа.</summary>
    private void Hide()
    {
        _timer.Stop();
        _visible = false;
        _root.Opacity = 0;
        AppWindow.Hide();
    }
}
