using System.Diagnostics;
using GlazeShell.App.Presentation;
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
/// Плашка заряда устройств: показывается под верхней панелью при наведении на значок
/// устройства или на свернувшуюся строку устройств. В первом случае это одна строка
/// с зарядом конкретного устройства, во втором — список всех подключённых устройств.
/// </summary>
/// <remarks>
/// <para>
/// Окно не активируется: наведение не должно забирать фокус у активного приложения.
/// Фон — тот же Acrylic, что у верхней панели, поэтому плашка читается как её
/// продолжение. Скругление задаёт система (DWM) с полноценным сглаживанием.
/// </para>
/// <para>
/// Появление и уход — движение на несколько DIP с плавным проявлением, как у плашки
/// громкости. Пока курсор остаётся на значке, показ продлевается каждым кадром, поэтому
/// плашка не угасает под ним и уходит только после того, как курсор уходит.
/// </para>
/// </remarks>
internal sealed class DeviceToastWindow : Window
{
    private const int SingleWidthDips = 232;
    private const int SingleHeightDips = 52;
    private const int ListWidthDips = 272;
    private const int RowHeightDips = 32;
    private const int ListPaddingDips = 6;

    /// <summary>Отступ от края монитора, чтобы плашка не прилипала к краю.</summary>
    private const int MarginDips = 12;

    /// <summary>Насколько плашка выезжает снизу при появлении, DIP.</summary>
    private const int SlideDips = 10;

    private const int OffscreenX = -30000;
    private const int OffscreenY = -30000;

    private const double ShowMilliseconds = 220;
    private const double HoldMilliseconds = 1100;
    private const double FadeMilliseconds = 190;

    private static readonly TimeSpan ScreenRefresh = TimeSpan.FromMilliseconds(16);

    private readonly Border _root;
    private readonly Grid _single;
    private readonly FontIcon _icon;
    private readonly TextBlock _name;
    private readonly TextBlock _battery;
    private readonly StackPanel _list;
    private readonly ThemeApplicator _themes;
    private readonly DispatcherQueueTimer _timer;
    private readonly Stopwatch _watch = new();
    private readonly nint _hwnd;

    private bool _closed;
    private bool _preloaded;
    private bool _visible;

    /// <summary>Конечное положение плашки; от него считается выезд снизу.</summary>
    private RectInt32 _placement;

    /// <summary>Момент, до которого плашка держится на экране.</summary>
    private long _holdUntilTick;

    /// <summary>
    /// Подпись последнего списка устройств. Список перестраивается только когда состав
    /// или заряд изменились: наведение обновляет показ каждым кадром, и без этой подписи
    /// строки пересоздавались бы 60 раз в секунду.
    /// </summary>
    private string? _listSignature;

