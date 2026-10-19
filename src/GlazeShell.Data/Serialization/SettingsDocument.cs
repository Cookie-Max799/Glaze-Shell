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

    public string? ThemeMode { get; set; }

    public string? AccentId { get; set; }

    public bool TopBarEnabled { get; set; } = true;

    public bool DockEnabled { get; set; } = true;

    /// <summary>Заменяет ли dock системную панель задач, скрывая её на время работы.</summary>
    public bool DockReplacesTaskbar { get; set; } = true;

    /// <summary>Идентификаторы приложений, закреплённых в dock, в порядке отображения.</summary>
    public List<string>? DockPinnedApps { get; set; }

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
            ThemeMode = settings.ThemeMode.ToId(),
            AccentId = settings.AccentId,
            TopBarEnabled = settings.TopBarEnabled,
            DockEnabled = settings.DockEnabled,
            DockReplacesTaskbar = settings.DockReplacesTaskbar,
            DockPinnedApps = settings.DockPinnedApps.Count == 0
                ? null
                : new List<string>(settings.DockPinnedApps),
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

        var themeMode = ThemeMode.System;
        if (document.ThemeMode is not null && !ThemeModeExtensions.TryParse(document.ThemeMode, out themeMode))
        {
            messages.Add($"The theme mode '{document.ThemeMode}' is not valid.");
        }

        if (document.AccentId is not null && !AccentPalette.IsKnown(document.AccentId))
        {
            messages.Add($"The accent '{document.AccentId}' is not valid.");
        }

        var monitorNames = MapMonitorNames(document.MonitorNames, messages);
        var dockPinnedApps = MapDockPinnedApps(document.DockPinnedApps, messages);

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
            monitorNames,
            themeMode,
            document.AccentId,
            document.TopBarEnabled,
            document.DockEnabled,
            dockPinnedApps,
            document.DockReplacesTaskbar);
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

    private static List<string> MapDockPinnedApps(List<string>? apps, List<string> messages)
    {
        var parsed = new List<string>();

        if (apps is null || apps.Count == 0)
        {
            return parsed;
        }

        if (apps.Count > UserSettings.MaxDockPinnedApps)
        {
            messages.Add($"The settings document contains more than {UserSettings.MaxDockPinnedApps} dock pinned applications.");
            return parsed;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var appId in apps)
        {
            if (!PersistenceLimits.IsValidIdentifier(appId))
            {
                messages.Add($"The dock pinned application '{appId}' is not valid.");
                continue;
            }

            if (seen.Add(appId))
            {
                parsed.Add(appId);
            }
        }

        return parsed;
    }
}
