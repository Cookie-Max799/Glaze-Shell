namespace GlazeShell.Core.Models;

public enum WallpaperFit
{
    None = 0,
    Fill,
    Fit,
    Stretch,
    Tile,
    Center,
    Span
}

public sealed record Theme
{
    public Theme(
        string id,
        string name,
        ThemeMetadata metadata,
        ThemeColors colors,
        ThemeFonts fonts,
        ThemeDimensions dimensions,
        ThemeIcons icons,
        ThemeWallpaper? wallpaper = null,
        ThemeEffects? effects = null)
    {
        Id = ModelValidation.Required(id, nameof(id));
        Name = ModelValidation.Required(name, nameof(name));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        Colors = colors ?? throw new ArgumentNullException(nameof(colors));
        Fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
        Dimensions = dimensions ?? throw new ArgumentNullException(nameof(dimensions));
        Icons = icons ?? throw new ArgumentNullException(nameof(icons));
        Wallpaper = wallpaper;
        Effects = effects ?? new ThemeEffects();
    }

    public string Id { get; }

    public string Name { get; }

    public ThemeMetadata Metadata { get; }

    public ThemeColors Colors { get; }

    public ThemeFonts Fonts { get; }

    public ThemeDimensions Dimensions { get; }

    public ThemeIcons Icons { get; }

    public ThemeWallpaper? Wallpaper { get; }

    public ThemeEffects Effects { get; }

    /// <summary>
    /// Встроенная тема по умолчанию. Приложение всегда имеет хотя бы одну тему:
    /// набор пользовательских тем не обязателен, а UI не должен оставаться без значений.
    /// </summary>
    public static Theme CreateDefault() => new(
        DefaultThemeId,
        "Glaze Shell",
        new ThemeMetadata("Glaze Shell", "1.0", "Glaze Shell", "The built-in light theme."),
        new ThemeColors(
        [
            new ThemeColor("layerBackground", "#FFFFFFFF"),
            new ThemeColor("layerText", "#FF1A1A1A"),
            new ThemeColor("subtleText", "#FF5C5C5C"),
            new ThemeColor("controlBackground", "#FFF3F3F3"),
            new ThemeColor("controlBackgroundHover", "#FFE8E8E8"),
            new ThemeColor("controlBorder", "#FFD0D0D0"),
            new ThemeColor("accentBackground", "#FF0F6CBD"),
            new ThemeColor("accentText", "#FFFFFFFF"),
            new ThemeColor("criticalBackground", "#FFC42B1C"),
            new ThemeColor("criticalText", "#FFFFFFFF")
        ]),
        new ThemeFonts(
        [
            new ThemeFont("Segoe UI Variable Text", 14),
            new ThemeFont("Segoe UI Variable Display", 20, "Semibold")
        ]),
        new ThemeDimensions(cornerRadius: 8, spacing: 8, iconSize: 24, titleBarHeight: 32),
        new ThemeIcons());

    /// <summary>
    /// Идентификатор встроенной темы. Задан константой, а не строкой в коде загрузки,
    /// чтобы ссылка на тему по умолчанию была одной и проверяемой.
    /// </summary>
    public const string DefaultThemeId = "glaze-default";
}

public sealed record ThemeMetadata
{
    public ThemeMetadata(string name, string version, string? author = null, string? description = null)
    {
        Name = ModelValidation.Required(name, nameof(name));
        Version = ModelValidation.Required(version, nameof(version));
        Author = ModelValidation.Optional(author, nameof(author));
        Description = ModelValidation.Optional(description, nameof(description));
    }

    public string Name { get; }

    public string Version { get; }

    public string? Author { get; }

    public string? Description { get; }
}

public sealed record ThemeColors
{
    public ThemeColors(IEnumerable<ThemeColor>? colors = null)
    {
        var copy = ModelValidation.Copy(colors, nameof(colors));
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var color in copy)
        {
            if (!seen.Add(color.Name))
            {
                throw new ArgumentException($"The color '{color.Name}' is duplicated.", nameof(colors));
            }
        }

        Colors = copy;
    }

    public IReadOnlyList<ThemeColor> Colors { get; }

    /// <summary>
    /// Цвет по имени. Регистр не учитывается: имена задаёт пользователь темы,
    /// а UI обращается к ним из кода. Возвращает <c>null</c>, если тема не задаёт цвет.
    /// </summary>
    public ThemeColor? Find(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        foreach (var color in Colors)
        {
            if (string.Equals(color.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return color;
            }
        }

        return null;
    }
}

public sealed record ThemeColor
{
    public ThemeColor(string name, string value)
    {
        Name = ModelValidation.Required(name, nameof(name));
        Value = ModelValidation.Required(value, nameof(value));
    }

    public string Name { get; }

    public string Value { get; }
}

public sealed record ThemeFonts
{
    public ThemeFonts(IEnumerable<ThemeFont>? fonts = null)
    {
        Fonts = ModelValidation.Copy(fonts, nameof(fonts));
    }

