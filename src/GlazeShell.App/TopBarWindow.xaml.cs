using GlazeShell.App.Presentation;
using GlazeShell.App.Theming;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Windows.WindowManagement;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace GlazeShell.App;

/// <summary>
/// Плавающая верхняя панель (A6, базовый слайс): topmost-окно без записи в панели задач,
/// со стеклянным фоном и скруглёнными углами. Показывается без перехвата фокуса.
/// </summary>
public sealed partial class TopBarWindow : Window
{
    private const int BarHeight = 48;
    private const int TopOffset = 18;
    private const int SideMargin = 24;

    private readonly ThemeApplicator _themes;
    private readonly IDisposable _settingsChanged;
    private nint _hwnd;

    public TopBarWindow(
        ISettingsManager settings,
        IWindowManager windows,
        IEventManager events,
        ThemeApplicator themes)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(events);

        _themes = themes ?? throw new ArgumentNullException(nameof(themes));

        ViewModel = new TopBarViewModel(windows, events, DispatcherQueue);

        InitializeComponent();
        Title = "Glaze Shell — Верхняя панель";

        _themes.ApplyFromSettings(Root);

        ConfigureOverlay();

        _settingsChanged = events.Subscribe<SettingsChanged>(OnSettingsChanged);
        Activated += OnActivated;

        Closed += (_, _) =>
        {
            ViewModel.Dispose();
            _settingsChanged.Dispose();
        };
    }

    public TopBarViewModel ViewModel { get; }

    private void ConfigureOverlay()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        SystemBackdrop = new DesktopAcrylicBackdrop();

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        OverlayWindow.MakeTopMostToolWindow(_hwnd);
        OverlayWindow.SetNonActivating(_hwnd, true);
        OverlayWindow.UseRoundedCorners(_hwnd);

        PositionOnPrimaryDisplay();
        OverlayWindow.ShowWithoutActivation(_hwnd);
    }

    /// <summary>
    /// Верхняя панель базового слайса показывается на основном мониторе: 24 px по бокам и
    /// 18 px сверху. Рабочая область не резервируется, поэтому панель не смещает окна.
    /// </summary>
    private void PositionOnPrimaryDisplay()
    {
        var bounds = DisplayArea.Primary.OuterBounds;
        var scale = OverlayWindow.GetScaleFactor(_hwnd);

        var margin = (int)Math.Round(SideMargin * scale);
        var top = (int)Math.Round(TopOffset * scale);
        var height = (int)Math.Round(BarHeight * scale);
        var width = Math.Max(1, bounds.Width - (margin * 2));

        AppWindow.MoveAndResize(new RectInt32(bounds.X + margin, bounds.Y + top, width, height));
    }

    /// <summary>
    /// Клик по строке поиска временно разрешает активацию окна, чтобы поле получило фокус
    /// ввода. Остальное время окно не перехватывает фокус активного приложения.
    /// </summary>
    private void OnSearchPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        OverlayWindow.SetNonActivating(_hwnd, false);
        Activate();
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState == WindowActivationState.Deactivated)
        {
            OverlayWindow.SetNonActivating(_hwnd, true);
        }
    }

    private void OnSettingsChanged(SettingsChanged changed) => _themes.ApplyFromSettings(Root);
}
