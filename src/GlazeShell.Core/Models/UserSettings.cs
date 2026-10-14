namespace GlazeShell.Core.Models;

public sealed record UserSettings
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMonitorNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public UserSettings(
        bool startWithWindows = false,
        bool minimizeToTray = false,
        bool showDesktopOnStartup = true,
        bool restoreLastLayout = true,
        string language = "system",
        string? activeThemeId = null,
        IReadOnlyDictionary<string, string>? monitorNames = null)
    {
        Language = ModelValidation.Required(language, nameof(language));
        ActiveThemeId = ModelValidation.Optional(activeThemeId, nameof(activeThemeId));
        MonitorNames = NormalizeMonitorNames(monitorNames);
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

    /// <summary>
    /// Пользовательские имена дисплеев: идентификатор монитора (HEX HMONITOR) → имя.
    /// Пустая карта означает, что используются имена по умолчанию.
    /// </summary>
    public IReadOnlyDictionary<string, string> MonitorNames { get; }

    public static UserSettings CreateDefault() => new();

    private static IReadOnlyDictionary<string, string> NormalizeMonitorNames(IReadOnlyDictionary<string, string>? names)
    {
        if (names is null || names.Count == 0)
        {
            return EmptyMonitorNames;
        }

        var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in names)
        {
            var normalizedKey = key?.Trim();
            var normalizedValue = value?.Trim();

            if (string.IsNullOrEmpty(normalizedKey) || string.IsNullOrEmpty(normalizedValue))
            {
                continue;
            }

            copy[normalizedKey] = normalizedValue;
        }

        return copy.Count == 0 ? EmptyMonitorNames : copy;
    }
}
