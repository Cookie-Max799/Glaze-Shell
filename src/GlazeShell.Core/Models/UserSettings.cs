namespace GlazeShell.Core.Models;

public sealed record UserSettings
{
    public UserSettings(
        bool startWithWindows = false,
        bool minimizeToTray = false,
        bool showDesktopOnStartup = true,
        bool restoreLastLayout = true,
        string language = "system",
        string? activeThemeId = null)
    {
        Language = ModelValidation.Required(language, nameof(language));
        ActiveThemeId = ModelValidation.Optional(activeThemeId, nameof(activeThemeId));
        StartWithWindows = startWithWindows;
        MinimizeToTray = minimizeToTray;
        ShowDesktopOnStartup = showDesktopOnStartup;
        RestoreLastLayout = restoreLastLayout;
    }

    public bool StartWithWindows { get; }

    public bool MinimizeToTray { get; }

    public bool ShowDesktopOnStartup { get; }

    public bool RestoreLastLayout { get; }

    public string Language { get; }

    public string? ActiveThemeId { get; }

    public static UserSettings CreateDefault() => new();
}
