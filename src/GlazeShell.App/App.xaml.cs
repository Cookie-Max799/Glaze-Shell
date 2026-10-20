using System.Globalization;
using GlazeShell.App.Theming;
using GlazeShell.Core.Configuration;
using GlazeShell.Core.Diagnostics;
using GlazeShell.Core.Discovery;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Persistence;
using GlazeShell.Core.Services;
using GlazeShell.Data.Persistence;
using GlazeShell.Data.Themes;
using GlazeShell.Infrastructure.Diagnostics;
using GlazeShell.Infrastructure.Logging;
using GlazeShell.Infrastructure.System;
using GlazeShell.Windows.Applications;
using GlazeShell.Windows.DisplayManagement;
using GlazeShell.Windows.FileSystem;
using GlazeShell.Windows.Lifecycle;
using GlazeShell.Windows.Shell;
using GlazeShell.Windows.SystemEvents;
using GlazeShell.Windows.WindowManagement;
using Microsoft.UI.Xaml;
using DesktopLayout = GlazeShell.Core.Models.DesktopLayout;

namespace GlazeShell.App;

public partial class App : Application
{
    private readonly FileLogger _logger;
    private Window? _window;
    private SingleInstanceLease? _singleInstanceLease;

    /// <summary>
    /// Верхняя панель (A6) — отдельное окно. Создаётся и закрывается по настройке
    /// <see cref="GlazeShell.Core.Models.UserSettings.TopBarEnabled"/>.
    /// </summary>
    private TopBarWindow? _topBar;

    /// <summary>
    /// Плавающий dock (A1) — отдельное окно. Создаётся и закрывается по настройке
    /// <see cref="GlazeShell.Core.Models.UserSettings.DockEnabled"/>.
    /// </summary>
    private DockWindow? _dock;

    /// <summary>
    /// Проводник Glaze Shell (E1/E2) — обычное окно приложения. Создаётся лениво по
    /// первому запросу (кнопка главного окна или команда Glance) и переиспользуется,
    /// чтобы в системе не было двух проводников Glaze.
    /// </summary>
    private ExplorerWindow? _explorer;

    /// <summary>
    /// Окно настроек Glaze (I1) — создаётся лениво и переиспользуется.
    /// </summary>
    private SettingsWindow? _settingsWindow;

    /// <summary>
    /// Скрытие системной панели задач, пока dock её заменяет (A1). Состояние живёт
    /// в настройке <see cref="GlazeShell.Core.Models.UserSettings.DockReplacesTaskbar"/>.
    /// </summary>
    private TaskbarHider? _taskbarHider;

    /// <summary>
    /// Глобальная горячая клавиша <c>Ctrl K</c> (B1): открывает поиск Glance в верхней
    /// панели, даже когда приложение не в фокусе.
    /// </summary>
    private GlobalHotkey? _hotkey;

    private IDisposable? _settingsChangedSubscription;

    /// <summary>
    /// Активная тема хранится в поле, а не в локальной переменной: менеджер должен
    /// пережить создание окна, иначе выбор темы потерял бы подписчиков и состояние.
    /// </summary>
    private ThemeManager? _themeManager;

