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
    private const int WidthDips = 420;

    /// <summary>
    /// Высота меню: не на весь экран, а прямоугольник по центру левого края. Небольшие
    /// экраны ужимают его, чтобы сверху и снизу остались одинаковые отступы.
    /// </summary>
    private const int HeightDips = 560;

    /// <summary>Минимальный отступ сверху и снизу от рабочей области.</summary>
    private const int VerticalMarginDips = 24;

    private const double SlideInMilliseconds = 240;
    private const double SlideOutMilliseconds = 170;

    private readonly ThemeApplicator _themes;
    private readonly IMonitorManager _monitors;
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

    public StartMenuWindow(
        ThemeApplicator themes,
        IMonitorManager monitors,
        ISettingsManager settings,
        IEventManager events)
    {
        _themes = themes ?? throw new ArgumentNullException(nameof(themes));
        _monitors = monitors ?? throw new ArgumentNullException(nameof(monitors));
        InitializeComponent();
        Title = "Glaze Shell — Пуск";

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        _themes.ApplyFromSettings(Root);
        ApplyBackdrop();

        _settingsChanged = events.Subscribe<SettingsChanged>(_ =>
        {
            _themes.ApplyFromSettings(Root);
            ApplyBackdrop();
        });

        _animation = DispatcherQueue.CreateTimer();
        _animation.Interval = TimeSpan.FromMilliseconds(16);
        _animation.Tick += OnAnimationTick;

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
        OverlayWindow.MakeTopMostToolWindow(_hwnd);
        OverlayWindow.UseSquareCorners(_hwnd);

        var monitor = _monitors.GetPrimaryMonitor();

        if (monitor is null)
        {
            return;
        }

        var scale = OverlayWindow.GetScaleFactor(_hwnd);
        var width = (int)Math.Round(WidthDips * scale);
        var area = monitor.WorkingArea ?? monitor.Bounds;
        var margin = (int)Math.Round(VerticalMarginDips * scale);
        var height = Math.Min((int)Math.Round(HeightDips * scale), area.Height - (margin * 2));
        var y = area.Y + ((area.Height - height) / 2);

        _shownBounds = new RectInt32(area.X + margin, y, width, height);
        _animationFrom = new RectInt32(_shownBounds.X - (int)Math.Round(32 * scale), y, width, height);
        _animationTo = _shownBounds;
        _slidingIn = true;
        _closing = false;
        _animation.Stop();

        AppWindow.MoveAndResize(_animationFrom);
        OverlayWindow.ApplyRoundedRegions(_hwnd, [new PixelRect(0, 0, width, height)], (int)Math.Round(20 * scale));
        _pointerDown = OverlayWindow.IsLeftButtonDown();
        _animating = true;
        _animation.Interval = TimeSpan.FromMilliseconds(16);

        SearchBox.Text = string.Empty;
        ApplyFilter(string.Empty);

        Activate();
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
            AppWindow.Hide();
            _animation.Stop();
            return;
        }

        _animationTo = new RectInt32(_shownBounds.X - (int)Math.Round(24 * OverlayWindow.GetScaleFactor(_hwnd)), _shownBounds.Y, _shownBounds.Width, _shownBounds.Height);
        var position = AppWindow.Position;
        _animationFrom = new RectInt32(position.X, position.Y, _shownBounds.Width, _shownBounds.Height);
        _animating = true;
        _animation.Interval = TimeSpan.FromMilliseconds(16);
        _slidingIn = false;
        _closing = true;
        _watch.Restart();
        _animation.Start();
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
        var eased = 1 - Math.Pow(1 - progress, 3);
        Root.Opacity = _slidingIn ? 0.65 + (0.35 * eased) : 1 - (0.35 * eased);

        var x = (int)Math.Round(_animationFrom.X + ((_animationTo.X - _animationFrom.X) * eased));

        AppWindow.MoveAndResize(new RectInt32(x, _animationFrom.Y, _animationFrom.Width, _animationFrom.Height));

        if (progress < 1)
        {
            return;
        }

        _animating = false;

        if (_slidingIn)
        {
            AppWindow.MoveAndResize(_shownBounds);
            _animation.Interval = TimeSpan.FromMilliseconds(50);
        }
        else
        {
            _closing = false;
            AppWindow.Hide();
            _animation.Stop();
        }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            HideMenu();
        }
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

    private void OnMainWindowClick(object sender, RoutedEventArgs e)
    {
        HideMenu();
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
        _backdrop = _themes.CreatePanelBackdrop(PanelSurface.StartMenu) as PanelBackdrop;
        SystemBackdrop = _backdrop;
        previous?.Dispose();
    }

    /// <summary>Текст поиска изменился; владелец может дополнить выдачу.</summary>
    public event EventHandler<string>? SearchFilterRequested;

    /// <summary>Пользователь выбрал приложение.</summary>
    public event EventHandler<StartMenuItemViewModel>? ItemActivated;

    /// <summary>Нажата кнопка «Glaze Shell»: открыть главное окно приложения.</summary>
    public event EventHandler? MainWindowRequested;

    /// <summary>Нажата кнопка «Параметры».</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>Нажата кнопка «Проводник».</summary>
    public event EventHandler? ExplorerRequested;
}
