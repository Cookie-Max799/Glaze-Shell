using GlazeShell.Core.Models;

namespace GlazeShell.Core.Theming;

/// <summary>
/// Светлая и тёмная палитры Glaze Shell и правила их тонирования основным цветом.
/// </summary>
/// <remarks>
/// Палитра живёт в ядре, а не в приложении, чтобы контраст текста и иконок можно было
/// проверить тестами для всех вариантов цветов, а не только глазами в конкретной теме.
/// После смешения с основным цветом яркость поверхностей ограничивается безопасным
/// диапазоном, а текст и акцент при необходимости отодвигаются от фона по контрасту WCAG.
/// </remarks>
public static class GlazePalette
{
    /// <summary>
    /// Прозрачность стекла по умолчанию: 45 % (непрозрачность 55 % — значение DESIGN §4).
    /// </summary>
    public const int DefaultGlassTransparency = UserSettings.DefaultGlassTransparency;

    /// <summary>
    /// Предел прозрачности стекла, который пользователь может выставить ползунком.
    /// Дальше панель почти сливается с фоном, и подписи теряют читаемость.
    /// </summary>
    public const int MaxGlassTransparency = UserSettings.MaxGlassTransparency;

    /// <summary>Прозрачность акцентной рамки поверхностей.</summary>
    public const byte BorderAlpha = 0x40;

    private const double PrimaryContrast = 7.0;
    private const double SecondaryContrast = 4.5;
    private const double MutedContrast = 3.0;
    private const double AccentContrast = 4.5;

    public static GlazeTheme Light { get; } = new(
        IsLight: true,
        Background: GlazeColor.ParseHex("#F2EDFF", GlazeColor.White),
        Surface: GlazeColor.ParseHex("#FFFFFF", GlazeColor.White),
        Elevated: GlazeColor.ParseHex("#E7DEFF", GlazeColor.White),
        Border: GlazeColor.ParseHex("#406C4DFF", GlazeColor.White),
        Glass: GlazeColor.ParseHex("#8CFFFFFF", GlazeColor.White),
        TextPrimary: GlazeColor.ParseHex("#1C1740", GlazeColor.Black),
        TextSecondary: GlazeColor.ParseHex("#5A5480", GlazeColor.Black),
        TextMuted: GlazeColor.ParseHex("#8A84B3", GlazeColor.Black),
        DefaultAccent: GlazeColor.ParseHex("#6C4DFF", GlazeColor.White),
        AccentForeground: GlazeColor.ParseHex("#5B3BE8", GlazeColor.White));

    public static GlazeTheme Dark { get; } = new(
        IsLight: false,
        Background: GlazeColor.ParseHex("#0E0B22", GlazeColor.Black),
        Surface: GlazeColor.ParseHex("#171332", GlazeColor.Black),
        Elevated: GlazeColor.ParseHex("#221C48", GlazeColor.Black),
        Border: GlazeColor.ParseHex("#40B4A4FF", GlazeColor.Black),
        Glass: GlazeColor.ParseHex("#8C211B45", GlazeColor.Black),
        TextPrimary: GlazeColor.ParseHex("#F8F4FF", GlazeColor.White),
        TextSecondary: GlazeColor.ParseHex("#C6C0EC", GlazeColor.White),
        TextMuted: GlazeColor.ParseHex("#918BBB", GlazeColor.White),
        DefaultAccent: GlazeColor.ParseHex("#B4A4FF", GlazeColor.White),
        AccentForeground: GlazeColor.ParseHex("#C9BEFF", GlazeColor.White));

    /// <summary>
    /// Собирает действующую тему: тонирует поверхности основным цветом, выравнивает
    /// читаемость текста, подбирает акцент и применяет прозрачность стекла.
    /// Возвращает готовые токены без привязки к WinUI.
    /// </summary>
    /// <param name="light">Светлая ли тема.</param>
    /// <param name="baseColorId">Идентификатор основного цвета или <c>null</c>.</param>
    /// <param name="accentId">Идентификатор акцента или <c>null</c>.</param>
    /// <param name="glassTransparency">
    /// Прозрачность стекла в процентах: 0 — плотное стекло, 100 — почти прозрачное.
    /// Значение ограничивается <see cref="MaxGlassTransparency"/>.
    /// </param>
    public static GlazeTheme Create(
        bool light,
        string? baseColorId,
        string? accentId,
        int glassTransparency = DefaultGlassTransparency)
    {
        var theme = TintWithBase(light ? Light : Dark, baseColorId);
        theme = EnsureReadable(theme);

        var accent = ResolveAccent(accentId, theme);
        var surfaces = new[] { theme.Background, theme.Surface, theme.Elevated };
        var accentForeground = accent.EnsureContrastAgainst(
            surfaces,
            AccentContrast,
            towardWhite: !theme.IsLight);

        return theme with
        {
            Border = accent.WithAlpha(BorderAlpha),
            Glass = theme.Glass.WithAlpha(GlassAlphaFor(glassTransparency)),
            DefaultAccent = accent,
            AccentForeground = accentForeground
        };
    }

