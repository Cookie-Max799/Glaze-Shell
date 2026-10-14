using GlazeShell.Core.Models;

namespace GlazeShell.Data.Serialization;

/// <summary>
/// Документ настроек пользователя (`settings.json`).
/// </summary>
public sealed class SettingsDocument
{
    public const string DefaultLanguage = "system";

    public int SchemaVersion { get; set; } = SchemaVersions.Settings;

    public bool StartWithWindows { get; set; }

    public bool MinimizeToTray { get; set; }

    public bool ShowDesktopOnStartup { get; set; } = true;

    public bool RestoreLastLayout { get; set; } = true;

    public string? Language { get; set; } = DefaultLanguage;

    public string? ActiveThemeId { get; set; }

    public Dictionary<string, string>? MonitorNames { get; set; }

    public static SettingsDocument FromSettings(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new SettingsDocument
        {
            SchemaVersion = SchemaVersions.Settings,
            StartWithWindows = settings.StartWithWindows,
            MinimizeToTray = settings.MinimizeToTray,
            ShowDesktopOnStartup = settings.ShowDesktopOnStartup,
            RestoreLastLayout = settings.RestoreLastLayout,
            Language = settings.Language,
            ActiveThemeId = settings.ActiveThemeId,
            MonitorNames = settings.MonitorNames.Count == 0
                ? null
                : new Dictionary<string, string>(settings.MonitorNames, StringComparer.OrdinalIgnoreCase)
        };
    }
}

/// <summary>
/// Преобразование документа настроек в доменную модель.
/// </summary>
public static class SettingsDocumentMapper
{
    public static bool TryMap(SettingsDocument? document, out UserSettings? settings, out IReadOnlyList<string> diagnostics)
    {
        var messages = new List<string>();

        if (document is null)
        {
            settings = null;
            diagnostics = ["The settings document is missing."];
            return false;
        }

        var language = document.Language ?? SettingsDocument.DefaultLanguage;
        if (!PersistenceLimits.IsValidIdentifier(language))
        {
            messages.Add($"The language '{document.Language}' is not valid.");
        }

        if (document.ActiveThemeId is not null && !PersistenceLimits.IsValidIdentifier(document.ActiveThemeId))
        {
            messages.Add($"The theme identifier '{document.ActiveThemeId}' is not valid.");
        }

        var monitorNames = MapMonitorNames(document.MonitorNames, messages);

        if (messages.Count > 0)
        {
            settings = null;
            diagnostics = messages;
            return false;
        }

        settings = new UserSettings(
            document.StartWithWindows,
            document.MinimizeToTray,
            document.ShowDesktopOnStartup,
            document.RestoreLastLayout,
            language,
            document.ActiveThemeId,
            monitorNames);
        diagnostics = Array.Empty<string>();
        return true;
    }

    private static Dictionary<string, string> MapMonitorNames(
        Dictionary<string, string>? names,
        List<string> messages)
    {
        if (names is null || names.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        if (names.Count > PersistenceLimits.MaxMonitorNames)
        {
            messages.Add($"The settings document contains more than {PersistenceLimits.MaxMonitorNames} monitor names.");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in names)
        {
            if (string.IsNullOrWhiteSpace(key) || !PersistenceLimits.IsValidIdentifier(key))
            {
                messages.Add($"The monitor identifier '{key}' is not valid.");
                continue;
            }

            if (!PersistenceLimits.IsValidName(value))
            {
                messages.Add($"The monitor name of '{key}' is not valid.");
                continue;
            }

            parsed[key] = value;
        }

        return parsed;
    }
}
