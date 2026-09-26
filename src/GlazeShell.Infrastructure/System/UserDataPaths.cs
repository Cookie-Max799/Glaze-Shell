using GlazeShell.Core.Configuration;

namespace GlazeShell.Infrastructure.System;

public static class UserDataPaths
{
    private const int MaxDirectoryNameLength = 64;

    private static readonly string[] ReservedDeviceNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    public static string GetRootDirectory(GlazeShellConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("The local application data directory is unavailable.");
        }

        return Path.Combine(localAppData, ValidateDirectoryName(configuration.DataDirectoryName));
    }

    public static string GetConfigurationFilePath(GlazeShellConfiguration configuration) =>
        Path.Combine(GetRootDirectory(configuration), "config.json");

    public static string GetLogDirectory(GlazeShellConfiguration configuration) =>
        Path.Combine(GetRootDirectory(configuration), "logs");

    public static string GetLogFilePath(GlazeShellConfiguration configuration) =>
        Path.Combine(GetLogDirectory(configuration), "glaze-shell.log");

    private static string ValidateDirectoryName(string directoryName)
    {
        if (string.IsNullOrWhiteSpace(directoryName) ||
            directoryName is "." or ".." ||
            directoryName.Length > MaxDirectoryNameLength ||
            directoryName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The data directory name is invalid.", nameof(directoryName));
        }

        if (directoryName.EndsWith('.') || directoryName.EndsWith(' '))
        {
            throw new ArgumentException("The data directory name cannot end with a dot or a space.", nameof(directoryName));
        }

        var deviceStem = directoryName.Split('.')[0];
        if (ReservedDeviceNames.Contains(deviceStem, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The data directory name is a reserved device name.", nameof(directoryName));
        }

        return directoryName;
    }
}
