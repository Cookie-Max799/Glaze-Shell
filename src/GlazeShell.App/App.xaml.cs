using GlazeShell.Core.Configuration;
using GlazeShell.Core.Discovery;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Persistence;
using GlazeShell.Core.Services;
using GlazeShell.Data.Persistence;
using GlazeShell.Data.Themes;
using GlazeShell.Infrastructure.Logging;
using GlazeShell.Infrastructure.System;
using GlazeShell.Windows.Applications;
using GlazeShell.Windows.DisplayManagement;
using GlazeShell.Windows.Shell;
using GlazeShell.Windows.WindowManagement;
using Microsoft.UI.Xaml;
using DesktopLayout = GlazeShell.Core.Models.DesktopLayout;

namespace GlazeShell.App;

public partial class App : Application
{
    private readonly FileLogger _logger;
    private Window? _window;

    /// <summary>
    /// Активная тема хранится в поле, а не в локальной переменной: менеджер должен
    /// пережить создание окна, иначе выбор темы потерял бы подписчиков и состояние.
    /// </summary>
    private ThemeManager? _themeManager;

    public App()
    {
        InitializeComponent();
        _logger = new FileLogger(UserDataPaths.GetLogFilePath(GlazeShellConfiguration.CreateDefault()));
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _logger.Write(GlazeLogLevel.Information, "App", "Application launched.");

        var (store, configuration) = CreateUserDataStore();

        var options = ApplicationDiscoveryOptions.Default;
        var eventManager = new EventManager(exception =>
            _logger.Write(GlazeLogLevel.Error, "Events", "Unhandled event dispatch failure.", exception));

        var discovery = new ApplicationDiscoveryService(
            [new AppsFolderSource(options), new StartMenuShortcutSource(options)],
            eventManager,
            options);

        var launcher = new WindowsApplicationLauncher(
            discovery,
            new ProcessInspector(),
            new PackageInstallLocationResolver());

        var windowManager = new WindowManager(eventManager);

        if (!windowManager.Start())
        {
            _logger.Write(GlazeLogLevel.Warning, "WindowManager", "Window events monitor could not be started.");
        }

        var monitorManager = new MonitorManager(eventManager);

        if (!monitorManager.Start())
        {
            _logger.Write(GlazeLogLevel.Warning, "MonitorManager", "Display events monitor could not be started.");
        }

        var desktopManager = new DesktopManager(eventManager, LoadLayout(store, monitorManager));

        var layoutWriter = new DesktopLayoutPersistenceWriter(store, eventManager, ReportPersistence);
        var settingsManager = new PersistingSettingsManager(store, eventManager, ReportPersistence);
        ReportPersistenceStatus(settingsManager.Load());

        _themeManager = CreateThemeManager(store, eventManager, settingsManager.GetSettings().ActiveThemeId);

        _window = new MainWindow(
            configuration.ApplicationName,
            discovery,
            launcher,
            windowManager,
            monitorManager,
            desktopManager,
            eventManager);

        _window.Closed += (_, _) =>
        {
            layoutWriter.Flush();
            layoutWriter.Dispose();
        };

        _window.Activate();
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
}
