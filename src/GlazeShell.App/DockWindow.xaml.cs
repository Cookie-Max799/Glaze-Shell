using System.Collections.Specialized;
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
    private const int PaddingTop = 20;
    private const int PaddingBottom = 8;

    /// <summary>Радиус капсулы dock (DESIGN §7.5: радиус 22).</summary>
    private const int PillRadius = 22;

    /// <summary>
    /// Максимальный подъём и масштаб плитки под курсором (DESIGN §7.5: 12 px / 1.2).
    /// Верхний отступ окна (20 px) подобран так, чтобы увеличенная плитка не выходила
    /// за границы капсулы: 8 px прироста (плитка 40 → 48 растёт от нижней кромки) плюс
    /// 12 px подъёма = 20 px.
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

    /// <summary>Сколько dock остаётся раскрытым после нажатия клавиши Windows.</summary>
    private static readonly TimeSpan RevealHold = TimeSpan.FromMilliseconds(1500);

    /// <summary>
    /// Сколько панель ещё держится после того, как курсор вышел из её контура. Короткая пауза
    /// нужна только чтобы док не мигал, когда курсор проскакивает кромку насквозь; раньше
    /// здесь было трёхсекундное ожидание, и панель висела открытой без причины.
    /// </summary>
    private static readonly TimeSpan CursorExitDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Длительность выезда и ухода. Обе короткие и предсказуемые: экспоненциальное
    /// «догоняние» цели растягивало выезд на полсекунды и заканчивалось микрошагами,
    /// которые читались как дрожание.
    /// </summary>
    private static readonly TimeSpan RevealOpenDuration = TimeSpan.FromMilliseconds(260);
    private static readonly TimeSpan RevealCloseDuration = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Высота «горячей зоны» у нижней кромки экрана: в чужом приложении dock полностью
    /// скрыт и вызывается касанием кромки или клавишей Windows.
    /// </summary>
    private const int BottomHotZone = 2;

    /// <summary>Порог, ниже которого пересчёт визуала пропускается (меньше пиксельного шума).</summary>
    private const double MagnifyScaleEpsilon = 0.002;
    private const double MagnifyLiftEpsilon = 0.05;

    private readonly Window _owner;
    private readonly ThemeApplicator _themes;
    private readonly IDisposable _settingsChanged;
    private readonly IDisposable _displayChanged;
    private readonly ShellMonitorTarget _monitorTarget;

    // Магнификация не может опираться только на PointerMoved: окно показано без активации
    // (WS_EX_NOACTIVATE), и WinUI не всегда доставляет ему события ввода. Поэтому позиция
    // курсора читается глобально таймером и переводится в координаты ленты плиток.
    private readonly DispatcherTimer _cursorWatch = new() { Interval = TimeSpan.FromMilliseconds(33) };

    private nint _hwnd;
    private double _windowLeft;
    private double _windowTop;
    private bool _cursorInside;
    private bool _cursorWatchRunning;

    private readonly int _ownProcessId = Environment.ProcessId;

    /// <summary>Активное окно и признак «чужая программа» — от него зависит видимость док-панели.</summary>
    private nint _foregroundHwnd;
    private bool _foreignForeground;

    /// <summary>Активно окно оболочки Windows (рабочий стол, «Пуск», поиск, уведомления).</summary>
    private bool _shellForeground;

    /// <summary>Клавиша Windows была нажата в прошлом кадре: нужен фронт нажатия.</summary>
    private bool _windowsKeyDown;

    /// <summary>До какого момента dock остаётся раскрытым по клавише Windows.</summary>
    private DateTimeOffset _revealUntil = DateTimeOffset.MinValue;

    /// <summary>До какого момента панель ещё держится после ухода курсора из контура.</summary>
    private DateTimeOffset _cursorExitUntil = DateTimeOffset.MinValue;

    /// <summary>Прогресс выезда: 1 — панель у нижнего края, 0 — полностью за ним.</summary>
    private double _reveal = 1.0;

    /// <summary>Куда движется панель: цель берётся из состояния, а шаг считает анимация.</summary>
    private double _revealTarget = 1.0;

    /// <summary>Откуда начался текущий выезд/уход и когда он стартовал.</summary>
    private double _revealFrom = 1.0;
    private DateTimeOffset _revealStepStart = DateTimeOffset.UtcNow;

    /// <summary>
    /// Отдельный таймер собственно движения панели (≈60 кадров/с). Он работает только пока
    /// выезд или уход не завершён, поэтому в покое dock не будит поток интерфейса лишний раз.
    /// </summary>
    private readonly DispatcherTimer _revealWatch = new() { Interval = TimeSpan.FromMilliseconds(16) };

    private bool _revealWatchRunning;

    private int _dockLeft;
    private int _dockTop;
    private int _dockWidth;
    private int _dockHeight;
    private RectInt32 _appliedPlacement;

    /// <summary>Состав плиток на прошлом проходе: по нему видно, какие плитки появились.</summary>
    private readonly HashSet<string> _knownKeys = new(StringComparer.Ordinal);

    /// <summary>Плитка, у которой проигрывается анимация появления, чтобы не запускать её дважды.</summary>
    private readonly HashSet<string> _enteringKeys = new(StringComparer.Ordinal);

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
        IMonitorManager monitors,
        ThemeApplicator themes,
        Window owner)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(monitors);

        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _themes = themes ?? throw new ArgumentNullException(nameof(themes));
        _monitorTarget = new ShellMonitorTarget(monitors);

        ViewModel = new DockViewModel(applications, launcher, windows, settings, events, DispatcherQueue);

        InitializeComponent();
        Title = "Glaze Shell — Dock";

        // Панель показывается без активации, поэтому Activated не приходит и x:Bind
        // не обновлялись бы: плитки приложений оставались бы пустыми.
        Bindings?.Update();

        _themes.ApplyFromSettings(Root);

        ViewModel.ActivateRequested += OnActivateRequested;
        ViewModel.Items.CollectionChanged += OnItemsChanged;
        Tiles.SizeChanged += OnTilesSizeChanged;
        _settingsChanged = events.Subscribe<SettingsChanged>(OnSettingsChanged);
        _displayChanged = events.Subscribe<DisplayChanged>(OnDisplayChanged);

        _cursorWatch.Tick += OnCursorWatchTick;
        _revealWatch.Tick += OnRevealWatchTick;

        ConfigureOverlay();

        Closed += (_, _) =>
        {
            _cursorWatch.Stop();
            _cursorWatch.Tick -= OnCursorWatchTick;
            _revealWatch.Stop();
            _revealWatch.Tick -= OnRevealWatchTick;
            ViewModel.ActivateRequested -= OnActivateRequested;
            ViewModel.Items.CollectionChanged -= OnItemsChanged;
            Tiles.SizeChanged -= OnTilesSizeChanged;
            _settingsChanged.Dispose();
            _displayChanged.Dispose();
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
    /// Ставит dock по центру целевого монитора вплотную к нижнему краю экрана: панель не
    /// парит над краем, а растворяется в нём — нижние углы прямые, скруглены только верхние
    /// (см. <see cref="OverlayWindow.ApplyTopRoundedRegion"/>). Рабочая область не
    /// резервируется, поэтому dock не смещает окна. Ширина считается по составу плиток:
    /// окно не должно быть шире содержимого, иначе стекло вышло бы за «pill».
    /// </summary>
    /// <remarks>
    /// Положение считается по целевому монитору (функция I6): при переносе dock на другой
    /// монитор учитываются его собственные границы и масштаб, поэтому панель одинаково
    /// выглядит при смешанном масштабировании.
    /// </remarks>
    private const int MinDockWidthDip = 560;

    private void UpdateDockSize()
    {
        if (_hwnd == 0)
        {
            return;
        }

        var target = _monitorTarget.Target;
        var scale = target?.ScaleFactor ?? OverlayWindow.GetScaleFactor(_hwnd);

        // Значки просятся в пикселях отображения: при 125 % плитка 40 DIP с увеличением
        // до 1.2 требует 60 пикселей, иначе значок растягивается и мылится.
        ViewModel.SetIconScale(scale);

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
        var widthDip = Math.Max(MinDockWidthDip, content + (PaddingHorizontal * 2));
        var heightDip = TileSize + PaddingTop + PaddingBottom;

        var width = (int)Math.Round(widthDip * scale);
        var height = (int)Math.Round(heightDip * scale);

        int left;
        int top;

        if (target is not null)
        {
            var placement = ShellPlacementResolver.Resolve(target, ShellAnchor.Bottom, ShellAlignment.Center, widthDip, heightDip);
            left = placement.X;
            top = placement.Y;
        }
        else
        {
            // Мониторы неизвестны: dock остаётся на основном экране, как и раньше.
            var bounds = DisplayArea.Primary.OuterBounds;
            left = bounds.X + ((bounds.Width - width) / 2);
            top = bounds.Y + bounds.Height - height;
        }

        _dockLeft = left;
        _dockTop = top;
        _dockWidth = width;
        _dockHeight = height;

        ApplyDockPlacement();

        _windowLeft = left;
        _windowTop = _appliedPlacement.Y;

        if (!_cursorWatchRunning)
        {
            _cursorWatchRunning = true;
            _cursorWatch.Start();
        }

        // Капсула: окно обрезается прямоугольником со скруглённым верхом, поэтому
        // acrylic-backdrop заполняет только саму панель, а не прямоугольник вокруг неё.
        // Нижний край остаётся прямым — панель прижата к краю экрана.
        OverlayWindow.ApplyTopRoundedRegion(
            _hwnd,
            new PixelRect(0, 0, width, height),
            (int)Math.Round(PillRadius * scale));
    }

    /// <summary>
    /// Применяет прогресс выезда: при <see cref="_reveal"/> = 1 панель стоит вплотную
    /// к нижнему краю, при 0 — целиком уходит за него. Положение сравнивается с прошлым,
    /// поэтому таймер курсора не дёргает окно на каждом кадре.
    /// </summary>
    private void ApplyDockPlacement()
    {
        if (_hwnd == 0 || _dockWidth <= 0 || _dockHeight <= 0)
        {
            return;
        }

        var offset = (int)Math.Round(_dockHeight * (1.0 - _reveal));
        var placement = new RectInt32(_dockLeft, _dockTop + offset, _dockWidth, _dockHeight);

        if (placement == _appliedPlacement)
        {
            return;
        }

        AppWindow.MoveAndResize(placement);
        _appliedPlacement = placement;
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _centersDirty = true;
        _glowIndex = -1;
        UpdateDockSize();

        // Появление плитки — отдельная анимация: она проигрывается после раскладки, когда
        // у контейнера уже есть размер, иначе стартовый масштаб применился бы к нулевой
        // геометрии и эффект не был бы виден.
        _ = DispatcherQueue.TryEnqueue(AnimateNewTiles);
    }

    /// <summary>
    /// Показывает плитки, которых не было на прошлом проходе: новая плитка «выпрыгивает»
    /// из-под панели с лёгким перелётом, как это делает dock macOS при запуске приложения.
    /// </summary>
    private void AnimateNewTiles()
    {
        var present = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < ViewModel.Items.Count; index++)
        {
            var item = ViewModel.Items[index];

            if (item.IsSeparator)
            {
                continue;
            }

            present.Add(item.Key);

            if (_knownKeys.Contains(item.Key) || !_enteringKeys.Add(item.Key))
            {
                continue;
            }

            if (TileAt(index) is { } tile)
            {
                PlayEntrance(tile, item.Key);
            }
        }

        _knownKeys.Clear();
        _knownKeys.UnionWith(present);
    }

    /// <summary>
    /// Анимация появления плитки: масштаб от 0.4 до 1 и подъём снизу. По завершении плитка
    /// возвращается к обычному состоянию, чтобы магнификация работала с чистого листа.
    /// </summary>
    private void PlayEntrance(Border tile, string key)
    {
        var (scaleTransform, translateTransform) = EnsureTransforms(tile);

        // Доступность (I5): при «уменьшить движение» плитка просто появляется.
        if (_themes.ReduceMotion)
        {
            scaleTransform.ScaleX = 1;
            scaleTransform.ScaleY = 1;
            translateTransform.Y = 0;
            _enteringKeys.Remove(key);
            return;
        }

        var duration = TimeSpan.FromMilliseconds(260);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        var storyboard = new Storyboard();

        foreach (var property in new[] { nameof(ScaleTransform.ScaleX), nameof(ScaleTransform.ScaleY) })
        {
            var animation = new DoubleAnimation
            {
                From = 0.4,
                To = 1,
                Duration = duration,
                EasingFunction = easing,
            };

            Storyboard.SetTarget(animation, scaleTransform);
            Storyboard.SetTargetProperty(animation, property);
            storyboard.Children.Add(animation);
        }

        var lift = new DoubleAnimation
        {
            From = -18,
            To = 0,
            Duration = duration,
            EasingFunction = easing,
        };

        Storyboard.SetTarget(lift, translateTransform);
        Storyboard.SetTargetProperty(lift, nameof(TranslateTransform.Y));
        storyboard.Children.Add(lift);

        storyboard.Completed += (_, _) =>
        {
            scaleTransform.ScaleX = 1;
            scaleTransform.ScaleY = 1;
            translateTransform.Y = 0;
            _enteringKeys.Remove(key);
            _centersDirty = true;
        };

        storyboard.Begin();
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
    private void OnSettingsChanged(SettingsChanged changed) => _ = DispatcherQueue.TryEnqueue(() =>
    {
        _themes.ApplyFromSettings(Root);
        ApplyBackdrop();

        if (_themes.ReduceMotion)
        {
            ResetTiles();
        }
    });

    /// <summary>
    /// Реакция на изменение конфигурации мониторов: dock перекладывается на целевой монитор
    /// без перезапуска оболочки (функция I6).
    /// </summary>
    private void OnDisplayChanged(DisplayChanged changed)
    {
        _monitorTarget.Update(changed.Monitors);

        _ = DispatcherQueue.TryEnqueue(UpdateDockSize);
    }

    /// <summary>
    /// Включает или отключает стеклянный фон окна. В режиме высокой контрастности стекло
    /// убирается: полупрозрачный фон поверх произвольного рабочего стола нечитаем, а
    /// непрозрачный цвет берётся из системной палитры.
    /// </summary>
    private void ApplyBackdrop() =>
        SystemBackdrop = _themes.HighContrast ? null : new Microsoft.UI.Xaml.Media.MicaBackdrop();

    /// <summary>
    /// Следит за активным окном: на рабочем столе dock виден всегда, в чужом приложении
    /// скрывается. Собственный процесс и оболочка Windows (рабочий стол, «Пуск», поиск,
    /// центр уведомлений) считаются «своими» — иначе панель исчезала бы при открытии «Пуска».
    /// </summary>
    private void UpdateForegroundState()
    {
        if (!OverlayWindow.TryGetForegroundWindow(out var hwnd, out var processId) ||
            hwnd == _foregroundHwnd)
        {
            return;
        }

        _foregroundHwnd = hwnd;
        var shell = processId != _ownProcessId && IsShellProcess(processId);
        _shellForeground = shell;
        _foreignForeground = processId != _ownProcessId && !shell;
    }

    /// <summary>Процесс оболочки Windows: его окна не считаются «чужим приложением».</summary>
    private static bool IsShellProcess(uint processId)
    {
        try
        {
            return Process.GetProcessById((int)processId).ProcessName is
                "explorer" or "StartMenuExperienceHost" or "SearchHost" or
                "ShellExperienceHost" or "TextInputHost" or "LockApp";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Видимость панели. На рабочем столе dock виден всегда; в чужом приложении уезжает
    /// за нижний край и вызывается клавишей Windows или касанием нижней кромки экрана.
    /// Пока курсор над панелью, она остаётся раскрытой, чтобы не исчезать под рукой.
    /// Здесь считается только цель: движение выполняет <see cref="OnRevealWatchTick"/>,
    /// чтобы шаг анимации не зависел от частоты таймера курсора.
    /// </summary>
    private void UpdateReveal()
    {
        var now = DateTimeOffset.UtcNow;
        var windowsKey = OverlayWindow.IsWindowsKeyDown();

        if (windowsKey && !_windowsKeyDown)
        {
            _revealUntil = now + RevealHold;
        }

        _windowsKeyDown = windowsKey;

        // Пока курсор внутри видимого контура панели или у самой кромки экрана,
        // она держится открытой. Проверка идёт по реальному положению окна,
        // поэтому «невидимый» прямоугольник за краем экрана её не удерживает.
        if (IsCursorOverPanel() || IsCursorOverBottomHotZone())
        {
            _cursorExitUntil = now + CursorExitDelay;
        }

        var target = 0.0;

        if (!_foreignForeground)
        {
            target = 1.0;
        }
        else if (now < _revealUntil || now < _cursorExitUntil)
        {
            target = 1.0;
        }

        // «Пуск» и панель задач открываются той же клавишей Windows и перекрывают док.
        // Пока активна оболочка, панель возвращается в верх topmost-слоя — иначе вызов
        // по клавише Windows прятал бы её под меню.
        if (_shellForeground && _reveal > 0.01)
        {
            OverlayWindow.RaiseTopMost(_hwnd);
        }

        SetRevealTarget(target);
    }

    /// <summary>
    /// Задаёт цель выезда и запускает таймер движения, если он ещё не идёт: в покое панель
    /// не должна будить поток интерфейса каждые 16 мс.
    /// </summary>
    private void SetRevealTarget(double target)
    {
        // Доступность (I5): при «уменьшить движение» панель перескакивает к цели сразу,
        // без кадра анимации и без запуска таймера движения.
        if (_themes.ReduceMotion)
        {
            StopRevealWatch();

            if (_reveal != target)
            {
                _reveal = target;
                ApplyDockPlacement();
            }

            _revealTarget = target;
            return;
        }

        var changed = Math.Abs(_revealTarget - target) > 0.001;
        _revealTarget = target;

        if (!changed && _revealWatchRunning)
        {
            return;
        }

        if (Math.Abs(_reveal - target) < 0.001)
        {
            StopRevealWatch();
            _reveal = target;
            return;
        }

        if (_revealWatchRunning)
        {
            // Цель сменилась на полпути (курсор вернулся во время ухода): движение
            // продолжается от текущего места, а не прыгает к прежнему отсчёту.
            _revealFrom = _reveal;
            _revealStepStart = DateTimeOffset.UtcNow;
            return;
        }

        _revealWatchRunning = true;
        _revealFrom = _reveal;
        _revealStepStart = DateTimeOffset.UtcNow;
        _revealWatch.Start();
    }

    private void StopRevealWatch()
    {
        if (!_revealWatchRunning)
        {
            return;
        }

        _revealWatch.Stop();
        _revealWatchRunning = false;
    }

    /// <summary>
    /// Шаг анимации выезда: прогресс идёт от стартового значения к цели за фиксированное
    /// время с мягкой синусоидальной кривой (ease-in-out). Раньше прогресс умножался на
    /// постоянные 0.25 за кадр таймера курсора (30 кадров/с), из-за чего панель дёргалась:
    /// первые кадры двигали её на четверть пути, последние — на доли пикселя.
    /// </summary>
    private void OnRevealWatchTick(object? sender, object e)
    {
        if (_hwnd == 0)
        {
            _revealWatch.Stop();
            _revealWatchRunning = false;
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var target = _revealTarget;

        // Доступность (I5): при «уменьшить движение» панель перескакивает к цели.
        if (_themes.ReduceMotion)
        {
            _reveal = target;
            ApplyDockPlacement();
            _revealWatch.Stop();
            _revealWatchRunning = false;
            return;
        }

        var duration = target >= _revealFrom ? RevealOpenDuration : RevealCloseDuration;
        var progress = duration > TimeSpan.Zero
            ? Math.Clamp((now - _revealStepStart).TotalMilliseconds / duration.TotalMilliseconds, 0.0, 1.0)
            : 1.0;

        // Синусоидальный ease-in-out: нулевая скорость на обоих концах, поэтому панель
        // не стартует рывком и не «щёлкает» об кромку в конце.
        var eased = 0.5 - (Math.Cos(Math.PI * progress) / 2.0);
        _reveal = _revealFrom + ((target - _revealFrom) * eased);

        if (progress >= 1.0)
        {
            _reveal = target;
        }

        ApplyDockPlacement();

        if (progress >= 1.0)
        {
            _revealWatch.Stop();
            _revealWatchRunning = false;
        }
    }

    /// <summary>
    /// Курсор внутри видимого контура панели. Считается по сохранённому положению с учётом
    /// прогресса выезда: у полностью скрытой панели нет области наведения, иначе невидимый
    /// прямоугольник за краем экрана удерживал бы её открытой.
    /// </summary>
    private bool IsCursorOverPanel()
    {
        if (_dockWidth <= 0 || _reveal <= 0.001 || !OverlayWindow.TryGetCursorScreenPosition(out var x, out var y))
        {
            return false;
        }

        var visibleTop = _dockTop + (int)Math.Round(_dockHeight * (1.0 - _reveal));

        return x >= _dockLeft &&
               x < _dockLeft + _dockWidth &&
               y >= visibleTop &&
               y <= _dockTop + _dockHeight;
    }

    /// <summary>
    /// Курсор у нижней кромки экрана в пределах панели. Зона считается по сохранённому
    /// положению, а не по окну: у полностью скрытого окна координаты отрицательные.
    /// </summary>
    private bool IsCursorOverBottomHotZone()
    {
        if (_dockWidth <= 0 || !OverlayWindow.TryGetCursorScreenPosition(out var x, out var y))
        {
            return false;
        }

        var bottom = _dockTop + _dockHeight;

        return x >= _dockLeft &&
               x < _dockLeft + _dockWidth &&
               y >= bottom - BottomHotZone &&
               y <= bottom;
    }

    /// <summary>
    /// Таймер читает глобальную позицию курсора и, если он над dock, ведёт магнификацию.
    /// Так эффект не зависит от того, доставляет ли WinUI события ввода окну, показанному
    /// без активации. Когда курсор уходит с панели, плитки плавно возвращаются на место.
    /// </summary>
    private void OnCursorWatchTick(object? sender, object e)
    {
        if (_hwnd == 0)
        {
            return;
        }

        UpdateForegroundState();
        UpdateReveal();

        if (!OverlayWindow.TryGetCursorPosition(_hwnd, out var clientX, out var clientY))
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
        var dipScale = scale <= 0 ? 1.0 : scale;

        // Координаты курсора приходят в физических пикселях окна, а центры плиток хранятся
        // в координатах ItemsControl: без пересчёта магнификация считалась бы со сдвигом
        // на боковой отступ панели и увеличивала бы не ту плитку.
        var rootX = clientX / dipScale;
        var tilesOrigin = Tiles.TransformToVisual(Root).TransformPoint(new Point(0, 0));
        ApplyMagnification(rootX - tilesOrigin.X);
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
        if (sender is not Border { DataContext: DockItemViewModel item } tile)
        {
            return;
        }

        e.Handled = true;
        PlayPress(tile);
        await ViewModel.ActivateItemAsync(item);
    }

    /// <summary>
    /// Отклик на нажатие: плитка коротко проседает и возвращается. Работает независимо от
    /// состояния наведения, поэтому клик виден и тогда, когда магнификация уже сброшена.
    /// </summary>
    private void PlayPress(Border tile)
    {
        var (scaleTransform, translateTransform) = EnsureTransforms(tile);

        if (_themes.ReduceMotion)
        {
            return;
        }

        var duration = TimeSpan.FromMilliseconds(90);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var storyboard = new Storyboard();

        foreach (var property in new[] { nameof(ScaleTransform.ScaleX), nameof(ScaleTransform.ScaleY) })
        {
            var animation = new DoubleAnimation
            {
                To = 0.86,
                Duration = duration,
                EasingFunction = easing,
                AutoReverse = true,
            };

            Storyboard.SetTarget(animation, scaleTransform);
            Storyboard.SetTargetProperty(animation, property);
            storyboard.Children.Add(animation);
        }

        var press = new DoubleAnimation
        {
            To = 3,
            Duration = duration,
            EasingFunction = easing,
            AutoReverse = true,
        };

        Storyboard.SetTarget(press, translateTransform);
        Storyboard.SetTargetProperty(press, nameof(TranslateTransform.Y));
        storyboard.Children.Add(press);

        // После нажатия состояние плитки снова подчиняется магнификации.
        storyboard.Completed += (_, _) =>
        {
            scaleTransform.ScaleX = 1;
            scaleTransform.ScaleY = 1;
            translateTransform.Y = 0;
            _centersDirty = true;
        };

        storyboard.Begin();
    }

    /// <summary>
    /// Клавиатурный путь к плитке (I5): Enter или пробел активируют приложение так же, как
    /// клик мышью. Контекстное меню открывается Shift+F10 или клавишей меню — стандартным
    /// путём WinUI, поэтому отдельная обработка не нужна.
    /// </summary>
    private async void OnTileKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not Border { DataContext: DockItemViewModel item } tile)
        {
            return;
        }

        if (e.Key is not (VirtualKey.Enter or VirtualKey.Space))
        {
            return;
        }

        e.Handled = true;
        PlayPress(tile);
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

            // Плитка опознаётся по имени из разметки: сравнение по ширине ломалось, стоило
            // поправить размер в XAML, и магнификация молча переставала работать.
            if (child is Border { Name: "TileBorder" } border)
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
            scaleTransform.ScaleY = scale;
            translateTransform.Y = -lift;
            return;
        }

        var duration = HoverDuration;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        var scaleAnimationX = new DoubleAnimation
        {
            To = scale,
            Duration = duration,
            EasingFunction = easing,
        };
        Storyboard.SetTarget(scaleAnimationX, scaleTransform);
        Storyboard.SetTargetProperty(scaleAnimationX, nameof(ScaleTransform.ScaleX));

        var scaleAnimationY = new DoubleAnimation
        {
            To = scale,
            Duration = duration,
            EasingFunction = easing,
        };
        Storyboard.SetTarget(scaleAnimationY, scaleTransform);
        Storyboard.SetTargetProperty(scaleAnimationY, nameof(ScaleTransform.ScaleY));

        var liftAnimation = new DoubleAnimation
        {
            To = -lift,
            Duration = duration,
            EasingFunction = easing,
        };
        Storyboard.SetTarget(liftAnimation, translateTransform);
        Storyboard.SetTargetProperty(liftAnimation, nameof(TranslateTransform.Y));

        var storyboard = new Storyboard();
        storyboard.Children.Add(scaleAnimationX);
        storyboard.Children.Add(scaleAnimationY);
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
