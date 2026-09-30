using GlazeShell.Core.Configuration;

namespace GlazeShell.Infrastructure.System;

public static class UserDataPaths
{
    public static string GetRootDirectory(GlazeShellConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("The local application data directory is unavailable.");
        }

        return Path.Combine(localAppData, UserDataDirectoryName.Validate(configuration.DataDirectoryName));
    }

    public static string GetConfigurationFilePath(GlazeShellConfiguration configuration) =>
        Path.Combine(GetRootDirectory(configuration), "config.json");

    public static string GetLogDirectory(GlazeShellConfiguration configuration) =>
        Path.Combine(GetRootDirectory(configuration), "logs");

    public static string GetLogFilePath(GlazeShellConfiguration configuration) =>
        Path.Combine(GetLogDirectory(configuration), "glaze-shell.log");
}