    public DeviceToastWindow(ThemeApplicator themes)
    {
        _themes = themes ?? throw new ArgumentNullException(nameof(themes));

        _icon = new FontIcon
        {
            FontSize = 15,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _name = new TextBlock
        {
            FontSize = 12.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _battery = new TextBlock
        {
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _single = new Grid { ColumnSpacing = 10 };
        _single.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        _single.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _single.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_icon, 0);
        Grid.SetColumn(_name, 1);
        Grid.SetColumn(_battery, 2);
        _single.Children.Add(_icon);
        _single.Children.Add(_name);
        _single.Children.Add(_battery);

        _list = new StackPanel { Spacing = 6 };

        _root = new Border
        {
            Padding = new Thickness(14, 8, 14, 8),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Child = _single,
        };

        Content = _root;
        AppWindow.IsShownInSwitchers = false;
        Title = "Glaze Shell — Заряд устройств";

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

        var width = Math.Max(1, (int)Math.Round(ListWidthDips * scale));
        var height = Math.Max(1, (int)Math.Round(SingleHeightDips * scale));
        AppWindow.MoveAndResize(new RectInt32(OffscreenX, OffscreenY, width, height));
        OverlayWindow.ShowWithoutActivation(_hwnd);
    }

    /// <summary>
    /// Показывает заряд одного устройства под его значком в верхней панели.
    /// </summary>
    /// <param name="iconCenterX">Центр значка устройства в физических пикселях экрана.</param>
    /// <param name="topOffsetDips">
    /// Верх плашки в DIP от края монитора: под открытой панелью верхней полосы.
    /// </param>
    public void ShowDevice(DeviceRowViewModel device, MonitorInfo monitor, int iconCenterX, double topOffsetDips)
    {
        _listSignature = null;
        _icon.Glyph = device.Glyph;
        _name.Text = device.Name;
        _battery.Text = device.BatteryText;
        _root.Child = _single;
        Show(SingleWidthDips, SingleHeightDips, monitor, iconCenterX, topOffsetDips);
    }

    /// <summary>
    /// Показывает список всех подключённых устройств с зарядом под свернувшейся
    /// строкой-значком.
    /// </summary>
    public void ShowDevices(IReadOnlyList<DeviceRowViewModel> devices, MonitorInfo monitor, int iconCenterX, double topOffsetDips)
    {
        var signature = string.Join("|", devices.Select(device => $"{device.Id}:{device.Battery}"));
        if (_root.Child != _list || signature != _listSignature)
        {
            _listSignature = signature;
            _list.Children.Clear();
            foreach (var device in devices)
            {
                var icon = new FontIcon
                {
                    Glyph = device.Glyph,
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                var name = new TextBlock
                {
                    Text = device.Name,
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                var battery = new TextBlock
                {
                    Text = device.BatteryText,
                    FontSize = 12.5,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                var row = new Grid { ColumnSpacing = 10 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(icon, 0);
                Grid.SetColumn(name, 1);
                Grid.SetColumn(battery, 2);
                row.Children.Add(icon);
                row.Children.Add(name);
                row.Children.Add(battery);
                _list.Children.Add(row);
            }

            _root.Child = _list;
        }

        Show(ListWidthDips, ListHeightDips(devices.Count), monitor, iconCenterX, topOffsetDips);
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

    private void Show(int widthDips, int heightDips, MonitorInfo monitor, int iconCenterX, double topOffsetDips)
    {
        if (_closed)
        {
            return;
        }

        _holdUntilTick = Environment.TickCount64 + (long)HoldMilliseconds;

        if (!_visible)
        {
            ApplyBrushes();
            _placement = ComputePlacement(widthDips, heightDips, monitor, iconCenterX, topOffsetDips);

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
        else
        {
            // Смена размера (одна строка ↔ список) — окно переносится на новое место.
            var target = ComputePlacement(widthDips, heightDips, monitor, iconCenterX, topOffsetDips);
            if (target.Width != _placement.Width || target.Height != _placement.Height)
            {
                _placement = target;
                AppWindow.MoveAndResize(_placement);
            }
            else if (target.X != _placement.X || target.Y != _placement.Y)
            {
                _placement = target;
                AppWindow.Move(new PointInt32(_placement.X, _placement.Y));
            }
        }

        if (_themes.ReduceMotion)
        {
            _root.Opacity = 1;
            PlaceAt(0);
        }

        _timer.Start();
    }

    private void ApplyBrushes()
    {
        _root.Background = (Brush)Application.Current.Resources["GlazeTopBarGlassBrush"];
        _root.BorderBrush = (Brush)Application.Current.Resources["GlazeBorderBrush"];
        _icon.Foreground = (Brush)Application.Current.Resources["GlazeGlassTextPrimaryBrush"];
        _name.Foreground = (Brush)Application.Current.Resources["GlazeGlassTextPrimaryBrush"];
        _battery.Foreground = (Brush)Application.Current.Resources["GlazeGlassTextPrimaryBrush"];
        _themes.ApplyFromSettings(_root, _root);
    }

    private static RectInt32 ComputePlacement(int widthDips, int heightDips, MonitorInfo monitor, int iconCenterX, double topOffsetDips)
    {
        var scale = monitor.ScaleFactor;
        var area = monitor.Bounds;

        var width = (int)Math.Round(widthDips * scale);
        var height = (int)Math.Round(heightDips * scale);
        var margin = (int)Math.Round(MarginDips * scale);

        // Плашка центрируется под значком, но не выходит за края монитора.
        var x = Math.Clamp(iconCenterX - (width / 2), area.X + margin, area.X + area.Width - width - margin);
        var y = area.Y + (int)Math.Round(Math.Max(0, topOffsetDips) * scale);

        return new RectInt32(x, y, width, height);
    }

    /// <summary>Высота списка устройств: строки по 32 DIP с отступом 6 между ними.</summary>
    private static int ListHeightDips(int rows)
    {
        var count = Math.Max(1, rows);
        return ListPaddingDips + (count * RowHeightDips) + ((count - 1) * 6) + ListPaddingDips;
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        if (_closed)
        {
            _timer.Stop();
            return;
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
