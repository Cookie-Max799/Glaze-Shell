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

        var directoryName = configuration.DataDirectoryName;
        if (string.IsNullOrWhiteSpace(directoryName) ||
            directoryName is "." or ".." ||
            directoryName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The data directory name is invalid.", nameof(configuration));
        }

        return Path.Combine(localAppData, directoryName);
    }

    public static string GetConfigurationFilePath(GlazeShellConfiguration configuration) =>
        Path.Combine(GetRootDirectory(configuration), "config.json");

    public static string GetLogDirectory(GlazeShellConfiguration configuration) =>
        Path.Combine(GetRootDirectory(configuration), "logs");

    public static string GetLogFilePath(GlazeShellConfiguration configuration) =>
        Path.Combine(GetLogDirectory(configuration), "glaze-shell.log");
}
