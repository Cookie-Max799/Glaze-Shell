using System.ComponentModel;
using System.Diagnostics;
using GlazeShell.App.Presentation;
using GlazeShell.App.Theming;
using GlazeShell.Core.Display;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Windows.WindowManagement;
using Microsoft.UI.Dispatching;
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
/// Плавающая верхняя панель (A6): topmost-окно без записи в панели задач во всю ширину
/// экрана. В покое чуть выезжает из-за верхнего края, при наведении раскрывается полностью.
/// Показывается без перехвата фокуса, по <c>Ctrl K</c> разрешает активацию и ставит фокус
/// в строку поиска Glance (B1).
/// </summary>
public sealed partial class TopBarWindow : Window
{
    private const int BarHeight = 30;

    /// <summary>Сколько пикселей полосы видно над верхним краем экрана в свёрнутом виде.</summary>
    private const int PeekHeight = 22;

    /// <summary>
    /// Высота «горячей зоны» у кромки экрана, когда работает чужая программа: панель
    /// полностью скрыта и выезжает только от касания кромки, чтобы не перекрывать
    /// заголовки окон и вкладки.
    /// </summary>
    private const int ForeignHotZone = 2;

    /// <summary>Радиус нижних углов полосы.</summary>
    private const int BarRadius = 24;

    /// <summary>Ширина popover «Центр управления».</summary>
    private const int QuickSettingsWidth = 380;

    /// <summary>Ширина popover часов и календаря.</summary>
    private const int CalendarWidth = 380;

    /// <summary>Отступ popover от верхней кромки: столько места занимает полоса панели.</summary>
    private const int PopoverTopOffset = 56;

    /// <summary>Радиус всплывающих панелей.</summary>
    private const int PanelRadius = 32;

    /// <summary>Задержка сворачивания после ухода курсора.</summary>
    private static readonly TimeSpan CollapseDelay = TimeSpan.FromMilliseconds(350);

    private readonly Window _owner;
    private readonly ThemeApplicator _themes;
    private readonly IDisposable _settingsChanged;
    private readonly IDisposable _displayChanged;
    private readonly ShellMonitorTarget _monitorTarget;
    private readonly DispatcherTimer _revealWatch = new();
    private nint _hwnd;

    /// <summary>Последнее применённое положение окна: панель переносится только при изменении.</summary>
    private RectInt32 _placement;
    private int _screenLeft;
    private int _screenTop;
    private int _screenWidth;
    private int _windowHeightPx;
    private double _scale = 1.0;
    private double _reveal;
    private double _lastAppliedReveal = -1;
    private DateTimeOffset _collapseAt = DateTimeOffset.UtcNow + CollapseDelay;
    private readonly int _ownProcessId = Environment.ProcessId;
    private nint _foregroundHwnd;
    private bool _foreignForeground;

    /// <summary>Окно сейчас активируемо и может держать клавиатуру (поиск или popover).</summary>
    private bool _inputEnabled;

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

        // x:Bind в окне обновляются по событию Activated, а панель показывается без
        // активации (WS_EX_NOACTIVATE), поэтому Activated не приходит и привязки
        // оставались бы пустыми: без часов, индикаторов нагрузки и иконок.
        Bindings?.Update();

        _themes.ApplyFromSettings(Root);

        ConfigureOverlay();

        // Регион и высота окна зависят от разметки: сначала применяем после загрузки, а
        // затем при изменении размеров.
        Root.Loaded += (_, _) =>
        {
            UpdateOverlayBounds();
            StartRevealWatch();
            UpdateForegroundState();
        };
        AppPill.SizeChanged += (_, _) => UpdateOverlayBounds();
        if (SearchHit is not null)
        {
            SearchHit.SizeChanged += (_, _) => UpdateOverlayBounds();
        }
        StatusPill.SizeChanged += (_, _) => UpdateOverlayBounds();
        ResultsPanel.SizeChanged += (_, _) => UpdateOverlayBounds();
        QuickSettingsPanel.SizeChanged += (_, _) => UpdateOverlayBounds();
        CalendarPanel.SizeChanged += (_, _) => UpdateOverlayBounds();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.QuickSettings.PropertyChanged += OnQuickSettingsPropertyChanged;
        ViewModel.Calendar.PropertyChanged += OnCalendarPropertyChanged;
        ViewModel.ActivateRequested += OnActivateRequested;
        ViewModel.ExplorerRequested += OnExplorerRequested;
        _settingsChanged = events.Subscribe<SettingsChanged>(OnSettingsChanged);
        _displayChanged = events.Subscribe<DisplayChanged>(OnDisplayChanged);
        Activated += OnActivated;

