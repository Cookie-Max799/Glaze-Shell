using GlazeShell.Core.Configuration;
using GlazeShell.Core.Discovery;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Services;
using GlazeShell.Infrastructure.Logging;
using GlazeShell.Infrastructure.System;
using GlazeShell.Windows.Applications;
using GlazeShell.Windows.DisplayManagement;
using GlazeShell.Windows.Shell;
using GlazeShell.Windows.WindowManagement;
using Microsoft.UI.Xaml;

namespace GlazeShell.App;

public partial class App : Application
{
    private readonly GlazeShellConfiguration _configuration = GlazeShellConfiguration.CreateDefault();
    private readonly FileLogger _logger;
    private Window? _window;

    public App()
    {
        InitializeComponent();
        _logger = new FileLogger(UserDataPaths.GetLogFilePath(_configuration));
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _logger.Write(GlazeLogLevel.Information, "App", "Application launched.");

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

        var desktopManager = new DesktopManager(eventManager);

        _window = new MainWindow(
            _configuration.ApplicationName,
            discovery,
            launcher,
            windowManager,
            monitorManager,
            desktopManager,
            eventManager);
        _window.Activate();
    }
}
