using GlazeShell.Core.Configuration;

namespace GlazeShell.Data.Serialization;

/// <summary>
/// Документ конфигурации приложения (`config.json`).
/// Отдельный тип нужен потому, что <see cref="GlazeShellConfiguration"/> хранит
/// `DataDirectoryName`, определяющий сам каталог хранения: значение проверяется
/// до того, как каталог признаётся пригодным.
/// </summary>
public sealed class ConfigurationDocument
{
    public int SchemaVersion { get; set; } = SchemaVersions.Configuration;

    public string? ApplicationName { get; set; } = DefaultApplicationName;

    public string? DataDirectoryName { get; set; } = DefaultDataDirectoryName;

    public const string DefaultApplicationName = "Glaze Shell";

    public const string DefaultDataDirectoryName = "GlazeShell";

    public static ConfigurationDocument FromConfiguration(GlazeShellConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new ConfigurationDocument
        {
            SchemaVersion = configuration.SchemaVersion,
            ApplicationName = configuration.ApplicationName,
            DataDirectoryName = configuration.DataDirectoryName
        };
    }
}

/// <summary>
/// Преобразование документа конфигурации в доменную модель.
/// </summary>
public static class ConfigurationDocumentMapper
{
    public static bool TryMap(ConfigurationDocument? document, out GlazeShellConfiguration? configuration, out IReadOnlyList<string> diagnostics)
    {
        var messages = new List<string>();

        if (document is null)
        {
            configuration = null;
            diagnostics = ["The configuration document is missing."];
            return false;
        }

        if (!PersistenceLimits.IsValidName(document.ApplicationName))
        {
            messages.Add($"The application name '{document.ApplicationName}' is not valid.");
        }

        if (!UserDataDirectoryName.IsValid(document.DataDirectoryName))
        {
            messages.Add($"The data directory name '{document.DataDirectoryName}' is not valid.");
        }

        if (messages.Count > 0)
        {
            configuration = null;
            diagnostics = messages;
            return false;
        }

        configuration = new GlazeShellConfiguration
        {
            SchemaVersion = SchemaVersions.Configuration,
            ApplicationName = document.ApplicationName!,
            DataDirectoryName = document.DataDirectoryName!
        };
        diagnostics = Array.Empty<string>();
        return true;
    }
}
