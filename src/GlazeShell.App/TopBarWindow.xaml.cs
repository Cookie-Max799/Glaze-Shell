using System.ComponentModel;
using System.Diagnostics;
using GlazeShell.App.Presentation;
using GlazeShell.App.Theming;
using GlazeShell.Core.Display;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Windows.WindowManagement;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;

namespace GlazeShell.App;

/// <summary>
/// Плавающая верхняя панель (A6): topmost-окно без записи в панели задач, со стеклянным
/// фоном и скруглёнными углами. Показывается без перехвата фокуса, а по <c>Ctrl K</c>
/// разрешает активацию и ставит фокус в строку поиска Glance (B1).
/// </summary>
public sealed partial class TopBarWindow : Window
{
    private const int BarHeight = 48;
    private const int TopOffset = 18;
    private const int SideMargin = 24;

    /// <summary>Ширина строки поиска: она же служит ограничением при измерении панели результатов.</summary>
    private const int SearchWidth = 440;

    /// <summary>Ширина popover «Центр управления» (D1): как у popover-карточки папки в DESIGN §7.2.</summary>
    private const int QuickSettingsWidth = 380;

    /// <summary>Радиус pill-плиток и панели результатов: половина высоты строки, то есть капсула (DESIGN §7.1).</summary>
    private const int PillRadius = BarHeight / 2;

    /// <summary>Радиус всплывающих панелей (DESIGN §4: popover — 32).</summary>
    private const int PanelRadius = 32;

    private readonly Window _owner;
    private readonly ThemeApplicator _themes;
    private readonly IDisposable _settingsChanged;
    private readonly IDisposable _displayChanged;
    private readonly ShellMonitorTarget _monitorTarget;
    private nint _hwnd;

    /// <summary>Последнее применённое положение окна: панель переносится только при изменении.</summary>
    private RectInt32 _placement;

    public TopBarWindow(
        ISettingsManager settings,
        IApplicationManager applications,
        IApplicationLauncher launcher,
        IWindowManager windows,
        IEventManager events,
        IMonitorManager monitors,
        ThemeApplicator themes,
        Window owner)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(applications);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(monitors);

        _themes = themes ?? throw new ArgumentNullException(nameof(themes));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _monitorTarget = new ShellMonitorTarget(monitors);

        ViewModel = new TopBarViewModel(
            windows,
            launcher,
            new GlanceSearchCatalog(applications, windows),
            settings,
            events,
            DispatcherQueue);

        InitializeComponent();
        Title = "Glaze Shell — Верхняя панель";

        _themes.ApplyFromSettings(Root);

        ConfigureOverlay();

        // Регион и высота окна зависят от разметки: сначала применяем после загрузки, а
        // затем при изменении ширины pill и высоты панели результатов.
        Root.Loaded += (_, _) => UpdateOverlayBounds();
        AppPill.SizeChanged += (_, _) => UpdateOverlayBounds();
        SearchPill.SizeChanged += (_, _) => UpdateOverlayBounds();
        StatusPill.SizeChanged += (_, _) => UpdateOverlayBounds();
        ResultsPanel.SizeChanged += (_, _) => UpdateOverlayBounds();
        QuickSettingsPanel.SizeChanged += (_, _) => UpdateOverlayBounds();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.QuickSettings.PropertyChanged += OnQuickSettingsPropertyChanged;
        ViewModel.ActivateRequested += OnActivateRequested;
        ViewModel.ExplorerRequested += OnExplorerRequested;
        _settingsChanged = events.Subscribe<SettingsChanged>(OnSettingsChanged);
        _displayChanged = events.Subscribe<DisplayChanged>(OnDisplayChanged);
        Activated += OnActivated;

