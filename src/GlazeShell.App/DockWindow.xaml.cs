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
using Windows.Foundation;
using Windows.Graphics;

namespace GlazeShell.App;

/// <summary>
/// Плавающий dock (A1, базовый слайс): topmost-окно без записи в панели задач, со стеклянным
/// фоном и скруглёнными углами. Показывается без перехвата фокуса, поэтому нажатия не
/// забирают фокус у активного приложения.
/// </summary>
public sealed partial class DockWindow : Window
{
    private const int TileSize = 52;
    private const int Gap = 5;
    private const int SeparatorWidth = 2;
    private const int PaddingHorizontal = 14;
    private const int PaddingTop = 22;
    private const int PaddingBottom = 12;

    /// <summary>Радиус капсулы dock (DESIGN §7.5: радиус 36).</summary>
    private const int PillRadius = 36;

    /// <summary>
    /// Максимальный подъём и масштаб плитки под курсором (DESIGN §7.5: 16 px / 1.2).
    /// Верхний отступ окна (22 px) подобран так, чтобы увеличенная плитка не выходила
    /// за границы капсулы: 5.2 px прироста + 16 px подъёма = 21.2 px.
    /// </summary>
    private const double HoverLift = 16;
    private const double HoverScale = 1.2;

    /// <summary>
    /// Радиус зоны магнификации: влияние курсора на плитку спадает как гауссова кривая
    /// <c>exp(-(d/σ)²)</c>. При σ ≈ 1.2·плитки сосед получает около 1.08 / 6 px, как в
    /// DESIGN §7.5, а вторая плитка — уже почти ноль. Это даёт непрерывный «горб» вокруг
    /// курсора, как в dock macOS, вместо увеличения строго одной плитки с соседями.
    /// </summary>
    private const double MagnifySigma = TileSize * 1.2;

    private static readonly TimeSpan HoverDuration = TimeSpan.FromMilliseconds(180);

    /// <summary>Порог, ниже которого пересчёт визуала пропускается (меньше пиксельного шума).</summary>
    private const double MagnifyScaleEpsilon = 0.002;
    private const double MagnifyLiftEpsilon = 0.05;

    private readonly Window _owner;
    private readonly ThemeApplicator _themes;
    private readonly IDisposable _settingsChanged;

    private nint _hwnd;

    // Кэш раскладки плиток в координатах ItemsControl: пересчитывается после изменения состава
    // и при изменении размеров, чтобы на каждое движение курсора не обходить визуальное дерево.
    private double[] _tileCenters = [];
    private double[] _tileHalfWidths = [];
    private double[] _lastScale = [];
    private double[] _lastLift = [];
    private bool _centersDirty = true;
    private int _glowIndex = -1;

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
        Tiles.SizeChanged += OnTilesSizeChanged;
        _settingsChanged = events.Subscribe<SettingsChanged>(_ => _themes.ApplyFromSettings(Root));

        ConfigureOverlay();

        Closed += (_, _) =>
        {
            ViewModel.ActivateRequested -= OnActivateRequested;
            ViewModel.Items.CollectionChanged -= OnItemsChanged;
            Tiles.SizeChanged -= OnTilesSizeChanged;
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
    /// Ставит dock по центру основного монитора вплотную к нижнему краю экрана: панель не
    /// парит над краем, а растворяется в нём — нижние углы прямые, скруглены только верхние
    /// (см. <see cref="OverlayWindow.ApplyTopRoundedRegion"/>). Рабочая область не
    /// резервируется, поэтому dock не смещает окна. Ширина считается по составу плиток:
    /// окно не должно быть шире содержимого, иначе стекло вышло бы за «pill».
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
        var top = bounds.Y + bounds.Height - height;

        AppWindow.MoveAndResize(new RectInt32(left, top, width, height));

        // Капсула: окно обрезается прямоугольником со скруглённым верхом, поэтому
        // acrylic-backdrop заполняет только саму панель, а не прямоугольник вокруг неё.
        if (OverlayWindow.TryGetClientSize(_hwnd, out var clientWidth, out var clientHeight))
        {
            OverlayWindow.ApplyTopRoundedRegion(
                _hwnd,
                new PixelRect(0, 0, clientWidth, clientHeight),
                (int)Math.Round(PillRadius * scale));
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _centersDirty = true;
        _glowIndex = -1;
        UpdateDockSize();
    }

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

    /// <summary>
    /// Непрерывная магнификация: на каждое движение курсора каждая плитка получает масштаб и
    /// подъём по гауссову спаду от расстояния до курсора. Плитка под курсором дополнительно
    /// подсвечивается акцентной рамкой. Так dock ведёт себя как в macOS: под курсором
    /// вырастает «горб» из нескольких плиток, а не одна плитка с двумя соседями.
    /// </summary>
    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var x = e.GetCurrentPoint(Tiles).Position.X;
        ApplyMagnification(x);
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e) => ResetTiles();

