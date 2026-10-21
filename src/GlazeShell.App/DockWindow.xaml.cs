using System.Collections.Specialized;
using System.Diagnostics;
using GlazeShell.App.Presentation;
using GlazeShell.App.Theming;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Windows.WindowManagement;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;

namespace GlazeShell.App;

/// <summary>
/// Плавающий dock (A1, базовый слайс): topmost-окно без записи в панели задач, со стеклянным
/// фоном и скруглёнными углами. Показывается без перехвата фокуса, поэтому нажатия не
/// забирают фокус у активного приложения.
/// </summary>
public sealed partial class DockWindow : Window
{
    private const int TileSize = 40;
    private const int Gap = 5;
    private const int SeparatorWidth = 2;
    private const int PaddingHorizontal = 28;
    private const int PaddingTop = 16;
    private const int PaddingBottom = 8;

    /// <summary>Радиус капсулы dock (DESIGN §7.5: радиус 36).</summary>
    private const int PillRadius = 22;

    /// <summary>
    /// Максимальный подъём и масштаб плитки под курсором (DESIGN §7.5: 16 px / 1.2).
    /// Верхний отступ окна (16 px) подобран так, чтобы увеличенная плитка не выходила
    /// за границы капсулы: 4 px прироста + 12 px подъёма = 16 px.
    /// </summary>
    private const double HoverLift = 12;
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

    // Магнификация не может опираться только на PointerMoved: окно показано без активации
    // (WS_EX_NOACTIVATE), и WinUI не всегда доставляет ему события ввода. Поэтому позиция
    // курсора читается глобально таймером и переводится в координаты ленты плиток.
    private readonly DispatcherTimer _cursorWatch = new() { Interval = TimeSpan.FromMilliseconds(33) };

    private nint _hwnd;
    private double _windowLeft;
    private double _windowTop;
    private bool _cursorInside;
    private bool _cursorWatchRunning;

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
        _settingsChanged = events.Subscribe<SettingsChanged>(OnSettingsChanged);

        _cursorWatch.Tick += OnCursorWatchTick;

        ConfigureOverlay();

        Closed += (_, _) =>
        {
            _cursorWatch.Stop();
            _cursorWatch.Tick -= OnCursorWatchTick;
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

        ApplyBackdrop();

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        OverlayWindow.MakeTopMostToolWindow(_hwnd);
        OverlayWindow.SetNonActivating(_hwnd, true);

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

        _windowLeft = left;
        _windowTop = top;

        if (!_cursorWatchRunning)
        {
            _cursorWatchRunning = true;
            _cursorWatch.Start();
        }

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
    /// Реакция на изменение настроек: переприменяет тему и обновляет фон окна. Если включено
    /// «уменьшить движение», текущая магнификация сразу сбрасывается, чтобы dock мгновенно
    /// принял спокойный вид.
    /// </summary>
    private void OnSettingsChanged(SettingsChanged _)
    {
        _themes.ApplyFromSettings(Root);
        ApplyBackdrop();

        if (_themes.ReduceMotion)
        {
            ResetTiles();
        }
    }

    /// <summary>
    /// Включает или отключает стеклянный фон окна. В режиме высокой контрастности стекло
    /// убирается: полупрозрачный фон поверх произвольного рабочего стола нечитаем, а
    /// непрозрачный цвет берётся из системной палитры.
    /// </summary>
    private void ApplyBackdrop() =>
        SystemBackdrop = _themes.HighContrast ? null : new DesktopAcrylicBackdrop();

    /// <summary>
    /// Таймер читает глобальную позицию курсора и, если он над dock, ведёт магнификацию.
    /// Так эффект не зависит от того, доставляет ли WinUI события ввода окну, показанному
    /// без активации. Когда курсор уходит с панели, плитки плавно возвращаются на место.
    /// </summary>
    private void OnCursorWatchTick(object? sender, object e)
    {
        if (_hwnd == 0 || !OverlayWindow.TryGetCursorPosition(_hwnd, out var clientX, out var clientY))
        {
            return;
        }

        if (!OverlayWindow.TryGetClientSize(_hwnd, out var clientWidth, out var clientHeight))
        {
            return;
        }

        var inside = clientX >= 0 && clientY >= 0 && clientX < clientWidth && clientY < clientHeight;

        if (!inside)
        {
            if (_cursorInside)
            {
                _cursorInside = false;
                ResetTiles();
            }

            return;
        }

        _cursorInside = true;

        var scale = OverlayWindow.GetScaleFactor(_hwnd);
        var tilesX = clientX / (scale <= 0 ? 1.0 : scale);
        ApplyMagnification(tilesX);
    }

    /// <summary>
    /// Непрерывная магнификация: на каждое движение курсора каждая плитка получает масштаб и
    /// подъём по гауссову спаду от расстояния до курсора. Плитка под курсором дополнительно
    /// подсвечивается акцентной рамкой. Так dock ведёт себя как в macOS: под курсором
    /// вырастает «горб» из нескольких плиток, а не одна плитка с двумя соседями.
    /// </summary>
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
    /// Клавиатурный путь к плитке (I5): Enter или пробел активируют приложение так же, как
    /// клик мышью. Контекстное меню открывается Shift+F10 или клавишей меню — стандартным
    /// путём WinUI, поэтому отдельная обработка не нужна.
    /// </summary>
    private async void OnTileKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not Border { DataContext: DockItemViewModel item })
        {
            return;
        }

