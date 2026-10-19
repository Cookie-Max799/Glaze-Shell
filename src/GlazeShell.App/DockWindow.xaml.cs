using System.Collections.Specialized;
using System.Diagnostics;
using System.Numerics;
using GlazeShell.App.Presentation;
using GlazeShell.App.Theming;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Windows.WindowManagement;
using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace GlazeShell.App;

/// <summary>
/// Плавающий dock (A1, базовый слайс): topmost-окно без записи в панели задач, со стеклянным
/// фоном и скруглёнными углами. Показывается без перехвата фокуса, поэтому нажатия не
/// забирают фокус у активного приложения.
/// </summary>
public sealed partial class DockWindow : Window
{
    private const int TileSize = 60;
    private const int Gap = 5;
    private const int SeparatorWidth = 1;
    private const int PaddingHorizontal = 14;
    private const int PaddingTop = 16;
    private const int PaddingBottom = 12;
    private const int BottomOffset = 18;

    /// <summary>
    /// Подъём и масштаб плитки при наведении. Отличаются от значений дизайна (16 px, 1.2):
    /// плитка увеличивается внутри окна, а всё, что вышло за его границы, было бы обрезано.
    /// </summary>
    private const double HoverLift = 10;
    private const double HoverScale = 1.18;
    private const double NeighborLift = 4;
    private const double NeighborScale = 1.06;

    private static readonly TimeSpan HoverDuration = TimeSpan.FromMilliseconds(140);

    private readonly Window _owner;
    private readonly ThemeApplicator _themes;
    private readonly IDisposable _settingsChanged;

    private nint _hwnd;

    public DockWindow(
        ISettingsManager settings,
        IApplicationManager applications,
        IApplicationLauncher launcher,
        IWindowManager windows,
        IEventManager events,
        ThemeApplicator themes,
        Window owner)
    {
        ArgumentNullException.ThrowIfNull(events);

        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _themes = themes ?? throw new ArgumentNullException(nameof(themes));

        ViewModel = new DockViewModel(applications, launcher, windows, settings, events, DispatcherQueue);

        InitializeComponent();
        Title = "Glaze Shell — Dock";

        _themes.ApplyFromSettings(Root);

        ViewModel.ActivateRequested += OnActivateRequested;
        ViewModel.Items.CollectionChanged += OnItemsChanged;
        _settingsChanged = events.Subscribe<SettingsChanged>(_ => _themes.ApplyFromSettings(Root));

        ConfigureOverlay();

        Closed += (_, _) =>
        {
            ViewModel.ActivateRequested -= OnActivateRequested;
            ViewModel.Items.CollectionChanged -= OnItemsChanged;
            _settingsChanged.Dispose();
            ViewModel.Dispose();
        };
    }

    public DockViewModel ViewModel { get; }

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

        UpdateDockSize();
        OverlayWindow.ShowWithoutActivation(_hwnd);

