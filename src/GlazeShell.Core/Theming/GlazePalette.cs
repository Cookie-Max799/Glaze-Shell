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
    /// Прозрачность панелей по умолчанию: 35 % для верхней панели (значение DESIGN §4,
    /// обновлённое после перехода на настоящий прозрачный backdrop).
    /// </summary>
    public const int DefaultTransparency = UserSettings.DefaultTopBarTransparency;

    /// <summary>
    /// Предел прозрачности, который пользователь может выставить ползунком. Дальше панель
    /// почти сливается с фоном, и подписи теряют читаемость.
    /// </summary>
    public const int MaxTransparency = UserSettings.MaxTransparency;

    /// <summary>Прозрачность акцентной рамки поверхностей.</summary>
    public const byte BorderAlpha = 0x40;

    private const double PrimaryContrast = 7.0;
    private const double SecondaryContrast = 4.5;
    private const double MutedContrast = 4.5;
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
    /// Значение ограничивается <see cref="MaxTransparency"/>.
    /// </param>
    public static GlazeTheme Create(
        bool light,
        string? baseColorId,
        string? accentId,
        int glassTransparency = DefaultTransparency)
    {
        var theme = TintWithBase(light ? Light : Dark, baseColorId);
        theme = EnsureReadable(theme);

        var accent = ResolveAccent(accentId, theme);
        var surfaces = new[] { theme.Background, theme.Surface, theme.Elevated, theme.Glass };
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
    /// <see cref="MaxTransparency"/>, чтобы панель не растворялась целиком.
    /// </summary>
    public static byte GlassAlphaFor(int transparency)
    {
        var limited = Math.Clamp(transparency, 0, MaxTransparency);

        return (byte)Math.Round(255 * (1 - (limited / 100.0)));
    }

    /// <summary>
    /// Предел прозрачности всплывающих панелей верхней панели (popover «Центр
    /// управления», календарь и результаты Glance). Панель может следовать за ползунком,
    /// но не становится прозрачнее этого значения: иначе её содержимое теряется на пёстром
    /// рабочем столе, а разница с полосой превращается в заметную границу.
    /// </summary>
    public const int OverlayMaxTransparency = 70;

    /// <summary>
    /// Альфа-канал всплывающих панелей: та же прозрачность, что у верхней панели, но не
    /// выше <see cref="OverlayMaxTransparency"/>. При плотной полосе панели остаются
    /// такими же плотными, при почти прозрачной — не прозрачнее 70 %.
    /// </summary>
    public static byte OverlayAlphaFor(int transparency)
    {
        return GlassAlphaFor(Math.Min(transparency, OverlayMaxTransparency));
    }

    /// <summary>
    /// Альфа-канал для поверхностей внутри верхней панели (pill, чип сочетания клавиш).
    /// На этих плашках лежат подписи и иконки, поэтому плотность не опускается ниже
    /// <see cref="SurfaceAlphaFloor"/>: панель остаётся прозрачной, а текст читается
    /// поверх любого рабочего стола. При небольшой прозрачности плашки выглядят как раньше.
    /// </summary>
    public static byte SurfaceAlphaFor(int transparency)
    {
        var limited = Math.Clamp(transparency, 0, MaxTransparency);
        var alpha = (byte)Math.Round(255 * (1 - (0.9 * limited / 100.0)));

        return Math.Max(alpha, SurfaceAlphaFloor);
    }

    /// <summary>
    /// Нижний предел плотности плашек верхней панели — 55 % непрозрачности. Полоса
    /// при этом сохраняет выбранную прозрачность: плотнее становится только подложка
    /// под подписями.
    /// </summary>
    public const byte SurfaceAlphaFloor = 140;

    /// <summary>
    /// Порог, выше которого стеклянная поверхность считается светлой и требует тёмных
    /// подписей. Ниже порога подписи светлые.
    /// </summary>
    public const double GlassLightThreshold = 0.2;

    /// <summary>
    /// Светлая ли стеклянная панель после смешения с обоями. Прозрачность настоящая,
    /// поэтому при высоком ползунке панель наследует яркость рабочего стола: на светлых
    /// обоях подписи должны стать тёмными, иначе белый текст на них не читается.
    /// </summary>
    /// <param name="theme">Действующая тема: важен цвет её стекла.</param>
    /// <param name="transparency">Прозрачность панели в процентах.</param>
    /// <param name="wallpaperLuminance">Относительная яркость обоев (0–1).</param>
    public static bool GlassIsLight(GlazeTheme theme, int transparency, double wallpaperLuminance)
    {
        var limited = Math.Clamp(transparency, 0, MaxTransparency);
        var transmission = limited / 100.0;

        // Тонирование Acrylic и заливка панели накладываются друг на друга, поэтому
        // обои просвечивают дважды: на 90 % остаётся около 81 % их яркости.
        var wallpaperWeight = transmission * transmission;
        var backdrop = (wallpaperWeight * Math.Clamp(wallpaperLuminance, 0, 1))
            + ((1 - wallpaperWeight) * theme.Glass.RelativeLuminance);

        return backdrop > GlassLightThreshold;
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
            var surfaces = new[] { background, surface, elevated, glass };

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
        var darkSurfaces = new[] { darkBackground, darkSurface, darkElevated, darkGlass };

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