        if (e.Key is not (VirtualKey.Enter or VirtualKey.Space))
        {
            return;
        }

        e.Handled = true;
        ResetTiles();
        await ViewModel.ActivateItemAsync(item);
    }

    /// <summary>
    /// Кольцо фокуса плитки (доступность, I5). Плитка — обычная рамка, у которой нет
    /// встроенного фокус-визуала WinUI, поэтому кольцо включается вручную по событиям фокуса.
    /// </summary>
    private void OnTileGotFocus(object sender, RoutedEventArgs e) => SetFocusRing(sender as Border, visible: true);

    private void OnTileLostFocus(object sender, RoutedEventArgs e) => SetFocusRing(sender as Border, visible: false);

    /// <summary>
    /// Показывает или прячет кольцо фокуса плитки. Кольцо лежит последним потомком
    /// содержимого плитки и опознаётся по имени, заданному в разметке.
    /// </summary>
    private static void SetFocusRing(Border? tile, bool visible)
    {
        if (tile?.Child is not Grid content)
        {
            return;
        }

        foreach (var child in content.Children)
        {
            if (child is Border { Name: "FocusRing" } ring)
            {
                ring.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                return;
            }
        }
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
    /// Подъём и масштаб выполняются визуальным слоем: масштаб — Composition-анимацией
    /// <see cref="Visual.Scale"/>, а подъём — свойством <see cref="UIElement.Translation"/>,
    /// которое учитывает раскладка. Анимировать <see cref="Visual.Offset"/> напрямую нельзя:
    /// раскладка перезаписывает его на каждом проходе, и подъём пропадает. Оба свойства
    /// анимируются одной длительностью, поэтому плитка реагирует плавно и предсказуемо.
    /// </summary>
    private void Animate(Border? tile, double lift, double scale)
    {
        if (tile is null)
        {
            return;
        }

        var (scaleTransform, translateTransform) = EnsureTransforms(tile);

        // Доступность (I5): при «уменьшить движение» плитка перескакивает к целевому виду
        // мгновенно, без Storyboard и плавных переходов.
        if (_themes.ReduceMotion)
        {
            scaleTransform.ScaleX = scale;
            translateTransform.Y = -lift;
            return;
        }

        var duration = HoverDuration;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        var scaleAnimation = new DoubleAnimation
        {
            To = scale,
            Duration = duration,
            EasingFunction = easing,
        };
        Storyboard.SetTarget(scaleAnimation, scaleTransform);
        Storyboard.SetTargetProperty(scaleAnimation, nameof(ScaleTransform.ScaleX));

        var liftAnimation = new DoubleAnimation
        {
            To = -lift,
            Duration = duration,
            EasingFunction = easing,
        };
        Storyboard.SetTarget(liftAnimation, translateTransform);
        Storyboard.SetTargetProperty(liftAnimation, nameof(TranslateTransform.Y));

        var storyboard = new Storyboard();
        storyboard.Children.Add(scaleAnimation);
        storyboard.Children.Add(liftAnimation);
        storyboard.Begin();
    }

    /// <summary>
    /// Акцент вокруг плитки под курсором. Ореол по дизайну требует размытия тени, которого у
    /// обычных элементов нет: плитка под курсором получает акцентную рамку. Вне наведения
    /// рамки нет вовсе — контур остаётся только у региона окна, поэтому стекло не обрастает
    /// серыми ободками, а «стеклянность» даёт одно преломление Acrylic-фона.
    /// </summary>
    private static void SetGlow(Border? tile, bool active)
    {
        if (tile is null)
        {
            return;
        }

        tile.BorderBrush = active ? ResourceBrush("GlazeAccentBrush") : null;
        tile.BorderThickness = new Thickness(active ? 2 : 0);
    }

    private static Brush? ResourceBrush(string key) =>
        Application.Current.Resources.TryGetValue(key, out var resource) && resource is Brush brush
            ? brush
            : null;

    /// <summary>
    /// Возвращает пару трансформаций плитки (масштаб + перенос), создавая
    /// <see cref="TransformGroup"/> при первом обращении. Масштаб растёт от нижней кромки
    /// (<see cref="ScaleTransform.CenterY"/> = 1), поэтому плитка «тянется» вверх, как в dock
    /// macOS, а не раздувается в обе стороны. Композиция из двух преобразований нужна потому,
    /// что Storyboard адресует одно свойство за анимацию: <c>ScaleX</c> и <c>Y</c> — разные
    /// элементы группы.
    /// </summary>
    private static (ScaleTransform Scale, TranslateTransform Translate) EnsureTransforms(Border tile)
    {
        if (tile.RenderTransform is TransformGroup { Children.Count: >= 2 } group
            && group.Children[0] is ScaleTransform scale
            && group.Children[1] is TranslateTransform translate)
        {
            return (scale, translate);
        }

        var newScale = new ScaleTransform { CenterX = 0.5, CenterY = 1.0 };
        var newTranslate = new TranslateTransform();
        var newGroup = new TransformGroup();
        newGroup.Children.Add(newScale);
        newGroup.Children.Add(newTranslate);
        tile.RenderTransform = newGroup;
        return (newScale, newTranslate);
    }
}
