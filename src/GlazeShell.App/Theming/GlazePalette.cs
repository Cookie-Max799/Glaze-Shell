using Windows.UI;

namespace GlazeShell.App.Theming;

/// <summary>
/// Значения цветовых токенов Glaze Shell для конкретной темы. Источник — дизайн-система
/// (<c>docs/DESIGN.md</c>, <c>docs/design/tokens/tokens.json</c>).
/// </summary>
internal sealed record GlazeTheme(
    Color Background,
    Color Surface,
    Color Elevated,
    Color Border,
    Color Glass,
    Color GlassRimTop,
    Color GlassRimMiddle,
    Color GlassRimBottom,
    Color GlassGlossTop,
    Color GlassGlossMiddle,
    Color GlassGlossBottom,
    Color TextPrimary,
    Color TextSecondary,
    Color TextMuted,
    Color DefaultAccent);

/// <summary>
/// Светлая и тёмная палитры Glaze Shell.
/// </summary>
internal static class GlazePalette
{
    public static GlazeTheme Light { get; } = new(
        Background: Rgb(0xF6, 0xF4, 0xFF),
        Surface: Rgb(0xFF, 0xFF, 0xFF),
        Elevated: Rgb(0xEC, 0xE8, 0xFF),
        Border: Rgb(0xDD, 0xD8, 0xF5),
        Glass: WithAlpha(Rgb(0xFF, 0xFF, 0xFF), 0x8C),
        GlassRimTop: WithAlpha(Rgb(0xFF, 0xFF, 0xFF), 0xF2),
        GlassRimMiddle: WithAlpha(Rgb(0xFF, 0xFF, 0xFF), 0x4D),
        GlassRimBottom: WithAlpha(Rgb(0xFF, 0xFF, 0xFF), 0x99),
        GlassGlossTop: WithAlpha(Rgb(0xFF, 0xFF, 0xFF), 0x73),
        GlassGlossMiddle: WithAlpha(Rgb(0xFF, 0xFF, 0xFF), 0x1F),
        GlassGlossBottom: WithAlpha(Rgb(0xFF, 0xFF, 0xFF), 0x00),
        TextPrimary: Rgb(0x25, 0x21, 0x4F),
        TextSecondary: Rgb(0x6B, 0x68, 0x94),
        TextMuted: Rgb(0xA5, 0xA2, 0xC8),
        DefaultAccent: Rgb(0x6A, 0x5B, 0xEA));

    public static GlazeTheme Dark { get; } = new(
        Background: Rgb(0x14, 0x12, 0x2B),
        Surface: Rgb(0x1E, 0x1B, 0x3D),
        Elevated: Rgb(0x2A, 0x27, 0x54),
        Border: Rgb(0x3A, 0x36, 0x70),
        Glass: WithAlpha(Rgb(0x22, 0x1E, 0x4A), 0x8C),
        GlassRimTop: WithAlpha(Rgb(0xFF, 0xFF, 0xFF), 0xF0),
        GlassRimMiddle: WithAlpha(Rgb(0xFF, 0xFF, 0xFF), 0x2E),
        GlassRimBottom: WithAlpha(Rgb(0xFF, 0xFF, 0xFF), 0x6E),
        GlassGlossTop: WithAlpha(Rgb(0xFF, 0xFF, 0xFF), 0x3D),
        GlassGlossMiddle: WithAlpha(Rgb(0xFF, 0xFF, 0xFF), 0x12),
        GlassGlossBottom: WithAlpha(Rgb(0xFF, 0xFF, 0xFF), 0x00),
        TextPrimary: Rgb(0xF4, 0xF1, 0xFF),
        TextSecondary: Rgb(0xB2, 0xAE, 0xD8),
        TextMuted: Rgb(0x77, 0x73, 0xA8),
        DefaultAccent: Rgb(0xA9, 0x9B, 0xFF));

    public static Color Rgb(byte red, byte green, byte blue) => Color.FromArgb(0xFF, red, green, blue);

    public static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    /// <summary>
    /// Разбирает цвет вида <c>#RRGGBB</c> или <c>#AARRGGBB</c>. Некорректная строка
    /// возвращает <paramref name="fallback"/>.
    /// </summary>
    public static Color ParseHex(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        var value = hex.Trim().TrimStart('#');

        if (value.Length == 6)
        {
            value = "FF" + value;
        }

        if (value.Length != 8 || !uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var packed))
        {
            return fallback;
        }

        return Color.FromArgb(
            (byte)((packed >> 24) & 0xFF),
            (byte)((packed >> 16) & 0xFF),
            (byte)((packed >> 8) & 0xFF),
            (byte)(packed & 0xFF));
    }
}
