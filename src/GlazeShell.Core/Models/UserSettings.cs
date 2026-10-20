namespace GlazeShell.Core.Models;

public sealed record UserSettings
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMonitorNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Максимальное число закреплённых в dock приложений (функция A1).</summary>
    public const int MaxDockPinnedApps = 32;

    /// <summary>Максимальное число избранных папок Explorer (функция E2).</summary>
    public const int MaxExplorerFavorites = 32;

    private static readonly IReadOnlyList<string> EmptyDockPinnedApps = Array.Empty<string>();

    private static readonly IReadOnlyList<string> EmptyExplorerFavorites = Array.Empty<string>();

    public UserSettings(
        bool startWithWindows = false,
        bool minimizeToTray = false,
        bool showDesktopOnStartup = true,
        bool restoreLastLayout = true,
        string language = "system",
        string? activeThemeId = null,
        IReadOnlyDictionary<string, string>? monitorNames = null,
        ThemeMode themeMode = ThemeMode.System,
        string? accentId = null,
        bool topBarEnabled = true,
        bool dockEnabled = true,
        IReadOnlyList<string>? dockPinnedApps = null,
        bool dockReplacesTaskbar = true,
        IReadOnlyList<string>? explorerFavorites = null,
        ExplorerViewMode explorerViewMode = ExplorerViewMode.Icons)
    {
        Language = ModelValidation.Required(language, nameof(language));
        ActiveThemeId = ModelValidation.Optional(activeThemeId, nameof(activeThemeId));
        MonitorNames = NormalizeMonitorNames(monitorNames);
        ThemeMode = ValidateThemeMode(themeMode, nameof(themeMode));
        AccentId = NormalizeAccent(accentId);
        TopBarEnabled = topBarEnabled;
        DockEnabled = dockEnabled;
        DockPinnedApps = NormalizeDockPinnedApps(dockPinnedApps);
        DockReplacesTaskbar = dockReplacesTaskbar;
        ExplorerFavorites = NormalizeExplorerFavorites(explorerFavorites);
        ExplorerViewMode = ValidateExplorerViewMode(explorerViewMode, nameof(explorerViewMode));
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

    /// <summary>
    /// Режим оформления: следовать системе либо явно светлая/тёмная тема.
    /// </summary>
    public ThemeMode ThemeMode { get; }

    /// <summary>
    /// Идентификатор выбранного акцентного цвета (<see cref="AccentPalette"/>) или
    /// <c>null</c>, если используется акцент темы по умолчанию.
    /// </summary>
    public string? AccentId { get; }

    /// <summary>
    /// Показывать ли плавающую верхнюю панель (Top bar, функция A6).
    /// </summary>
    public bool TopBarEnabled { get; }

    /// <summary>
    /// Показывать ли плавающий dock (функция A1).
    /// </summary>
    public bool DockEnabled { get; }

    /// <summary>
    /// Идентификаторы приложений, закреплённых в dock (функция A1), в порядке отображения.
    /// Закреплённые показываются отдельно от запущенных и переживают перезапуск.
    /// </summary>
    public IReadOnlyList<string> DockPinnedApps { get; }

    /// <summary>
    /// Заменяет ли dock системную панель задач: пока dock показан и настройка включена,
    /// панель задач сдвигается за край экрана и возвращается при выходе.
    /// </summary>
    public bool DockReplacesTaskbar { get; }

    /// <summary>
    /// Полные пути папок, добавленных пользователем в «Избранное» сайдбара Explorer (E2),
    /// в порядке отображения. Системные папки из этого списка не хранятся: они показываются
    /// всегда и приходят из <c>IFileSystemBrowser</c>.
    /// </summary>
    public IReadOnlyList<string> ExplorerFavorites { get; }

    /// <summary>
    /// Вид списка Explorer (E1): иконки или список. Значение общее для всех папок, а не
    /// отдельное на каждую: память вида по папке — следующий шаг.
    /// </summary>
    public ExplorerViewMode ExplorerViewMode { get; }

    public static UserSettings CreateDefault() => new();

    private static ExplorerViewMode ValidateExplorerViewMode(ExplorerViewMode mode, string parameterName) =>
        mode is ExplorerViewMode.Icons or ExplorerViewMode.List
            ? mode
            : throw new ArgumentOutOfRangeException(parameterName, mode, "The Explorer view mode is not supported.");

    /// <summary>
    /// Нормализует избранные папки Explorer: отбрасывает пустые и повторяющиеся пути,
    /// сохраняя порядок пользователя, и ограничивает число элементов.
    /// </summary>
    private static IReadOnlyList<string> NormalizeExplorerFavorites(IReadOnlyList<string>? favorites)
    {
        if (favorites is null || favorites.Count == 0)
        {
            return EmptyExplorerFavorites;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(Math.Min(favorites.Count, MaxExplorerFavorites));

        foreach (var favorite in favorites)
        {
            var normalized = favorite?.Trim().TrimEnd('\\');

            if (string.IsNullOrEmpty(normalized) || !seen.Add(normalized))
            {
                continue;
            }

            result.Add(normalized);

            if (result.Count >= MaxExplorerFavorites)
            {
                break;
            }
        }

        return result.Count == 0 ? EmptyExplorerFavorites : result;
    }

    private static ThemeMode ValidateThemeMode(ThemeMode mode, string parameterName) =>
        mode is ThemeMode.System or ThemeMode.Light or ThemeMode.Dark
            ? mode
            : throw new ArgumentOutOfRangeException(parameterName, mode, "The theme mode is not supported.");

    private static string? NormalizeAccent(string? accentId)
    {
        var normalized = ModelValidation.Optional(accentId, nameof(accentId));

        if (normalized is null)
        {
            return null;
        }

        if (!AccentPalette.IsKnown(normalized))
        {
            throw new ArgumentException($"The accent '{normalized}' is not supported.", nameof(accentId));
        }

        return normalized;
    }

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

    /// <summary>
    /// Нормализует список закреплённых в dock приложений: отбрасывает пустые и повторяющиеся
    /// идентификаторы, сохраняя порядок пользователя, и ограничивает число элементов.
    /// </summary>
    private static IReadOnlyList<string> NormalizeDockPinnedApps(IReadOnlyList<string>? apps)
    {
        if (apps is null || apps.Count == 0)
        {
            return EmptyDockPinnedApps;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(Math.Min(apps.Count, MaxDockPinnedApps));

        foreach (var appId in apps)
        {
            var normalized = appId?.Trim();

            if (string.IsNullOrEmpty(normalized) || !seen.Add(normalized))
            {
                continue;
            }

            result.Add(normalized);

            if (result.Count >= MaxDockPinnedApps)
            {
                break;
            }
        }

        return result.Count == 0 ? EmptyDockPinnedApps : result;
    }
}