    private void ApplyMagnification(double pointerX)
    {
        EnsureTileLayout();

        var maxDelta = HoverScale - 1.0;
        var sigma = MagnifySigma;
        var hovered = -1;

        for (var index = 0; index < ViewModel.Items.Count; index++)
        {
            if (ViewModel.Items[index].IsSeparator)
            {
                continue;
            }

            var center = _tileCenters[index];

            if (double.IsNaN(center) || TileAt(index) is not { } tile)
            {
                continue;
            }

            var distance = Math.Abs(pointerX - center);
            var normalized = distance / sigma;
            var influence = Math.Exp(-(normalized * normalized));
            var scale = 1.0 + (maxDelta * influence);
            var lift = HoverLift * influence;

            if (Math.Abs(scale - _lastScale[index]) > MagnifyScaleEpsilon ||
                Math.Abs(lift - _lastLift[index]) > MagnifyLiftEpsilon)
            {
                _lastScale[index] = scale;
                _lastLift[index] = lift;
                Animate(tile, lift, scale);
            }

            if (distance <= _tileHalfWidths[index])
            {
                hovered = index;
            }
        }

        if (hovered != _glowIndex)
        {
            SetGlow(TileAt(_glowIndex), false);
            SetGlow(TileAt(hovered), true);
            _glowIndex = hovered;
        }
    }

    private void OnTilesSizeChanged(object sender, SizeChangedEventArgs e) => _centersDirty = true;

    /// <summary>
    /// Пересчитывает кэш центров и полуширин плиток в координатах <see cref="Tiles"/>. Вызывается
    /// лениво из <see cref="ApplyMagnification"/>: к моменту движения курсора раскладка готова,
    /// а после изменения состава плиток (<see cref="_centersDirty"/>) кэш обновляется один раз.
    /// </summary>
    private void EnsureTileLayout()
    {
        var count = ViewModel.Items.Count;

        if (!_centersDirty && _tileCenters.Length == count)
        {
            return;
        }

        _tileCenters = new double[count];
        _tileHalfWidths = new double[count];
        _lastScale = new double[count];
        _lastLift = new double[count];

        for (var index = 0; index < count; index++)
        {
            _lastScale[index] = 1.0;
            _tileCenters[index] = double.NaN;

            if (TileAt(index) is not { } tile)
            {
                continue;
            }

            var origin = tile.TransformToVisual(Tiles).TransformPoint(new Point(0, 0));
            _tileCenters[index] = origin.X + (tile.ActualWidth / 2);
            _tileHalfWidths[index] = tile.ActualWidth / 2;
        }

        _centersDirty = false;
    }

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
        else if (item.IsFolder || item.IsTrash)
        {
            var open = new MenuFlyoutItem { Text = "Открыть" };
            open.Click += async (_, _) => await ViewModel.ActivateItemAsync(item);
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

        for (var index = 0; index < _lastScale.Length; index++)
        {
            _lastScale[index] = 1.0;
            _lastLift[index] = 0.0;
        }

        _glowIndex = -1;
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
