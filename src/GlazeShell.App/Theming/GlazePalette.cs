using GlazeShell.Core.Theming;
using Windows.UI;
using CorePalette = GlazeShell.Core.Theming.GlazePalette;
using CoreTheme = GlazeShell.Core.Theming.GlazeTheme;

namespace GlazeShell.App.Theming;

/// <summary>
/// Значения цветовых токенов Glaze Shell для конкретной темы в типах WinUI.
/// </summary>
internal sealed record GlazeTheme(
    Color Background,
    Color Surface,
    Color Elevated,
    Color Border,
    Color Glass,
    Color TextPrimary,
    Color TextSecondary,
    Color TextMuted,
    Color DefaultAccent,
    Color AccentForeground);

/// <summary>
/// Переходник между framework-free палитрой ядра и WinUI: ядро считает цвета и контраст,
/// приложение только переводит их в <see cref="Color"/> и перекрашивает кисти.
/// </summary>
internal static class GlazePalette
{
    public static GlazeTheme Create(bool light, string? baseColorId, string? accentId, int glassTransparency) =>
        Convert(CorePalette.Create(light, baseColorId, accentId, glassTransparency));

    public static Color ParseHex(string? hex, Color fallback) =>
        ToColor(GlazeColor.ParseHex(hex, ToCore(fallback)));

    public static Color WithAlpha(Color color, byte alpha) => ToColor(ToCore(color).WithAlpha(alpha));

    public static byte GlassAlphaFor(int transparency) => CorePalette.GlassAlphaFor(transparency);

    public static byte SurfaceAlphaFor(int transparency) => CorePalette.SurfaceAlphaFor(transparency);

    public static byte OverlayAlphaFor(int transparency) => CorePalette.OverlayAlphaFor(transparency);

    public static Color Lighten(Color color, double amount) => ToColor(GlazeColor.Lighten(ToCore(color), amount));

    public static Color OnAccent(Color accent) => ToColor(GlazeColor.OnAccent(ToCore(accent)));

    private static GlazeTheme Convert(CoreTheme theme) => new(
        ToColor(theme.Background),
        ToColor(theme.Surface),
        ToColor(theme.Elevated),
        ToColor(theme.Border),
        ToColor(theme.Glass),
        ToColor(theme.TextPrimary),
        ToColor(theme.TextSecondary),
        ToColor(theme.TextMuted),
        ToColor(theme.DefaultAccent),
        ToColor(theme.AccentForeground));

    private static Color ToColor(GlazeColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private static GlazeColor ToCore(Color color) => new(color.R, color.G, color.B, color.A);
}