        Closed += (_, _) =>
        {
            _revealWatch.Stop();
            _revealWatch.Tick -= OnRevealWatchTick;
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            ViewModel.QuickSettings.PropertyChanged -= OnQuickSettingsPropertyChanged;
            ViewModel.Calendar.PropertyChanged -= OnCalendarPropertyChanged;
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
            FocusSearchCore(clearText: true);
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() => FocusSearchCore(clearText: true));
    }

    private void FocusSearchCore(bool clearText)
    {
        if (_hwnd == 0)
        {
            return;
        }

        ViewModel.CloseQuickSettings();
        ViewModel.CloseCalendar();

        if (clearText)
        {
            ViewModel.ClearSearch();
        }

        RevealImmediately();

        // Окно показано без активации, а система запрещает фоновому процессу менять
        // активное окно: просто Activate() мигал бы кнопкой на панели задач, и поле
        // оставалось бы без клавиатуры. EnableInput присоединяет очереди ввода.
        EnableInput();

        // Фокус ставится после того, как окно стало активным: сразу после вызова он уходил
        // бы в ещё не готовое поле и курсор не появлялся.
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            UpdateOverlayBounds();

            if (SearchBox is not null)
            {
                SearchBox.Focus(FocusState.Programmatic);
                SearchBox.Select(SearchBox.Text.Length, 0);
            }
        });
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

        UpdateOverlayBounds();
        OverlayWindow.ShowWithoutActivation(_hwnd);
        StartRevealWatch();
    }

    /// <summary>
    /// Раскладывает окно по целевому монитору во всю его ширину и увеличивает по высоте
    /// под панель результатов Glance или popover. В свёрнутом виде окно поднято вверх так,
    /// что над краем экрана остаётся только <see cref="PeekHeight"/> пикселей полосы.
    /// Рабочая область не резервируется.
    /// </summary>
    private void UpdateOverlayBounds()
    {
        if (_hwnd == 0)
        {
            return;
        }

        var target = _monitorTarget.Target;
        var scale = target?.ScaleFactor ?? OverlayWindow.GetScaleFactor(_hwnd);
        _scale = scale <= 0 ? 1.0 : scale;

        // Значок просится в пикселях отображения: при масштабе 125 % и выше 16 DIP — это
        // больше 16 пикселей, и значок остаётся резким.
        ViewModel.SetIconScale(_scale);

        var logicalHeight = (double)BarHeight;

        if (ViewModel.HasResults)
        {
            const int constraint = 440;
            logicalHeight += 6;
            logicalHeight += MeasurePanelHeight(ResultsPanel, constraint);
        }
        else if (ViewModel.QuickSettings.IsOpen)
        {
            logicalHeight = PopoverTopOffset + MeasurePanelHeight(QuickSettingsPanel, QuickSettingsWidth);
        }
        else if (ViewModel.Calendar.IsOpen)
        {
            logicalHeight = PopoverTopOffset + MeasurePanelHeight(CalendarPanel, CalendarWidth);
        }

        _windowHeightPx = Math.Max(1, (int)Math.Round(logicalHeight * _scale));

        if (target is not null)
        {
            _screenLeft = target.Bounds.X;
            _screenTop = target.Bounds.Y;
            _screenWidth = Math.Max(1, target.Bounds.Width);
        }
        else
        {
            var bounds = DisplayArea.Primary.OuterBounds;
            _screenLeft = bounds.X;
            _screenTop = bounds.Y;
            _screenWidth = Math.Max(1, bounds.Width);
        }

        ApplyPlacement();
        UpdateWindowRegion();
    }

    /// <summary>Применяет текущий прогресс выезда к положению окна.</summary>
    private void ApplyPlacement()
    {
        if (_hwnd == 0 || _screenWidth <= 0)
        {
            return;
        }

        // В покое над краем экрана остаётся кромка панели; когда работает чужая программа,
        // панель убирается полностью и появляется только под курсором у кромки.
        var peek = _foreignForeground ? 0 : PeekHeight;
        var hidden = (int)Math.Round((BarHeight - peek) * (1.0 - _reveal) * _scale);
        var placement = new RectInt32(_screenLeft, _screenTop - hidden, _screenWidth, _windowHeightPx);

        if (placement != _placement)
        {
            AppWindow.MoveAndResize(placement);
            _placement = placement;
        }
    }

    /// <summary>Запускает таймер слежения за курсором.</summary>
    private void StartRevealWatch()
    {
        if (_revealWatch.IsEnabled)
        {
            return;
        }

        _revealWatch.Interval = TimeSpan.FromMilliseconds(16);
        _revealWatch.Tick -= OnRevealWatchTick;
        _revealWatch.Tick += OnRevealWatchTick;
        _revealWatch.Start();
    }

    /// <summary>
    /// Выезд и автосворачивание панели. Пока в фокусе чужая программа, панель полностью
    /// убирается за верхний край и выезжает только под курсор, оказавшийся у кромки экрана.
    /// </summary>
    private void OnRevealWatchTick(object? sender, object e)
    {
        if (_hwnd == 0)
        {
            return;
        }

        UpdateForegroundState();

        // Окно в режиме без активации не получает нажатий в элементы, поэтому клик
        // по строке поиска распознаётся по глобальному состоянию кнопки мыши.
        if (!_inputEnabled && _reveal > 0.5 && OverlayWindow.IsLeftButtonDown() && IsCursorOverSearchBox())
        {
            FocusSearchCore(clearText: false);
        }

        var now = DateTimeOffset.UtcNow;
        var pinned = IsPinnedOpen();
        var target = 0.0;

        if (pinned || IsCursorOverHotZone())
        {
            _collapseAt = now + CollapseDelay;
            target = 1.0;
        }
        else if (now < _collapseAt)
        {
            target = 1.0;
        }

        if (_themes.ReduceMotion)
        {
            _reveal = target;
        }
        else
        {
            _reveal += (target - _reveal) * 0.28;

            if (Math.Abs(target - _reveal) < 0.002)
            {
                _reveal = target;
            }
        }

        if (Math.Abs(_reveal - _lastAppliedReveal) > 0.0005)
        {
            _lastAppliedReveal = _reveal;
            ApplyPlacement();
        }

        // Полностью убранная панель не должна удерживать клавиатуру: иначе после ухода
        // в чужое приложение фокус оставался бы у скрытого окна.
        if (_reveal <= 0 && _inputEnabled && !IsPinnedOpen())
        {
            if (SearchBox is { FocusState: not FocusState.Unfocused })
            {
                _ = SearchBox.Focus(FocusState.Unfocused);
            }

            _inputEnabled = false;
            OverlayWindow.SetNonActivating(_hwnd, true);
        }
    }

    /// <summary>
    /// Следит за активным окном. Трекер окон пропускает собственный процесс, поэтому
    /// возврат в Glaze Shell по событиям не виден: смена активного окна читается напрямую.
    /// Сравнение дешёвое (один вызов Win32), полное чтение окна выполняется только при смене.
    /// </summary>
    private void UpdateForegroundState()
    {
        if (!OverlayWindow.TryGetForegroundWindow(out var hwnd, out var processId) || hwnd == _foregroundHwnd)
        {
            return;
        }

        _foregroundHwnd = hwnd;
        ViewModel.RefreshForeground();

        var foreign = processId != _ownProcessId;

        if (foreign == _foreignForeground)
        {
            return;
        }

        _foreignForeground = foreign;

        // Уход в другое приложение убирает панель сразу, без 350 мс ожидания.
        if (foreign)
        {
            _collapseAt = DateTimeOffset.UtcNow;
        }

        // Высота «кромки» в покое зависит от режима, поэтому положение пересчитывается,
        // даже если прогресс выезда не менялся.
        _lastAppliedReveal = -1;
    }

    /// <summary>
    /// Курсор в «горячей зоне» у верхней кромки экрана. Зона считается в экранных координатах
    /// и от состояния выезда не зависит: полностью скрытая панель иначе не могла бы
    /// вызваться курсором. В чужом приложении зона узкая — одно прикосновение к кромке,
    /// чтобы панель не всплывала над строкой вкладок браузера.
    /// </summary>
    private bool IsCursorOverHotZone()
    {
        if (_screenWidth <= 0 ||
            !OverlayWindow.TryGetCursorScreenPosition(out var x, out var y))
        {
            return false;
        }

        // Скрытая в чужом приложении панель ловится узкой полосой у кромки; как только она
        // выехала, зоной становится вся полоса — иначе клик по поиску не успевал бы
        // удержать панель открытой.
        var zone = !_foreignForeground || _reveal >= 0.5 ? BarHeight : ForeignHotZone;
        var zoneHeight = Math.Max(1, (int)Math.Round(zone * _scale));
        return x >= _screenLeft && x < _screenLeft + _screenWidth && y >= _screenTop && y < _screenTop + zoneHeight;
    }

    /// <summary>
    /// Курсор над строкой поиска. Границы берутся из разметки, поэтому клик срабатывает
    /// ровно по видимому полю, а не по полосе панели целиком.
    /// </summary>
    private bool IsCursorOverSearchBox()
    {
        if (SearchBox is null ||
            SearchBox.ActualWidth <= 0 ||
            SearchBox.ActualHeight <= 0 ||
            !OverlayWindow.TryGetCursorPosition(_hwnd, out var x, out var y))
        {
            return false;
        }

        var bounds = SearchBox
            .TransformToVisual(null)
            .TransformBounds(new Rect(0, 0, SearchBox.ActualWidth, SearchBox.ActualHeight));

        var left = (int)Math.Round(bounds.X * _scale);
        var top = (int)Math.Round(bounds.Y * _scale);
        var right = (int)Math.Round((bounds.X + bounds.Width) * _scale);
        var bottom = (int)Math.Round((bounds.Y + bounds.Height) * _scale);

        return x >= left && x < right && y >= top && y < bottom;
    }

    /// <summary>
    /// Поиск или открытый popover удерживают панель раскрытой. Пока у окна есть ввод,
    /// панель тоже не сворачивается: иначе фокус в пустом поле поиска сразу терялся бы.
    /// </summary>
    private bool IsPinnedOpen() =>
        _inputEnabled ||
        ViewModel.HasQuery ||
        ViewModel.HasResults ||
        ViewModel.QuickSettings.IsOpen ||
        ViewModel.Calendar.IsOpen;

    /// <summary>Мгновенно раскрывает панель.</summary>
    private void RevealImmediately()
    {
        _reveal = 1;
        _lastAppliedReveal = 1;
        _collapseAt = DateTimeOffset.UtcNow + CollapseDelay;
        ApplyPlacement();
    }

    private void HoldRevealed() => _collapseAt = DateTimeOffset.UtcNow + CollapseDelay;

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
    /// Вырезает из окна полосу во всю ширину со скруглённым низом и всплывающие панели.
    /// Окно панели шире и выше её содержимого, поэтому без этой маски раскрытые результаты
    /// поиска и popover выглядели бы прямоугольником во весь экран с фоном Mica.
    /// </summary>
    private void UpdateWindowRegion()
    {
        if (_hwnd == 0 || !OverlayWindow.TryGetClientSize(_hwnd, out var clientWidth, out _))
        {
            return;
        }

        var scale = _scale <= 0 ? 1.0 : _scale;
        var barHeight = Math.Max(1, (int)Math.Round(BarHeight * scale));
        var barRadius = (int)Math.Round(BarRadius * scale);
        var panelRadius = (int)Math.Round(PanelRadius * scale);
        var regions = new List<PixelRegion>(3);

        if (ViewModel.HasResults)
        {
            AddRegion(ResultsPanel, panelRadius);
        }

        if (ViewModel.QuickSettings.IsOpen)
        {
            AddRegion(QuickSettingsPanel, panelRadius);
        }

        if (ViewModel.Calendar.IsOpen)
        {
            AddRegion(CalendarPanel, panelRadius);
        }

        OverlayWindow.ApplyBottomRoundedRegions(
            _hwnd,
            new PixelRect(-1, 0, clientWidth + 2, barHeight),
            barRadius,
            regions);

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
        // Клик не должен стирать уже набранный запрос: очистка — только по Ctrl K.
        if (DispatcherQueue.HasThreadAccess)
        {
            FocusSearchCore(clearText: false);
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() => FocusSearchCore(clearText: false));
    }

    /// <summary>
    /// Поиск в фокусе: панель удерживается раскрытой и не сворачивается, пока пользователь
    /// печатает, даже если курсор ушёл с полосы.
    /// </summary>
    private void OnSearchGotFocus(object sender, RoutedEventArgs e) => HoldRevealed();

    private void OnSearchLostFocus(object sender, RoutedEventArgs e)
    {
        // Режим без активации возвращается, когда клавиатура панели больше не нужна:
        // иначе после закрытия поиска окно продолжало бы забирать фокус у приложения.
        _ = DispatcherQueue.TryEnqueue(RestoreInput);

        if (!string.IsNullOrWhiteSpace(ViewModel.SearchText))
        {
            return;
        }

        ViewModel.ClearSearch();
        _collapseAt = DateTimeOffset.UtcNow + CollapseDelay;
    }

    /// <summary>
    /// Включает ввод: снимает <c>WS_EX_NOACTIVATE</c>, выводит окно на передний план и
    /// передаёт ему клавиатуру. Окно показано без активации, а система запрещает фоновому
    /// процессу менять активное окно — поэтому одного <c>Activate()</c> недостаточно, и
    /// <see cref="OverlayWindow.ActivateForInput"/> присоединяет очереди ввода.
    /// </summary>
    private void EnableInput()
    {
        if (_hwnd == 0)
        {
            return;
        }

        _inputEnabled = true;
        OverlayWindow.SetNonActivating(_hwnd, false);
        _ = OverlayWindow.ActivateForInput(_hwnd);
    }

    /// <summary>
    /// Возвращает окну режим без активации, если ввод больше не используется: строка поиска
    /// не в фокусе и ни один popover не открыт. Иначе панель оставалась бы активным окном
    /// поверх чужой программы и забирала бы у неё клавиатуру.
    /// </summary>
    private void RestoreInput()
    {
        if (_hwnd == 0 ||
            ViewModel.QuickSettings.IsOpen ||
            ViewModel.Calendar.IsOpen ||
            SearchBox is { FocusState: not FocusState.Unfocused })
        {
            return;
        }

        _inputEnabled = false;
        OverlayWindow.SetNonActivating(_hwnd, true);
    }

    /// <summary>
    /// Клик по правому pill открывает «Центр управления» (D1): окно разрешает активацию,
    /// потому что в popover есть ползунок и другие элементы управления.
    /// </summary>
    private void OnStatusPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // Клик по часам открывает календарь (D4), а не «Центр управления».
        if (IsWithin(e.OriginalSource as DependencyObject, ClockButton))
        {
            return;
        }

        EnableInput();
        ViewModel.ClearSearch();
        ViewModel.ToggleQuickSettings();
        HoldRevealed();
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

        // Фокус на часах: Enter/Space открывает календарь, а не «Центр управления».
        if (IsWithin(e.OriginalSource as DependencyObject, ClockButton))
        {
            return;
        }

        e.Handled = true;
        EnableInput();
        ViewModel.ToggleQuickSettings();
        HoldRevealed();
    }

    /// <summary>
    /// Клик по часам открывает или закрывает popover календаря (D4). Окно разрешает
    /// активацию, потому что в popover есть кнопки перелистывания и перехода к сегодня.
    /// </summary>
    private void OnClockClick(object sender, RoutedEventArgs e)
    {
        EnableInput();
        ViewModel.ClearSearch();
        ViewModel.ToggleCalendar();
        HoldRevealed();
    }

    private void OnCalendarPreviousMonth(object sender, RoutedEventArgs e) => ViewModel.Calendar.PreviousMonth();

    private void OnCalendarNextMonth(object sender, RoutedEventArgs e) => ViewModel.Calendar.NextMonth();

    private void OnCalendarToday(object sender, RoutedEventArgs e) => ViewModel.Calendar.GoToToday();

    /// <summary>Проверяет, находится ли элемент внутри контейнера (учёт вложенных текстов и иконок).</summary>
    private static bool IsWithin(DependencyObject? source, DependencyObject container)
    {
        while (source is not null)
        {
            if (ReferenceEquals(source, container))
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
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
        if (e.PropertyName is nameof(TopBarViewModel.HasResults) or nameof(TopBarViewModel.HasQuery))
        {
            if (ViewModel.HasResults || ViewModel.HasQuery)
            {
                HoldRevealed();
                _ = DispatcherQueue.TryEnqueue(() => UpdateOverlayBounds());
            }
            else
            {
                _ = DispatcherQueue.TryEnqueue(() => UpdateOverlayBounds());
            }
        }
    }

    /// <summary>
    /// Размер окна и регион зависят от видимости «Центра управления».
    /// </summary>
    private void OnQuickSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(QuickSettingsViewModel.IsOpen))
        {
            if (ViewModel.QuickSettings.IsOpen)
            {
                HoldRevealed();
            }
            else
            {
                _ = DispatcherQueue.TryEnqueue(RestoreInput);
            }

            UpdateOverlayBounds();
        }
    }

    /// <summary>
    /// Размер окна и регион зависят от видимости календаря.
    /// </summary>
    private void OnCalendarPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CalendarViewModel.IsOpen))
        {
            if (ViewModel.Calendar.IsOpen)
            {
                HoldRevealed();
            }
            else
            {
                _ = DispatcherQueue.TryEnqueue(RestoreInput);
            }

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

        // Клик по рабочему столу или другому приложению — это «light dismiss»: поиск,
        // «Центр управления» и календарь закрываются вместе со своими панелями.
        ViewModel.CloseQuickSettings();
        ViewModel.CloseCalendar();
        ViewModel.ClearSearch();

        _collapseAt = DateTimeOffset.UtcNow + CollapseDelay;
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
        _ = DispatcherQueue.TryEnqueue(() => UpdateOverlayBounds());
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
        SystemBackdrop = _themes.HighContrast ? null : new Microsoft.UI.Xaml.Media.MicaBackdrop();
}
