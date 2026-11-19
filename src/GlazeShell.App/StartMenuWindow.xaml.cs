using System.Diagnostics;
using GlazeShell.App.Presentation;
using GlazeShell.App.Theming;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Windows.DisplayManagement;
using GlazeShell.Windows.WindowManagement;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.System;

namespace GlazeShell.App;

/// <summary>
/// Меню «Пуск» Glaze Shell: панель у левого края экрана с поиском и сеткой приложений.
/// </summary>
/// <remarks>
/// Открывается одиночным нажатием клавиши Windows, пока заменена панель задач. Панель
/// выезжает из-за левого края вместе с окном и примыкает к краю без отступа, поэтому
/// выглядит продолжением экрана, а не отдельным окном. Повторное нажатие Windows,
/// <c>Esc</c> и потеря фокуса скрывают её.
/// </remarks>
public sealed partial class StartMenuWindow : Window
{
    private const int WidthDips = 460;
    private const int TileIconDips = 32;

    /// <summary>
    /// Высота меню: не на весь экран, а прямоугольник по центру левого края. Небольшие
    /// экраны ужимают его, чтобы сверху и снизу остались одинаковые отступы.
    /// </summary>
    private const int HeightDips = 600;

    /// <summary>Минимальный отступ сверху и снизу от рабочей области.</summary>
    private const int VerticalMarginDips = 24;

    private const double SlideInMilliseconds = 420;
    private const double SlideOutMilliseconds = 200;

    /// <summary>На сколько DIP меню выезжает из-за левого края при появлении.</summary>
    private const int SlideInDips = 88;

    /// <summary>На сколько DIP меню уезжает при скрытии.</summary>
    private const int SlideOutDips = 48;

    /// <summary>На сколько DIP содержимое поднимается, пока меню выезжает.</summary>
    private const int ContentRiseDips = 18;

    /// <summary>Начальный масштаб содержимого: меню «вырастает» из чуть уменьшенного состояния.</summary>
    private const float ContentStartScale = 0.96f;

    private readonly ThemeApplicator _themes;
    private readonly IMonitorManager _monitors;
    private readonly ISettingsManager _settings;
    private readonly IDisposable _settingsChanged;
    private readonly List<StartMenuItemViewModel> _items = [];
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _animation;
    private readonly Stopwatch _watch = new();

    private nint _hwnd;
    private RectInt32 _shownBounds;
    private RectInt32 _animationFrom;
    private RectInt32 _animationTo;
    private bool _slidingIn;
    private bool _closing;
    private bool _closed;
    private bool _animating;
    private bool _pointerDown;
    private PanelBackdrop? _backdrop;

    /// <summary>Нужен повторный проход по подписям: сетку пересобрали.</summary>
    private bool _needsTextRefresh;

    public StartMenuWindow(
        ThemeApplicator themes,
        IMonitorManager monitors,
        ISettingsManager settings,
        IEventManager events)
    {
        _themes = themes ?? throw new ArgumentNullException(nameof(themes));
        _monitors = monitors ?? throw new ArgumentNullException(nameof(monitors));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        InitializeComponent();
        AppWindow.IsShownInSwitchers = false;
        Title = "Glaze Shell — Пуск";

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        // Схема подписей читается по фону под панелью, а он известен только после показа
        // и раскладки: до этого ActualWidth ещё нулевой.
        Root.Loaded += (_, _) => _themes.ApplyFromSettings(Root, Root, PanelSurface.StartMenu);
        ApplyBackdrop();

        _settingsChanged = events.Subscribe<SettingsChanged>(_ =>
        {
            _themes.ApplyFromSettings(Root, Root, PanelSurface.StartMenu);
            ApplyBackdrop();
        });

        _animation = DispatcherQueue.CreateTimer();
        _animation.Interval = TimeSpan.FromMilliseconds(16);
        _animation.Tick += OnAnimationTick;

        // Плитки приложений создаются шаблоном во время раскладки, то есть уже после
        // применения темы. Их подписи берут общие кисти панели, а те к этому моменту
        // могло перекрасить соседнее окно под свой фон. Проход по готовой сетке
        // возвращает подписям схему меню.
        AppsGrid.ContainerContentChanging += (_, args) =>
        {
            if (!args.InRecycleQueue)
            {
                _needsTextRefresh = true;
            }
        };
        AppsGrid.LayoutUpdated += (_, _) =>
        {
            if (!_needsTextRefresh)
            {
                return;
            }

            _needsTextRefresh = false;
            ThemeApplicator.RefreshLocalText(Root);
        };

        Activated += OnActivated;
        Closed += (_, _) =>
        {
            _closed = true;
            _animation.Stop();
            _animation.Tick -= OnAnimationTick;
            _settingsChanged.Dispose();
            _backdrop?.Dispose();
            _backdrop = null;
        };
    }

