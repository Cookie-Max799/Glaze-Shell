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
            ActiveThemeId = settings.ActiveThemeId
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
            document.ActiveThemeId);
        diagnostics = Array.Empty<string>();
        return true;
    }
}