    public App()
    {
        _singleInstanceLease = SingleInstanceHelper.Acquire();
        if (_singleInstanceLease is null)
        {
            SingleInstanceHelper.ActivateExistingInstance();
            Environment.Exit(0);
            return;
        }

        InitializeComponent();
        _logger = new FileLogger(UserDataPaths.GetLogFilePath(GlazeShellConfiguration.CreateDefault()));
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _logger.Write(GlazeLogLevel.Information, "App", "Application launched.");

        var performance = new PerformanceMonitor(_logger);
        var eventLatency = new EventLatencyRecorder();

        var (store, configuration) = CreateUserDataStore();

        var options = ApplicationDiscoveryOptions.Default;
        var eventManager = new EventManager(
            exception => _logger.Write(GlazeLogLevel.Error, "Events", "Unhandled event dispatch failure.", exception),
            eventLatency);

        var discovery = new ApplicationDiscoveryService(
            [new AppsFolderSource(options), new StartMenuShortcutSource(options)],
            eventManager,
            options);

        // Отказ источника не прерывает запуск, но раньше о нём не оставалось следа:
        // предупреждения обнаружения попадают в лог, чтобы пустой список был объясним.
        discovery.DiscoveryCompleted += (_, report) => ReportDiscovery(report);

        var launcher = new WindowsApplicationLauncher(
            discovery,
            new ProcessInspector());

        var windowManager = new WindowManager(eventManager);
        var monitorManager = new MonitorManager(eventManager, ReportEventFailure);
        var eventSources = new ShellEventCoordinator(eventManager, windowManager, monitorManager, errorHandler: ReportEventFailure);

        var desktopManager = new DesktopManager(eventManager, LoadLayout(store, monitorManager));

        var layoutWriter = new DesktopLayoutPersistenceWriter(store, eventManager, ReportPersistence);
        var settingsManager = new PersistingSettingsManager(store, eventManager, ReportPersistence);
        ReportPersistenceStatus(settingsManager.Load());

        _themeManager = CreateThemeManager(store, eventManager, settingsManager.GetSettings().ActiveThemeId);

        var themeApplicator = new ThemeApplicator(settingsManager);

        var window = new MainWindow(
            configuration.ApplicationName,
            discovery,
            launcher,
            windowManager,
            monitorManager,
            desktopManager,
            settingsManager,
            eventManager,
            themeApplicator);
        _window = window;

        // Проводник Glaze Shell (E1/E2): создаётся лениво и переиспользуется — повторное
        // открытие показывает уже существующее окно и, если передана папка, переходит в неё.
        void OpenExplorer(string? path)
        {
            if (_explorer is null)
            {
                _explorer = new ExplorerWindow(
                    new NativeFileSystemBrowser(),
                    settingsManager,
                    eventManager,
                    themeApplicator,
                    new NativeFilePreviewReader(),
                    path);

                _explorer.Closed += (_, _) => _explorer = null;
                _explorer.Activate();
                return;
            }

            _explorer.Activate();

            if (!string.IsNullOrWhiteSpace(path))
            {
                _ = _explorer.ViewModel.NavigateAsync(path);
            }
        }

        // Настройки Glaze Shell (I1): создаются лениво и переиспользуются, чтобы не плодить окна.
        void OpenSettings()
        {
            if (_settingsWindow is null)
            {
                _settingsWindow = new SettingsWindow(settingsManager, eventManager, themeApplicator);

                _settingsWindow.Closed += (_, _) => _settingsWindow = null;
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow.Activate();
        }

        // Кнопка «Проводник» в главном окне и команда open-explorer в Glance ведут в одно окно.
        window.ExplorerRequested += (_, path) => OpenExplorer(path);
        window.SettingsRequested += (_, _) => OpenSettings();

        // Верхняя панель создаётся до активации главного окна: панель показывается без
        // перехвата фокуса, поэтому фокус остаётся у главного окна.
        void ApplyTopBar(bool enabled)
        {
            if (enabled && _topBar is null)
            {
                _topBar = new TopBarWindow(
                    settingsManager,
                    discovery,
                    launcher,
                    windowManager,
                    eventManager,
                    themeApplicator,
                    window);

                // Команда open-explorer приходит сюда из панели: окном Explorer владеет
                // приложение, поэтому панель только пересылает запрос.
                _topBar.ExplorerRequested += (_, path) => OpenExplorer(path);
            }
            else if (!enabled)
            {
                _topBar?.Close();
                _topBar = null;
            }
        }

        void ApplyDock(bool enabled)
        {
            if (enabled)
            {
                _dock ??= new DockWindow(settingsManager, discovery, launcher, windowManager, eventManager, themeApplicator, window);
            }
            else
            {
                _dock?.Close();
                _dock = null;
            }
        }

        // Панель задач скрывается только вместе с dock: без него переключение окон
        // осталось бы без штатной панели. Скрытие обратимо и повторяется после
        // перезапуска Explorer; при выходе панель возвращается на место.
        void ApplyTaskbarReplacement(bool enabled)
        {
            if (enabled)
            {
                _taskbarHider ??= new TaskbarHider();
                _taskbarHider.Hide();
            }
            else
            {
                _taskbarHider?.Dispose();
                _taskbarHider = null;
            }
        }

        // Предыдущий запуск мог завершиться аварийно и оставить панель задач за краем
        // экрана: сохранённого положения в этом случае нет, поэтому панель возвращается
        // к нижней границе монитора до решения о новом скрытии.
        _ = TaskbarHider.RestoreIfOffScreen();

        ApplyTopBar(settingsManager.GetSettings().TopBarEnabled);
        ApplyDock(settingsManager.GetSettings().DockEnabled);
        ApplyTaskbarReplacement(
            settingsManager.GetSettings().DockEnabled &&
            settingsManager.GetSettings().DockReplacesTaskbar);

        // Ctrl K открывает поиск Glance (B1). Комбинацию может занимать другая программа:
        // тогда приложение работает без неё, а причина попадает в лог.
        _hotkey = GlobalHotkey.TryRegister(
            GlobalHotkey.Modifier.Control,
            (uint)'K',
            exception => _logger.Write(GlazeLogLevel.Error, "Hotkey", "The Ctrl K handler failed.", exception));

        if (_hotkey is not null)
        {
            _hotkey.Pressed += OnHotkeyPressed;
        }
        else
        {
            _logger.Write(
                GlazeLogLevel.Warning,
                "Hotkey",
                "The Ctrl K shortcut is unavailable; it is probably taken by another application.");
        }

        _settingsChangedSubscription = eventManager.Subscribe<SettingsChanged>(
            changed =>
            {
                ApplyTopBar(changed.Settings.TopBarEnabled);
                ApplyDock(changed.Settings.DockEnabled);
                ApplyTaskbarReplacement(changed.Settings.DockEnabled && changed.Settings.DockReplacesTaskbar);
            });

        // Координатор запускается после создания окна: подписчики уже зарегистрированы,
        // поэтому ни одно событие не теряется между подпиской и первым проходом источника.
        try
        {
            ReportEventStatus(eventSources.Start());
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException)
        {
            _logger.Write(GlazeLogLevel.Warning, "Events", "System event sources failed to start.", exception);
        }
        catch (Exception exception)
        {
            _logger.Write(GlazeLogLevel.Error, "Events", "Unexpected failure while starting system event sources.", exception);
        }

        window.Closed += (_, _) => Shutdown(layoutWriter, eventSources, performance, eventLatency);

        window.Activate();

        // Baseline снимается после активации окна: время работы процесса в этом снимке —
        // фактическое время запуска приложения, а не только время композиции объектов.
        performance.LogBaseline();
        StartPerformanceSampling(performance);
    }

    /// <summary>
    /// Освобождает ресурсы при закрытии окна. Каждый шаг изолирован: сбой одной очистки
    /// не должен мешать остальным и не должен удерживать процесс в памяти, из-за чего
    /// закрытое окно выглядело бы как «незакрывающееся» приложение. Сбой логируется.
    /// </summary>
    private void Shutdown(
        DesktopLayoutPersistenceWriter layoutWriter,
        ShellEventCoordinator eventSources,
        PerformanceMonitor performance,
        EventLatencyRecorder eventLatency)
    {
        _logger.Write(GlazeLogLevel.Information, "App", "Application window closed, releasing resources.");

        // Панель задач возвращается первой: даже если закрытие панелей или запись
        // данных ниже сорвётся, пользователь не останется без системной панели.
        TryShutdownStep("taskbar", () =>
        {
            _taskbarHider?.Dispose();
            _taskbarHider = null;
        });

        // Горячая клавиша снимается до закрытия панели: иначе нажатие Ctrl K могло бы
        // прийти в уже закрывающееся окно.
        TryShutdownStep("hotkey", () =>
        {
            _hotkey?.Dispose();
            _hotkey = null;
        });

        TryShutdownStep("top bar", () =>
        {
            _settingsChangedSubscription?.Dispose();
            _settingsChangedSubscription = null;
            _topBar?.Close();
            _topBar = null;
        });

        TryShutdownStep("dock", () =>
        {
            _dock?.Close();
            _dock = null;
        });

        TryShutdownStep("explorer", () =>
        {
            _explorer?.Close();
            _explorer = null;
        });

        TryShutdownStep("settings", () =>
        {
            _settingsWindow?.Close();
            _settingsWindow = null;
        });

        TryShutdownStep("layout writer", () =>
        {
            layoutWriter.Flush();
            layoutWriter.Dispose();
        });

        TryShutdownStep("event sources", eventSources.Dispose);
        TryShutdownStep("single-instance lease", () =>
        {
            _singleInstanceLease?.Dispose();
            _singleInstanceLease = null;
        });

        TryShutdownStep("performance summary", () => LogPerformanceSummary(performance, eventLatency));

        _logger.Write(GlazeLogLevel.Information, "App", "Application resources released.");
    }

    /// <summary>
    /// Пишет задержку доставки событий и итог сессии по ресурсам. Вызывается из пути
    /// закрытия после освобождения хуков: к этому моменту счётчики handles и потоков
    /// показывают состояние после остановки источников событий.
    /// </summary>
    private void LogPerformanceSummary(PerformanceMonitor performance, EventLatencyRecorder eventLatency)
    {
        _logger.Write(
            GlazeLogLevel.Information,
            "Performance",
            $"Event dispatch latency: {eventLatency.Snapshot().ToLogString()}.");

        performance.LogSession();
        performance.Dispose();
    }

    /// <summary>
    /// Включает периодическую запись idle-снимков, если задан интервал через переменную
    /// окружения <c>GLAZESHELL_PERF_INTERVAL_MS</c>. По умолчанию профилирование idle выключено:
    /// постоянный таймер — это фоновая нагрузка, которую не создают без потребности.
    /// </summary>
    private void StartPerformanceSampling(PerformanceMonitor performance)
    {
        var configured = Environment.GetEnvironmentVariable("GLAZESHELL_PERF_INTERVAL_MS");
        if (!int.TryParse(configured, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds) ||
            milliseconds <= 0)
        {
            return;
        }

        performance.StartSampling(TimeSpan.FromMilliseconds(milliseconds));
        _logger.Write(
            GlazeLogLevel.Information,
            "Performance",
            $"Idle sampling is enabled every {milliseconds} ms.");
    }

    private void TryShutdownStep(string name, Action step)
    {
        try
        {
            step();
        }
        catch (Exception exception)
        {
            _logger.Write(GlazeLogLevel.Warning, "App", $"Releasing the {name} failed while closing.", exception);
        }
    }

    /// <summary>
    /// Создаёт хранилище пользовательских данных вместе с уже загруженной конфигурацией.
    /// Корневой каталог определяется конфигурацией, поэтому конфигурация читается первой —
    /// из каталога по умолчанию, а перечитывать её из нового корня нельзя: там её ещё нет,
    /// и иначе смена каталога молча сбрасывала бы имя приложения и имя каталога.
    /// </summary>
    private (JsonUserDataStore Store, GlazeShellConfiguration Configuration) CreateUserDataStore()
    {
        var defaults = GlazeShellConfiguration.CreateDefault();
        var defaultRoot = UserDataPaths.GetRootDirectory(defaults);
        var bootstrap = new JsonUserDataStore(defaultRoot, ReportPersistence);

        var configuration = bootstrap.LoadConfiguration();
        ReportPersistenceStatus(configuration);

        if (string.Equals(configuration.Value.DataDirectoryName, defaults.DataDirectoryName, StringComparison.Ordinal))
        {
            return (bootstrap, configuration.Value);
        }

        var root = UserDataPaths.GetRootDirectory(configuration.Value);

        _logger.Write(
            GlazeLogLevel.Information,
            "Persistence",
            $"The configured data directory '{root}' is used. The log file of this session stays in '{defaultRoot}'.");

        return (new JsonUserDataStore(root, ReportPersistence), configuration.Value);
    }

    /// <summary>
    /// Загружает layout и снимает привязки к недоступным мониторам. Результат сверки сразу
    /// сохраняется, иначе снятые привязки повторно вычислялись бы при каждом запуске.
    /// </summary>
    private DesktopLayout LoadLayout(JsonUserDataStore store, MonitorManager monitors)
    {
        var loaded = store.LoadLayout();
        ReportPersistenceStatus(loaded);

        var reconciliation = MonitorLayoutBinding.Reconcile(loaded.Value, monitors.GetMonitors());
        if (!reconciliation.HasChanges)
        {
            return reconciliation.Layout;
        }

        foreach (var tabId in reconciliation.ClearedTabIds)
        {
            _logger.Write(
                GlazeLogLevel.Warning,
                "Persistence",
                $"The monitor binding of tab '{tabId}' was cleared because the monitor is unavailable.");
        }

        try
        {
            store.SaveLayout(reconciliation.Layout);
        }
        catch (Exception exception) when (exception is PersistenceException or IOException or UnauthorizedAccessException)
        {
            ReportPersistence("The reconciled layout could not be saved.", exception);
        }

        return reconciliation.Layout;
    }

    /// <summary>
    /// Загружает пользовательские темы и выбирает тему из настроек.
    /// Темы наполняет пользователь, поэтому их отсутствие или повреждение — не повод
    /// прерывать запуск: применяется встроенная тема, а причины попадают в лог.
    /// </summary>
    private ThemeManager CreateThemeManager(JsonUserDataStore store, IEventManager eventManager, string? activeThemeId)
    {
        var loaded = new ThemeStore(store.RootDirectory).LoadThemes();

        foreach (var diagnostic in loaded.Diagnostics)
        {
            _logger.Write(GlazeLogLevel.Warning, "Theme", diagnostic);
        }

        var manager = new ThemeManager(eventManager, loaded.Themes, activeThemeId);
        var active = manager.GetActiveTheme();

        if (activeThemeId is not null && !string.Equals(activeThemeId, active.Id, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Write(
                GlazeLogLevel.Warning,
                "Theme",
                $"The configured theme '{activeThemeId}' is not available, so the theme '{active.Id}' is used.");
        }

        _logger.Write(
            GlazeLogLevel.Information,
            "Theme",
            $"Theme '{active.Id}' is active, {manager.GetThemes().Count} user theme(s) are loaded.");

        return manager;
    }

    private void ReportDiscovery(DiscoveryReport report)
    {
        foreach (var warning in report.Warnings)
        {
            _logger.Write(GlazeLogLevel.Warning, "Discovery", warning);
        }
    }

    private void ReportPersistenceStatus<T>(PersistenceLoadResult<T> result)
    {
        if (result.Status == PersistenceStatus.Loaded)
        {
            return;
        }

        var level = result.Status == PersistenceStatus.Unsupported ? GlazeLogLevel.Warning : GlazeLogLevel.Information;
        foreach (var diagnostic in result.Diagnostics)
        {
            _logger.Write(level, "Persistence", diagnostic);
        }
    }

    private void ReportPersistence(string message, Exception? exception) =>
        _logger.Write(GlazeLogLevel.Warning, "Persistence", message, exception);

    private void ReportEventFailure(Exception exception) =>
        _logger.Write(GlazeLogLevel.Warning, "Events", "A system event source failed.", exception);

    /// <summary>
    /// Обработка нажатия глобальной горячей клавиши <c>Ctrl K</c>. Приходит с потока
    /// слушателя, поэтому <see cref="TopBarWindow.FocusSearch"/> сам переключается на
    /// поток интерфейса.
    /// </summary>
    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        var topBar = _topBar;

        if (topBar is null)
        {
            _logger.Write(
                GlazeLogLevel.Information,
                "Hotkey",
                "The Ctrl K shortcut was pressed, but the top bar is disabled.");
            return;
        }

        topBar.FocusSearch();
    }

    /// <summary>
    /// Пишет результат запуска источников системных событий. Отдельный отказ не является
    /// поводом прерывать запуск: приложение остаётся рабочим, но соответствующие события
    /// не приходят, поэтому причина попадает в лог как warning.
    /// </summary>
    private void ReportEventStatus(ShellEventStatus status)
    {
        foreach (var diagnostic in status.Diagnostics)
        {
            _logger.Write(GlazeLogLevel.Warning, "Events", diagnostic);
        }

        _logger.Write(
            GlazeLogLevel.Information,
            "Events",
            $"System event sources: window events {Describe(status.WindowEventsStarted)}, " +
            $"display events {Describe(status.DisplayEventsStarted)}, " +
            $"process events {Describe(status.ProcessEventsStarted)}.");

        static string Describe(bool started) => started ? "started" : "unavailable";
    }
}