    /// <summary>Элементы сетки: отфильтрованный список приложений.</summary>
    public IList<StartMenuItemViewModel> Items => AppsGrid.Items.OfType<StartMenuItemViewModel>().ToList();

    /// <summary>Открыта ли панель сейчас (учитывая анимацию закрытия).</summary>
    public bool IsShown => !_closed && AppWindow.IsVisible;

    /// <summary>Играется анимация ухода: повторный жест должен дождаться её конца.</summary>
    public bool IsClosing => _closing;

    internal bool OwnsWindow(nint hwnd) => !_closed && hwnd != 0 && hwnd == _hwnd;

    internal int IconRequestPixels => Math.Clamp(
        (int)Math.Ceiling(TileIconDips * (_monitors.GetPrimaryMonitor()?.ScaleFactor ?? 1) * 2), 96, 256);

    /// <summary>Заполняет список приложений и сразу применяет текущий поиск.</summary>
    public void SetItems(IReadOnlyList<StartMenuItemViewModel> items)
    {
        _items.Clear();
        _items.AddRange(items);
        ApplyFilter(SearchBox.Text);
    }

    /// <summary>Открывает панель: выезд из-за левого края с примыканием к экрану.</summary>
    public void ShowNearTaskbar()
    {
        if (_closed || (AppWindow.IsVisible && !_closing))
        {
            return;
        }

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var useDwmCorners = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);
        OverlayWindow.MakeTopMostToolWindow(_hwnd, useRedirectionBitmap: useDwmCorners);
        var monitor = _monitors.GetPrimaryMonitor();

        if (monitor is null)
        {
            return;
        }

        var scale = monitor.ScaleFactor;
        var area = monitor.WorkingArea ?? monitor.Bounds;
        var margin = (int)Math.Round(VerticalMarginDips * scale);
        var width = Math.Max(1, Math.Min((int)Math.Round(WidthDips * scale), area.Width - (margin * 2)));
        var height = Math.Max(1, Math.Min((int)Math.Round(HeightDips * scale), area.Height - (margin * 2)));
        var y = area.Y + ((area.Height - height) / 2);

        _shownBounds = new RectInt32(area.X + margin, y, width, height);
        var slideIn = (int)Math.Round(SlideInDips * scale);
        _animationFrom = new RectInt32(_shownBounds.X - slideIn, y, width, height);
        _animationTo = _shownBounds;
        _slidingIn = true;
        _closing = false;
        _animation.Stop();

        AppWindow.MoveAndResize(_animationFrom);
        if (useDwmCorners)
        {
            OverlayWindow.UseRoundedCorners(_hwnd);
        }
        else
        {
            OverlayWindow.ApplyRoundedRegions(_hwnd, [new PixelRect(0, 0, width, height)], (int)Math.Round(8 * scale));
        }
        _pointerDown = OverlayWindow.IsLeftButtonDown();
        _animating = true;
        _animation.Interval = TimeSpan.FromMilliseconds(16);

        SearchBox.Text = string.Empty;
        ApplyFilter(string.Empty);

        // Трёхслойное появление: полоса выезжает из-за края, стекло проявляется,
        // содержимое одновременно поднимается, проявляется и «вырастает» из чуть
        // уменьшенного масштаба. Меню собирается на месте с ощущением глубины.
        Root.Opacity = _themes.ReduceMotion ? 1 : 0;
        PanelContent.Opacity = _themes.ReduceMotion ? 1 : 0;
        PanelContent.Translation = _themes.ReduceMotion
            ? System.Numerics.Vector3.Zero
            : new System.Numerics.Vector3(0, ContentRiseDips, 0);
        PanelContent.Scale = _themes.ReduceMotion
            ? new System.Numerics.Vector3(1, 1, 1)
            : new System.Numerics.Vector3(ContentStartScale, ContentStartScale, 1);

        AppWindow.Show();
        Activate();
        if (!OverlayWindow.TryGetForegroundWindow(out var foreground, out _) || foreground != _hwnd)
        {
            _ = OverlayWindow.ActivateForInput(_hwnd);
        }

        SearchBox.Focus(FocusState.Programmatic);

        if (_themes.ReduceMotion)
        {
            AppWindow.MoveAndResize(_shownBounds);
            _animating = false;
            _animation.Interval = TimeSpan.FromMilliseconds(50);
            _animation.Start();
            return;
        }

