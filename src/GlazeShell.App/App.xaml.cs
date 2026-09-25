using GlazeShell.Core.Configuration;
using GlazeShell.Infrastructure.Logging;
using GlazeShell.Infrastructure.System;
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
        _window = new MainWindow(_configuration.ApplicationName);
        _window.Activate();
    }
}
