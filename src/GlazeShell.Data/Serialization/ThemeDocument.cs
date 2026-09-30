namespace GlazeShell.Data.Serialization;

/// <summary>
/// Документ темы в JSON. Зеркалит модель <c>Theme</c> и содержит только данные:
/// тема описывает оформление и не может содержать исполняемый код.
/// </summary>
internal sealed class ThemeDocument
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public ThemeMetadataDocument? Metadata { get; set; }

    public List<ThemeColorDocument>? Colors { get; set; }

    public List<ThemeFontDocument>? Fonts { get; set; }

    public ThemeDimensionsDocument? Dimensions { get; set; }

    public List<ThemeIconDocument>? Icons { get; set; }

    public ThemeWallpaperDocument? Wallpaper { get; set; }

    public ThemeEffectsDocument? Effects { get; set; }
}

internal sealed class ThemeMetadataDocument
{
    public string? Name { get; set; }

    public string? Version { get; set; }

    public string? Author { get; set; }

    public string? Description { get; set; }
}

internal sealed class ThemeColorDocument
{
    public string? Name { get; set; }

    public string? Value { get; set; }
}

internal sealed class ThemeFontDocument
{
    public string? Family { get; set; }

    public double? Size { get; set; }

    public string? Weight { get; set; }

    public string? Style { get; set; }
}

internal sealed class ThemeDimensionsDocument
{
    public double? CornerRadius { get; set; }

    public double? Spacing { get; set; }

    public double? IconSize { get; set; }

    public int? TitleBarHeight { get; set; }
}

internal sealed class ThemeIconDocument
{
    public string? Name { get; set; }

    public string? Source { get; set; }
}

internal sealed class ThemeWallpaperDocument
{
    public string? Source { get; set; }

    public string? Fit { get; set; }
}

internal sealed class ThemeEffectsDocument
{
    public bool? AnimationsEnabled { get; set; }

    public int? AnimationDurationMilliseconds { get; set; }

    public double? Opacity { get; set; }
}