    /// <summary>
    /// Переводит прозрачность в процентах в альфа-канал стекла. Прозрачность 0 % даёт
    /// непрозрачную заливку, 100 % — полностью прозрачную, но ползунок ограничен
    /// <see cref="MaxGlassTransparency"/>, чтобы панель не растворялась целиком.
    /// </summary>
    public static byte GlassAlphaFor(int transparency)
    {
        var limited = Math.Clamp(transparency, 0, MaxGlassTransparency);

        return (byte)Math.Round(255 * (1 - (limited / 100.0)));
    }

    /// <summary>
    /// Тонирует поверхности темы основным цветом пользователя. Смешение, а не замена цвета:
    /// оттенок заметен, но поверхности сохраняют светлоту своей темы.
    /// </summary>
    public static GlazeTheme TintWithBase(GlazeTheme theme, string? baseColorId)
    {
        var option = AccentPalette.Find(baseColorId);

        if (option is null)
        {
            return theme;
        }

        var baseColor = GlazeColor.ParseHex(option.Hex, theme.DefaultAccent);

        return theme with
        {
            Background = GlazeColor.Mix(theme.Background, baseColor, 0.32),
            Surface = GlazeColor.Mix(theme.Surface, baseColor, 0.24),
            Elevated = GlazeColor.Mix(theme.Elevated, baseColor, 0.42),
            Glass = GlazeColor.Mix(theme.Glass, baseColor, 0.38),
            DefaultAccent = GlazeColor.Mix(theme.DefaultAccent, baseColor, 0.55)
        };
    }

    /// <summary>Акцент из палитры или акцент темы по умолчанию.</summary>
    public static GlazeColor ResolveAccent(string? accentId, GlazeTheme theme)
    {
        var option = AccentPalette.Find(accentId);

        return option is null
            ? theme.DefaultAccent
            : GlazeColor.ParseHex(option.Hex, theme.DefaultAccent);
    }

    /// <summary>
    /// Ограничивает яркость поверхностей и поднимает контраст текста. Светлая тема держит
    /// поверхности светлыми для тёмного текста, тёмная — тёмными для светлого.
    /// </summary>
    public static GlazeTheme EnsureReadable(GlazeTheme theme)
    {
        if (theme.IsLight)
        {
            var background = theme.Background.ClampLuminance(0.64, 1.0);
            var surface = theme.Surface.ClampLuminance(0.70, 1.0);
            var elevated = theme.Elevated.ClampLuminance(0.66, 1.0);
            var glass = theme.Glass.ClampLuminance(0.65, 1.0);
            var surfaces = new[] { background, surface, elevated };

            return theme with
            {
                Background = background,
                Surface = surface,
                Elevated = elevated,
                Glass = glass.WithAlpha(theme.Glass.A),
                TextPrimary = theme.TextPrimary.EnsureContrastAgainst(surfaces, PrimaryContrast, towardWhite: false),
                TextSecondary = theme.TextSecondary.EnsureContrastAgainst(surfaces, SecondaryContrast, towardWhite: false),
                TextMuted = theme.TextMuted.EnsureContrastAgainst(surfaces, MutedContrast, towardWhite: false)
            };
        }

        var darkBackground = theme.Background.ClampLuminance(0.0, 0.07);
        var darkSurface = theme.Surface.ClampLuminance(0.0, 0.08);
        var darkElevated = theme.Elevated.ClampLuminance(0.0, 0.10);
        var darkGlass = theme.Glass.ClampLuminance(0.0, 0.09);
        var darkSurfaces = new[] { darkBackground, darkSurface, darkElevated };

        return theme with
        {
            Background = darkBackground,
            Surface = darkSurface,
            Elevated = darkElevated,
            Glass = darkGlass.WithAlpha(theme.Glass.A),
            TextPrimary = theme.TextPrimary.EnsureContrastAgainst(darkSurfaces, PrimaryContrast, towardWhite: true),
            TextSecondary = theme.TextSecondary.EnsureContrastAgainst(darkSurfaces, SecondaryContrast, towardWhite: true),
            TextMuted = theme.TextMuted.EnsureContrastAgainst(darkSurfaces, MutedContrast, towardWhite: true)
        };
    }

    public static GlazeColor ParseHex(string? hex, GlazeColor fallback) => GlazeColor.ParseHex(hex, fallback);

    public static GlazeColor Mix(GlazeColor from, GlazeColor to, double amount) => GlazeColor.Mix(from, to, amount);

    public static GlazeColor Lighten(GlazeColor color, double amount) => GlazeColor.Lighten(color, amount);

    public static GlazeColor OnAccent(GlazeColor accent) => GlazeColor.OnAccent(accent);
}
