using GlazeShell.Core.Models;

namespace GlazeShell.Core.Settings;

/// <summary>
/// Явная пересборка <see cref="UserSettings"/> для каталога настроек. Записи-настройки не
/// поддерживают <c>with</c> (у них явный конструктор с get-only свойствами), поэтому каждое
/// изменение приходится выражать новым вызовом конструктора, где все поля берутся из
/// исходного набора, кроме одного. Так точечное изменение не теряет остальные настройки и не
/// ломается при добавлении новых полей: их достаточно провести в одном месте.
/// </summary>
public static class UserSettingsEditor
{
    public static UserSettings With(
        UserSettings source,
        bool? startWithWindows = null,
        bool? minimizeToTray = null,
        bool? showDesktopOnStartup = null,
        bool? restoreLastLayout = null,
        string? language = null,
        string? activeThemeId = null,
        ThemeMode? themeMode = null,
        string? accentId = null,
        bool clearAccent = false,
        bool? reduceMotion = null,
        bool? highContrast = null,
        bool? topBarEnabled = null,
        bool? dockEnabled = null,
        bool? dockReplacesTaskbar = null,
        ExplorerViewMode? explorerViewMode = null,
        bool? recoveryHotkeyEnabled = null,
        string? recoveryHotkeyId = null,
        bool? safeModeEnabled = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new UserSettings(
            startWithWindows: startWithWindows ?? source.StartWithWindows,
            minimizeToTray: minimizeToTray ?? source.MinimizeToTray,
            showDesktopOnStartup: showDesktopOnStartup ?? source.ShowDesktopOnStartup,
            restoreLastLayout: restoreLastLayout ?? source.RestoreLastLayout,
            language: language ?? source.Language,
            activeThemeId: activeThemeId ?? source.ActiveThemeId,
            monitorNames: source.MonitorNames,
            themeMode: themeMode ?? source.ThemeMode,
            accentId: clearAccent ? null : accentId ?? source.AccentId,
            topBarEnabled: topBarEnabled ?? source.TopBarEnabled,
            dockEnabled: dockEnabled ?? source.DockEnabled,
            dockPinnedApps: source.DockPinnedApps,
            dockReplacesTaskbar: dockReplacesTaskbar ?? source.DockReplacesTaskbar,
            explorerFavorites: source.ExplorerFavorites,
            explorerViewMode: explorerViewMode ?? source.ExplorerViewMode,
            reduceMotion: reduceMotion ?? source.ReduceMotion,
            highContrast: highContrast ?? source.HighContrast,
            recoveryHotkeyEnabled: recoveryHotkeyEnabled ?? source.RecoveryHotkeyEnabled,
            recoveryHotkeyId: recoveryHotkeyId ?? source.RecoveryHotkeyId,
            safeModeEnabled: safeModeEnabled ?? source.SafeModeEnabled);
    }
}
