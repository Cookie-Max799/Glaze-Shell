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

    /// <summary>Полные пути папок, добавленных в «Избранное» Explorer (E2).</summary>
    public List<string>? ExplorerFavorites { get; set; }

    /// <summary>Вид списка Explorer (E1): <c>icons</c> или <c>list</c>.</summary>
    public string? ExplorerViewMode { get; set; }

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
            ExplorerFavorites = settings.ExplorerFavorites.Count == 0
                ? null
                : new List<string>(settings.ExplorerFavorites),
            ExplorerViewMode = settings.ExplorerViewMode.ToId(),
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

        var explorerViewMode = ExplorerViewMode.Icons;
        if (document.ExplorerViewMode is not null && !ExplorerViewModeExtensions.TryParse(document.ExplorerViewMode, out explorerViewMode))
        {
            messages.Add($"The Explorer view mode '{document.ExplorerViewMode}' is not valid.");
        }

        var monitorNames = MapMonitorNames(document.MonitorNames, messages);
        var dockPinnedApps = MapDockPinnedApps(document.DockPinnedApps, messages);
        var explorerFavorites = MapExplorerFavorites(document.ExplorerFavorites, messages);

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
            document.DockReplacesTaskbar,
            explorerFavorites,
            explorerViewMode);
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

    /// <summary>
    /// Разбирает избранные папки Explorer (E2). Полный путь бывает длиннее обычного имени,
    /// поэтому проверяется только отсутствие управляющих символов: длину списка и документа
    /// ограничивают <see cref="UserSettings.MaxExplorerFavorites"/> и лимит размера файла.
    /// </summary>
    private static List<string> MapExplorerFavorites(List<string>? favorites, List<string> messages)
    {
        var parsed = new List<string>();

        if (favorites is null || favorites.Count == 0)
        {
            return parsed;
        }

        if (favorites.Count > UserSettings.MaxExplorerFavorites)
        {
            messages.Add($"The settings document contains more than {UserSettings.MaxExplorerFavorites} Explorer favorites.");
            return parsed;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var favorite in favorites)
        {
            if (string.IsNullOrWhiteSpace(favorite) || PersistenceLimits.ContainsControlCharacters(favorite))
            {
                messages.Add($"The Explorer favorite '{favorite}' is not valid.");
                continue;
            }

            var normalized = favorite.Trim().TrimEnd('\\');

            if (normalized.Length > 0 && seen.Add(normalized))
            {
                parsed.Add(normalized);
            }
        }

        return parsed;
    }
}