    public IReadOnlyList<ThemeFont> Fonts { get; }
}

public sealed record ThemeFont
{
    public ThemeFont(string family, double size, string? weight = null, string? style = null)
    {
        Family = ModelValidation.Required(family, nameof(family));
        ModelValidation.Positive(size, nameof(size));
        Size = size;
        Weight = ModelValidation.Optional(weight, nameof(weight));
        Style = ModelValidation.Optional(style, nameof(style));
    }

    public string Family { get; }

    public double Size { get; }

    public string? Weight { get; }

    public string? Style { get; }
}

public sealed record ThemeDimensions
{
    public ThemeDimensions(
        double cornerRadius = 8,
        double spacing = 8,
        double iconSize = 24,
        int titleBarHeight = 32)
    {
        ModelValidation.NonNegative(cornerRadius, nameof(cornerRadius));
        ModelValidation.NonNegative(spacing, nameof(spacing));
        ModelValidation.Positive(iconSize, nameof(iconSize));
        ModelValidation.Positive(titleBarHeight, nameof(titleBarHeight));
        CornerRadius = cornerRadius;
        Spacing = spacing;
        IconSize = iconSize;
        TitleBarHeight = titleBarHeight;
    }

    public double CornerRadius { get; }

    public double Spacing { get; }

    public double IconSize { get; }

    public int TitleBarHeight { get; }
}

public sealed record ThemeIcons
{
    public ThemeIcons(IEnumerable<ThemeIcon>? icons = null)
    {
        Icons = ModelValidation.Copy(icons, nameof(icons));
    }

    public IReadOnlyList<ThemeIcon> Icons { get; }
}

public sealed record ThemeIcon
{
    public ThemeIcon(string name, string source)
    {
        Name = ModelValidation.Required(name, nameof(name));
        Source = ThemeAssetValidation.Source(source, nameof(source));
    }

    public string Name { get; }

    public string Source { get; }
}

public sealed record ThemeWallpaper
{
    public ThemeWallpaper(string source, WallpaperFit fit = WallpaperFit.Fill)
    {
        Source = ThemeAssetValidation.Source(source, nameof(source));
        Fit = fit;
    }

    public string Source { get; }

    public WallpaperFit Fit { get; }
}

public sealed record ThemeEffects
{
    public ThemeEffects(
        bool animationsEnabled = true,
        int animationDurationMilliseconds = 150,
        double opacity = 1.0)
    {
        ModelValidation.NonNegative(animationDurationMilliseconds, nameof(animationDurationMilliseconds));
        ModelValidation.InRange(opacity, 0, 1, nameof(opacity));
        AnimationsEnabled = animationsEnabled;
        AnimationDurationMilliseconds = animationDurationMilliseconds;
        Opacity = opacity;
    }

    public bool AnimationsEnabled { get; }

    public int AnimationDurationMilliseconds { get; }

    public double Opacity { get; }
}

/// <summary>
/// Проверка источника ассета темы.
/// </summary>
/// <remarks>
/// Тема — это данные, а не программа: план запрещает выполнять код из тем, поэтому
/// источник проверяется по двум независимым признакам.
/// <para>Расширение: исполняемые и загружаемые форматы отклоняются явным списком,
/// чтобы запрет не зависел от того, какие расширения система считает выполняемыми.</para>
/// <para>Путь: разрешены только относительные пути внутри каталога темы. Абсолютный путь,
/// UNC, диск и выход вверх через <c>..</c> недопустимы, иначе тема заставила бы UI
/// читать файлы за пределами каталога тем — это уже чтение по пути, заданному документом.</para>
/// </remarks>
internal static class ThemeAssetValidation
{
    private static readonly string[] ForbiddenExtensions =
    [
        ".exe",
        ".bat",
        ".cmd",
        ".ps1",
        ".dll",
        ".com",
        ".msi",
        ".scr",
        ".vbs",
        ".js",
        ".jse",
        ".wsf",
        ".hta",
        ".lnk",
        ".reg",
        ".msc",
        ".jar",
        ".psm1"
    ];

    public static string Source(string value, string parameterName)
    {
        var source = ModelValidation.Required(value, parameterName);

        var extension = Path.GetExtension(source);
        if (ForbiddenExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Executable theme assets are not allowed.", parameterName);
        }

        if (!IsSafeRelativePath(source))
        {
            throw new ArgumentException(
                "A theme asset path must be relative and must stay inside the theme directory.",
                parameterName);
        }

        return source;
    }

    private static bool IsSafeRelativePath(string source)
    {
        if (source.IndexOfAny(['\0', '\r', '\n']) >= 0)
        {
            return false;
        }

        // Схема (ms-appx://, http://, file://) и двоеточие — признак URI или потока NTFS,
        // а не относительного пути внутри каталога темы.
        if (source.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        if (Path.IsPathRooted(source) || source.StartsWith('\\') || source.StartsWith('/'))
        {
            return false;
        }

        foreach (var segment in source.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
            {
                return false;
            }
        }

        return source.Length > 0;
    }
}