        _watch.Restart();
        _animation.Start();
    }

    /// <summary>
    /// Переключает видимость: повторное нажатие клавиши Windows закрывает меню так же,
    /// как открыло.
    /// </summary>
    public void Toggle()
    {
        if (IsShown && !_closing)
        {
            HideMenu();
        }
        else
        {
            ShowNearTaskbar();
        }
    }

    /// <summary>Скрывает панель (уезжает за левый край).</summary>
    public void HideMenu()
    {
        if (_closed || !AppWindow.IsVisible || _closing)
        {
            return;
        }

        if (_themes.ReduceMotion)
        {
            _animating = false;
            _closing = false;
            AppWindow.Hide();
            _animation.Stop();
            Hidden?.Invoke(this, EventArgs.Empty);
            return;
        }

        var scale = OverlayWindow.GetScaleFactor(_hwnd);
        _animationTo = new RectInt32(
            _shownBounds.X - (int)Math.Round(SlideOutDips * scale),
            _shownBounds.Y,
            _shownBounds.Width,
            _shownBounds.Height);
        var position = AppWindow.Position;
        _animationFrom = new RectInt32(position.X, position.Y, _shownBounds.Width, _shownBounds.Height);
        _animating = true;
        _animation.Interval = TimeSpan.FromMilliseconds(16);
        _slidingIn = false;
        _closing = true;
        _watch.Restart();
        _animation.Start();
    }

    /// <summary>
    /// Мягкое замедление с заметным перелётом (ease-out back): панель проскакивает
    /// конечную точку на несколько DIP и «прищёлкивается» назад — движение читается
    /// упругим, а не линейным.
    /// </summary>
    private static double EaseOutBack(double progress)
    {
        const double c1 = 1.7;
        const double c3 = c1 + 1;
        var p = progress - 1;
        return 1 + (c3 * p * p * p) + (c1 * p * p);
    }

    private void OnAnimationTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        var down = OverlayWindow.IsLeftButtonDown();
        if (down && !_pointerDown && OverlayWindow.TryGetCursorPosition(_hwnd, out var px, out var py) &&
            (px < 0 || py < 0 || px >= _shownBounds.Width || py >= _shownBounds.Height))
        {
            HideMenu();
        }
        _pointerDown = down;
        if (!_animating)
        {
            return;
        }
        var length = _slidingIn ? SlideInMilliseconds : SlideOutMilliseconds;
        var progress = Math.Clamp(_watch.Elapsed.TotalMilliseconds / length, 0, 1);

        // Выезд с мягким «дотормаживанием» и лёгким перелётом у края, уход — с ускорением:
        // появление читается живым, закрытие остаётся быстрым.
        var eased = _slidingIn
            ? EaseOutBack(progress)
            : progress * progress;

        var x = (int)Math.Round(_animationFrom.X + ((_animationTo.X - _animationFrom.X) * eased));

        AppWindow.MoveAndResize(new RectInt32(x, _animationFrom.Y, _animationFrom.Width, _animationFrom.Height));

        if (_themes.ReduceMotion)
        {
            Root.Opacity = 1;
            PanelContent.Opacity = 1;
        }
        else if (_slidingIn)
        {
            // Стекло проявляется за первые 55 % пути; содержимое — с задержкой, подъемом
            // и ростом масштаба до единицы: меню сначала «въезжает» поверхностью,
            // затем на ней собираются элементы.
            Root.Opacity = Math.Clamp(progress / 0.55, 0, 1);
            PanelContent.Opacity = Math.Clamp((progress - 0.12) / 0.5, 0, 1);
            PanelContent.Translation = new System.Numerics.Vector3(
                0,
                (float)((1 - eased) * ContentRiseDips),
                0);
            PanelContent.Scale = new System.Numerics.Vector3(
                (float)(ContentStartScale + ((1 - ContentStartScale) * Math.Clamp(progress / 0.7, 0, 1))),
                (float)(ContentStartScale + ((1 - ContentStartScale) * Math.Clamp(progress / 0.7, 0, 1))),
                1);
        }
        else
        {
            Root.Opacity = 1 - Math.Clamp((progress - 0.2) / 0.8, 0, 1);
            PanelContent.Opacity = Root.Opacity;
        }

        if (progress < 1)
        {
            return;
        }

        _animating = false;

        if (_slidingIn)
        {
            AppWindow.MoveAndResize(_shownBounds);
            PanelContent.Translation = System.Numerics.Vector3.Zero;
            PanelContent.Scale = new System.Numerics.Vector3(1, 1, 1);
            PanelContent.Opacity = 1;
            Root.Opacity = 1;
            _animation.Interval = TimeSpan.FromMilliseconds(50);
        }
        else
        {
            _closing = false;
            AppWindow.Hide();
            _animation.Stop();
            Hidden?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (OverlayWindow.TryGetForegroundWindow(out var hwnd, out var processId) &&
                (OwnsWindow(hwnd) || (Application.Current as App)?.IsReplacedSystemMenu(processId) == true))
            {
                return;
            }

            HideMenu();
        });
    }

    private void ApplyFilter(string? query)
    {
        var trimmed = query?.Trim();

        AppsGrid.Items.Clear();

        foreach (var item in _items)
        {
            if (string.IsNullOrEmpty(trimmed)
                || item.Name.Contains(trimmed, StringComparison.CurrentCultureIgnoreCase)
                || item.ApplicationId.Contains(trimmed, StringComparison.CurrentCultureIgnoreCase))
            {
                AppsGrid.Items.Add(item);
            }
        }

        // Сетка пересобрана: контейнеры создаются заново и получают общие кисти,
        // поэтому подписи нужно перекрасить ещё раз после раскладки.
        _needsTextRefresh = true;

        EmptyText.Visibility = AppsGrid.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter(SearchBox.Text);
        SearchFilterRequested?.Invoke(this, SearchBox.Text);
    }

    private void OnAppItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is StartMenuItemViewModel item)
        {
            ItemActivated?.Invoke(this, item);
            HideMenu();
        }
    }

    /// <summary>
    /// Правая кнопка по плитке приложения: открыть, показать файл программы в проводнике
    /// или закрепить приложение в dock. Меню не скрывает «Пуск» — пользователь может
    /// выбрать ещё одно действие.
    /// </summary>
    private void OnAppItemRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source ||
            VisualTreeSearch.FindDataContext<StartMenuItemViewModel>(source) is not { } item)
        {
            return;
        }

        e.Handled = true;

        var flyout = new MenuFlyout();

        var open = new MenuFlyoutItem { Text = "Открыть" };
        open.Click += (_, _) =>
        {
            ItemActivated?.Invoke(this, item);
            HideMenu();
        };
        flyout.Items.Add(open);

        if (item.ExecutablePath is { Length: > 0 } path)
        {
            var location = new MenuFlyoutItem { Text = "Открыть расположение файла" };
            location.Click += (_, _) => ShellTargets.ShowInExplorer(path);
            flyout.Items.Add(location);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());

        var pinned = _settings.GetSettings().DockPinnedApps;
        var isPinned = pinned.Any(id => string.Equals(id, item.ApplicationId, StringComparison.OrdinalIgnoreCase));

        var pin = new MenuFlyoutItem { Text = isPinned ? "Открепить от Dock" : "Закрепить в Dock" };
        pin.Click += (_, _) => PinToggleRequested?.Invoke(this, item);
        flyout.Items.Add(pin);

        flyout.ShowAt(AppsGrid, new FlyoutShowOptions { Position = e.GetPosition(AppsGrid) });
    }

    private void OnMainWindowClick(object sender, RoutedEventArgs e)
    {
        _animation.Stop();
        _animating = false;
        _closing = false;
        AppWindow.Hide();
        Hidden?.Invoke(this, EventArgs.Empty);
        MainWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
        HideMenu();
    }

    private void OnExplorerClick(object sender, RoutedEventArgs e)
    {
        ExplorerRequested?.Invoke(this, EventArgs.Empty);
        HideMenu();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs args)
    {
        switch (args.Key)
        {
            case VirtualKey.Escape:
                HideMenu();
                args.Handled = true;
                break;

            case VirtualKey.Enter:
                if (ReferenceEquals(args.OriginalSource, SearchBox) && AppsGrid.Items.Count > 0)
                {
                    if (AppsGrid.Items[0] is StartMenuItemViewModel first)
                    {
                        ItemActivated?.Invoke(this, first);
                        HideMenu();
                        args.Handled = true;
                    }
                }

                break;

            default:
                break;
        }
    }

    /// <summary>Тот же Acrylic-фон, что у остальных панелей, но с ползунком меню «Пуск».</summary>
    private void ApplyBackdrop()
    {
        if (_closed)
        {
            return;
        }

        var previous = _backdrop;
        SystemBackdrop = null;
        previous?.Dispose();
        _backdrop = _themes.CreatePanelBackdrop(PanelSurface.StartMenu) as PanelBackdrop;
        SystemBackdrop = _backdrop;
    }

    /// <summary>Текст поиска изменился; владелец может дополнить выдачу.</summary>
    public event EventHandler<string>? SearchFilterRequested;

    /// <summary>Меню скрылось (после анимации ухода или мгновенно).</summary>
    public event EventHandler? Hidden;

    /// <summary>Пользователь выбрал приложение.</summary>
    public event EventHandler<StartMenuItemViewModel>? ItemActivated;

    /// <summary>Просят закрепить или открепить приложение в dock.</summary>
    public event EventHandler<StartMenuItemViewModel>? PinToggleRequested;

    /// <summary>Нажата кнопка «Glaze Shell»: открыть главное окно приложения.</summary>
    public event EventHandler? MainWindowRequested;

    /// <summary>Нажата кнопка «Параметры».</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>Нажата кнопка «Проводник».</summary>
    public event EventHandler? ExplorerRequested;
}