        Closed += (_, _) =>
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            ViewModel.QuickSettings.PropertyChanged -= OnQuickSettingsPropertyChanged;
            ViewModel.ActivateRequested -= OnActivateRequested;
            ViewModel.ExplorerRequested -= OnExplorerRequested;
            ViewModel.Dispose();
            _settingsChanged.Dispose();
            _displayChanged.Dispose();
        };
    }

    public TopBarViewModel ViewModel { get; }

    /// <summary>
    /// Просьба открыть проводник Glaze Shell: окном владеет приложение, поэтому панель
    /// только пересылает запрос наружу.
    /// </summary>
    public event EventHandler<string?>? ExplorerRequested;

    /// <summary>
    /// Показывает панель, временно разрешает активацию окна и ставит фокус в строку поиска.
    /// Вызывается по глобальной горячей клавише <c>Ctrl K</c>.
    /// </summary>
    public void FocusSearch()
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            FocusSearchCore();
            return;
        }

        _ = DispatcherQueue.TryEnqueue(FocusSearchCore);
    }

    private void FocusSearchCore()
    {
        if (_hwnd == 0)
        {
            return;
        }

        ViewModel.CloseQuickSettings();
        OverlayWindow.SetNonActivating(_hwnd, false);
        Activate();
        SearchBox.Focus(FocusState.Programmatic);
    }

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
        OverlayWindow.UseRoundedCorners(_hwnd);

        UpdateOverlayBounds();
        OverlayWindow.ShowWithoutActivation(_hwnd);
    }

    /// <summary>
    /// Раскладывает окно по целевому монитору (24 px по бокам, 18 px сверху) и увеличивает
    /// его по высоте под панель результатов Glance или popover «Центр управления». Рабочая
    /// область не резервируется, поэтому панель не смещает окна приложений.
    /// </summary>
    /// <remarks>
    /// Положение считается по целевому монитору (функция I6): при подключении или отключении
    /// монитора панель переезжает вместе с оболочкой, а её размеры считаются по масштабу
    /// именно этого монитора.
    /// </remarks>
    private void UpdateOverlayBounds()
    {
        if (_hwnd == 0)
        {
            return;
        }

        var target = _monitorTarget.Target;
        var scale = target?.ScaleFactor ?? OverlayWindow.GetScaleFactor(_hwnd);

        var logicalHeight = (double)BarHeight;

        if (ViewModel.HasResults)
        {
            // Панель измеряется независимо от текущей высоты окна: иначе Auto-строка
            // получила бы ноль доступной высоты и её натуральный размер был бы потерян.
            var constraint = SearchPill.ActualWidth > 0 ? SearchPill.ActualWidth : SearchWidth;
            logicalHeight += MeasurePanelHeight(ResultsPanel, constraint);
        }
        else if (ViewModel.QuickSettings.IsOpen)
        {
            // У popover «Центр управления» отступ сверху уже равен строке pill плюс зазор,
            // поэтому он прижат к правому краю и сам задаёт нужную высоту окна.
            logicalHeight = MeasurePanelHeight(QuickSettingsPanel, QuickSettingsWidth);
        }

        var pixelHeight = Math.Max(1, (int)Math.Round(logicalHeight * scale));

        int left;
        int top;
        int width;

        if (target is not null)
        {
            var placement = ShellPlacementResolver.Resolve(
                target,
                ShellAnchor.Top,
                ShellAlignment.Stretch,
                widthDip: 0,
                heightDip: logicalHeight,
                marginDip: SideMargin,
                verticalMarginDip: TopOffset);

            left = placement.X;
            top = placement.Y;
            width = placement.Width;
        }
        else
        {
            // Мониторы неизвестны: панель остаётся на основном экране, как и раньше.
            var bounds = DisplayArea.Primary.OuterBounds;
            var margin = (int)Math.Round(SideMargin * scale);
            left = bounds.X + margin;
            top = bounds.Y + (int)Math.Round(TopOffset * scale);
            width = Math.Max(1, bounds.Width - (margin * 2));
        }

        var placementRect = new RectInt32(left, top, width, pixelHeight);

        if (placementRect != _placement)
        {
            AppWindow.MoveAndResize(placementRect);
            _placement = placementRect;
        }

        UpdateWindowRegion();
    }

    /// <summary>
    /// Измеряет всплывающую панель, пока её строка сетки ещё не получила высоту. Видимость
    /// принудительно включается на время замера: свёрнутый элемент сообщил бы нулевой размер,
    /// а окно осталось бы высотой в одну строку.
    /// </summary>
    private static double MeasurePanelHeight(FrameworkElement panel, double width)
    {
        var visibility = panel.Visibility;
        panel.Visibility = Visibility.Visible;
        panel.Measure(new Size(Math.Max(1, width), double.PositiveInfinity));
        var height = panel.DesiredSize.Height;
        panel.Visibility = visibility;
        return height;
    }

    /// <summary>
    /// Вырезает из окна три pill и всплывающую панель (результаты Glance или «Центр
    /// управления») по их фактическим границам: системный acrylic-backdrop рисуется только
    /// внутри этих фигур, поэтому панель выглядит как плавающие стёкла, а не как одна полоса
    /// во всю ширину (DESIGN §7.1). Pills — капсулы, у панелей радиус popover (DESIGN §4).
    /// </summary>
    private void UpdateWindowRegion()
    {
        if (_hwnd == 0)
        {
            return;
        }

        var scale = OverlayWindow.GetScaleFactor(_hwnd);
        var pillRadius = (int)Math.Round(PillRadius * scale);
        var panelRadius = (int)Math.Round(PanelRadius * scale);
        var regions = new List<PixelRegion>(5);

        AddRegion(AppPill, pillRadius);
        AddRegion(SearchPill, pillRadius);
        AddRegion(StatusPill, pillRadius);

        if (ViewModel.HasResults)
        {
            AddRegion(ResultsPanel, panelRadius);
        }

        if (ViewModel.QuickSettings.IsOpen)
        {
            AddRegion(QuickSettingsPanel, panelRadius);
        }

        if (regions.Count == 0)
        {
            return;
        }

        OverlayWindow.ApplyRoundedRegions(_hwnd, regions);

        void AddRegion(FrameworkElement element, int radius)
        {
            if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
            {
                return;
            }

            var elementBounds = element.TransformToVisual(Root).TransformBounds(
                new Rect(0, 0, element.ActualWidth, element.ActualHeight));

            regions.Add(new PixelRegion(
                new PixelRect(
                    (int)Math.Round(elementBounds.X * scale),
                    (int)Math.Round(elementBounds.Y * scale),
                    (int)Math.Round(elementBounds.Width * scale),
                    (int)Math.Round(elementBounds.Height * scale)),
                radius));
        }
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

    /// <summary>
    /// Клик по правому pill открывает «Центр управления» (D1): окно разрешает активацию,
    /// потому что в popover есть ползунок и другие элементы управления.
    /// </summary>
    private void OnStatusPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        OverlayWindow.SetNonActivating(_hwnd, false);
        Activate();
        ViewModel.ToggleQuickSettings();
    }

    /// <summary>
    /// Клавиатурная активация правого pill (доступность, I5): то же действие, что и по клику.
    /// Pill — обычная рамка, поэтому Enter и пробел обрабатываются вручную.
    /// </summary>
    private void OnStatusKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is not (VirtualKey.Enter or VirtualKey.Space))
        {
            return;
        }

        e.Handled = true;
        OverlayWindow.SetNonActivating(_hwnd, false);
        Activate();
        ViewModel.ToggleQuickSettings();
    }

    private void OnToggleMute(object sender, RoutedEventArgs e) => ViewModel.QuickSettings.ToggleMute();

    private void OnOpenNetworkSettings(object sender, RoutedEventArgs e) => ShellTargets.Open("ms-settings:network");

    private void OnOpenBluetoothSettings(object sender, RoutedEventArgs e) => ShellTargets.Open("ms-settings:bluetooth");

    private void OnOpenSoundSettings(object sender, RoutedEventArgs e) => ShellTargets.Open("ms-settings:sound");

    /// <summary>Клавиатура Glance: стрелки двигают выбор, Enter открывает, Escape очищает.</summary>
    private async void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Enter:
                e.Handled = true;
                await ViewModel.ActivateSelectedAsync();
                break;

            case VirtualKey.Escape:
                e.Handled = true;
                ViewModel.ClearSearch();
                break;

            case VirtualKey.Down:
                e.Handled = true;
                ViewModel.MoveSelection(1);
                break;

            case VirtualKey.Up:
                e.Handled = true;
                ViewModel.MoveSelection(-1);
                break;

            default:
                break;
        }
    }

    private async void OnResultClicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is GlanceResultViewModel result)
        {
            ViewModel.SelectedResult = result;
        }

        await ViewModel.ActivateSelectedAsync();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TopBarViewModel.HasResults))
        {
            UpdateOverlayBounds();
        }
    }

    /// <summary>
    /// Размер окна и регион зависят от видимости «Центра управления»: при открытии popover
    /// окно растёт по высоте, при закрытии возвращается к строке pill.
    /// </summary>
    private void OnQuickSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(QuickSettingsViewModel.IsOpen))
        {
            UpdateOverlayBounds();
        }
    }

    /// <summary>
    /// Команда поиска <c>open-shell</c> просит показать главное окно: панель им не владеет,
    /// поэтому переключение выполняет владелец.
    /// </summary>
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
            Trace.WriteLine($"GlazeShell.TopBar: activating the main window failed. {exception}");
        }
    }

    /// <summary>
    /// Команда поиска <c>open-explorer</c> просит открыть проводник Glaze Shell: панель
    /// окном не владеет, поэтому запрос пересылается приложению.
    /// </summary>
    private void OnExplorerRequested(object? sender, string? path) => ExplorerRequested?.Invoke(this, path);

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState != WindowActivationState.Deactivated)
        {
            return;
        }

        OverlayWindow.SetNonActivating(_hwnd, true);

        // Клик по рабочему столу или другому приложению — это «light dismiss»: поиск
        // и «Центр управления» закрываются вместе со своими панелями.
        ViewModel.CloseQuickSettings();
        ViewModel.ClearSearch();
    }

    /// <summary>
    /// Переприменяет тему и обновляет фон окна. В режиме высокой контрастности стекло
    /// отключается: полупрозрачные pill поверх рабочего стола нечитаемы, а непрозрачный фон
    /// берётся из системной палитры.
    /// </summary>
    private void OnSettingsChanged(SettingsChanged changed)
    {
        _themes.ApplyFromSettings(Root);
        ApplyBackdrop();
    }

    /// <summary>
    /// Реакция на изменение конфигурации мониторов: верхняя панель переезжает на целевой
    /// монитор вместе с остальной оболочкой, без перезапуска (функция I6).
    /// </summary>
    private void OnDisplayChanged(DisplayChanged changed)
    {
        _monitorTarget.Update(changed.Monitors);

        _ = DispatcherQueue.TryEnqueue(UpdateOverlayBounds);
    }

    private void ApplyBackdrop() =>
        SystemBackdrop = _themes.HighContrast ? null : new DesktopAcrylicBackdrop();
}