        _ = ViewModel.InitializeAsync();
    }

    /// <summary>
    /// Ставит dock по центру основного монитора на 18 px выше края рабочей области. Рабочая
    /// область не резервируется, поэтому dock не смещает окна. Ширина считается по составу
    /// плиток: окно не должно быть шире содержимого, иначе стекло вышло бы за «pill».
    /// </summary>
    private void UpdateDockSize()
    {
        var bounds = DisplayArea.Primary.OuterBounds;
        var scale = OverlayWindow.GetScaleFactor(_hwnd);

        var tiles = 0;
        var separators = 0;

        foreach (var item in ViewModel.Items)
        {
            if (item.IsSeparator)
            {
                separators++;
            }
            else
            {
                tiles++;
            }
        }

        var count = ViewModel.Items.Count;
        var content = (tiles * TileSize) + (separators * SeparatorWidth) + ((count > 1 ? count - 1 : 0) * Gap);

        var width = (int)Math.Round((content + (PaddingHorizontal * 2)) * scale);
        var height = (int)Math.Round((TileSize + PaddingTop + PaddingBottom) * scale);
        var left = bounds.X + ((bounds.Width - width) / 2);
        var top = bounds.Y + bounds.Height - height - (int)Math.Round(BottomOffset * scale);

        AppWindow.MoveAndResize(new RectInt32(left, top, width, height));
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateDockSize();

    private void OnActivateRequested(object? sender, EventArgs e)
    {
        try
        {
            var appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(
                WinRT.Interop.WindowNative.GetWindowHandle(_owner)));

            if (appWindow.Presenter is OverlappedPresenter presenter &&
                presenter.State == OverlappedPresenterState.Minimized)
            {
                presenter.Restore();
            }

            _owner.Activate();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.Dock: activating the main window failed. {exception}");
        }
    }

    private void OnTilePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border { DataContext: DockItemViewModel item })
        {
            return;
        }

        var index = ViewModel.Items.IndexOf(item);

        Animate(TileAt(index), HoverLift, HoverScale);
        SetGlow(TileAt(index), true);
        Animate(TileAt(index - 1), NeighborLift, NeighborScale);
        Animate(TileAt(index + 1), NeighborLift, NeighborScale);
    }

    private void OnTilePointerExited(object sender, PointerRoutedEventArgs e) => ResetTiles();

    private async void OnTileTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not Border { DataContext: DockItemViewModel item })
        {
            return;
        }

        e.Handled = true;
        ResetTiles();
        await ViewModel.ActivateItemAsync(item);
    }

    /// <summary>
    /// Плитка приложения по контекстному меню закрепляется или открепляется. Меню требует
    /// активации, поэтому окно временно разрешает перехват фокуса и снова запрещает его
    /// после закрытия меню.
    /// </summary>
    private void OnTileRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not Border { DataContext: DockItemViewModel item } tile)
        {
            return;
        }

        e.Handled = true;
        ResetTiles();

        OverlayWindow.SetNonActivating(_hwnd, false);
        Activate();

        var flyout = new MenuFlyout();

        if (item.IsStart)
        {
            var open = new MenuFlyoutItem { Text = "Открыть Glaze Shell" };
            open.Click += (_, _) => OnActivateRequested(this, EventArgs.Empty);
            flyout.Items.Add(open);
        }
        else if (item.ApplicationId is not null)
        {
            var toggle = new MenuFlyoutItem { Text = item.PinLabel };
            toggle.Click += (_, _) => ViewModel.TogglePinned(item);
            flyout.Items.Add(toggle);

            if (item.IsRunning)
            {
                var focus = new MenuFlyoutItem { Text = "Переключиться на окно" };
                focus.Click += async (_, _) => await ViewModel.ActivateItemAsync(item);
                flyout.Items.Add(focus);
            }
        }
        else
        {
            return;
        }

        flyout.Closed += (_, _) =>
        {
            OverlayWindow.SetNonActivating(_hwnd, true);
            ResetTiles();
        };

        flyout.ShowAt(tile, new FlyoutShowOptions { Position = e.GetPosition(tile) });
    }

    /// <summary>
    /// Плитка приложения по индексу: шаблон элемента содержит плитку-<see cref="Border"/>,
    /// которая и находится обходом визуального дерева.
    /// </summary>
    private Border? TileAt(int index)
    {
        if (index < 0 || index >= ViewModel.Items.Count)
        {
            return null;
        }

        return Tiles.ContainerFromIndex(index) is DependencyObject container ? FindTile(container) : null;
    }

    private static Border? FindTile(DependencyObject parent)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);

        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is Border { Width: TileSize } border)
            {
                return border;
            }

            if (FindTile(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private void ResetTiles()
    {
        for (var index = 0; index < ViewModel.Items.Count; index++)
        {
            var tile = TileAt(index);
            Animate(tile, 0, 1.0);
            SetGlow(tile, false);
        }
    }

    /// <summary>
    /// Подъём и масштаб выполняются визуальным слоем (Composition), а не анимацией свойств
    /// разметки: так плитка не пересчитывает соседей, а реакция укладывается в 50 мс.
    /// </summary>
    private static void Animate(Border? tile, double lift, double scale)
    {
        if (tile is null)
        {
            return;
        }

        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(tile);
            var compositor = visual.Compositor;

            visual.CenterPoint = new Vector3(TileSize / 2f, TileSize / 2f, 0f);

            var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
            scaleAnimation.InsertKeyFrame(1f, new Vector3((float)scale, (float)scale, 1f));
            scaleAnimation.Duration = HoverDuration;
            visual.StartAnimation(nameof(Visual.Scale), scaleAnimation);

            var offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
            offsetAnimation.InsertKeyFrame(1f, new Vector3(0f, -(float)lift, 0f));
            offsetAnimation.Duration = HoverDuration;
            visual.StartAnimation(nameof(Visual.Offset), offsetAnimation);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.Dock: a hover animation failed. {exception}");
        }
    }

    /// <summary>
    /// Акцентная рамка вокруг плитки под курсором. Ореол по дизайну требует размытия тени,
    /// которого у обычных элементов нет: рамка акцента даёт тот же акцент, но без создания
    /// отдельного визуального слоя.
    /// </summary>
    private static void SetGlow(Border? tile, bool active)
    {
        if (tile is null)
        {
            return;
        }

        tile.BorderBrush = ResourceBrush(active ? "GlazeAccentBrush" : "GlazeBorderBrush");
        tile.BorderThickness = new Thickness(active ? 2 : 1);
    }

    private static Brush ResourceBrush(string key) =>
        Application.Current.Resources.TryGetValue(key, out var resource) && resource is Brush brush
            ? brush
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
}
