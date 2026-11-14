using GlazeShell.Core.Models;
using GlazeShell.Core.Theming;

namespace GlazeShell.Core.Tests.Theming;

[TestClass]
public sealed class GlazePaletteTests
{
    [TestMethod]
    public void ContrastRatioMatchesKnownReferenceValues()
    {
        Assert.IsGreaterThan(20.9, GlazeColor.Black.ContrastRatio(GlazeColor.White));
        Assert.AreEqual(1.0, GlazeColor.White.ContrastRatio(GlazeColor.White), 0.0001);
    }

    [TestMethod]
    public void ParseHexAcceptsRgbAndArgb()
    {
        var rgb = GlazeColor.ParseHex("#4C6FFF", GlazeColor.Black);
        var argb = GlazeColor.ParseHex("#804C6FFF", GlazeColor.Black);

        Assert.AreEqual(new GlazeColor(0x4C, 0x6F, 0xFF), rgb);
        Assert.AreEqual(new GlazeColor(0x4C, 0x6F, 0xFF, 0x80), argb);
        Assert.AreEqual(GlazeColor.Black, GlazeColor.ParseHex("not-a-color", GlazeColor.Black));
    }

    [TestMethod]
    public void OnAccentAlwaysReachesAa()
    {
        foreach (var accent in AccentPalette.All)
        {
            var color = GlazeColor.ParseHex(accent.Hex, GlazeColor.Black);
            var text = GlazePalette.OnAccent(color);

            Assert.IsGreaterThanOrEqualTo(
                4.5,
                text.ContrastRatio(color),
                $"{accent.Id}: text on accent is not readable.");
        }
    }

    [TestMethod]
    public void EveryBaseAndAccentCombinationKeepsTextReadable()
    {
        foreach (var light in new[] { true, false })
        {
            foreach (var baseColor in AccentPalette.All)
            {
                foreach (var accent in AccentPalette.All)
                {
                    var theme = GlazePalette.Create(light, baseColor.Id, accent.Id);
                    var surfaces = new[] { theme.Background, theme.Surface, theme.Elevated };
                    var mode = light ? "light" : "dark";
                    var label = $"{mode}, base={baseColor.Id}, accent={accent.Id}";

                    AssertContrast(theme.TextPrimary, surfaces, 7.0, $"Primary text: {label}");
                    AssertContrast(theme.TextSecondary, surfaces, 4.5, $"Secondary text: {label}");
                    AssertContrast(theme.TextMuted, surfaces, 3.0, $"Muted text: {label}");
                    AssertContrast(theme.AccentForeground, surfaces, 4.5, $"Accent foreground: {label}");
                }
            }
        }
    }

    [TestMethod]
    public void DefaultThemeKeepsTextReadable()
    {
        foreach (var light in new[] { true, false })
        {
            var theme = GlazePalette.Create(light, baseColorId: null, accentId: null);
            var surfaces = new[] { theme.Background, theme.Surface, theme.Elevated };
            var mode = light ? "light" : "dark";

            AssertContrast(theme.TextPrimary, surfaces, 7.0, $"Primary text: {mode}");
            AssertContrast(theme.TextSecondary, surfaces, 4.5, $"Secondary text: {mode}");
            AssertContrast(theme.TextMuted, surfaces, 3.0, $"Muted text: {mode}");
            AssertContrast(theme.AccentForeground, surfaces, 4.5, $"Accent foreground: {mode}");
        }
    }

    /// <summary>
    /// Проверяет, что выбранная прозрачность влияет только на альфа-канал стекла, а цвет
    /// и читаемость непрозрачных поверхностей остаются прежними.
    /// </summary>
    [TestMethod]
    public void GlassTransparencyChangesOnlyTheGlassAlpha()
    {
        foreach (var light in new[] { true, false })
        {
            var opaque = GlazePalette.Create(light, baseColorId: null, accentId: null, glassTransparency: 0);
            var defaults = GlazePalette.Create(light, baseColorId: null, accentId: null);
            var translucent = GlazePalette.Create(light, baseColorId: null, accentId: null, glassTransparency: 60);

            Assert.AreEqual(0xFF, opaque.Glass.A, "0 % transparency must be opaque.");
            Assert.AreEqual(
                GlazePalette.GlassAlphaFor(GlazePalette.DefaultTransparency),
                defaults.Glass.A,
                "The default transparency must match the DESIGN glass alpha.");
            Assert.IsLessThan(defaults.Glass.A, translucent.Glass.A, "More transparency must lower the alpha.");

            Assert.AreEqual(opaque.Glass.R, translucent.Glass.R, "Glass colour must not change.");
            Assert.AreEqual(defaults.Background, opaque.Background, "Surfaces must not depend on the glass alpha.");
            Assert.AreEqual(defaults.TextPrimary, translucent.TextPrimary, "Text must not depend on the glass alpha.");
        }
    }

    [TestMethod]
    public void GlassTransparencyIsClampedToTheAllowedRange()
    {
        var tooTransparent = GlazePalette.Create(light: true, baseColorId: null, accentId: null, glassTransparency: 500);
        var negative = GlazePalette.Create(light: true, baseColorId: null, accentId: null, glassTransparency: -40);

        Assert.AreEqual(
            GlazePalette.GlassAlphaFor(GlazePalette.MaxTransparency),
            tooTransparent.Glass.A);
        Assert.AreEqual(0xFF, negative.Glass.A);
    }

    /// <summary>
    /// Всплывающие панели верхней полосы следуют за ползунком, но не становятся
    /// прозрачнее 70 %: иначе их содержимое теряется на пёстром рабочем столе.
    /// </summary>
    [TestMethod]
    public void OverlayAlphaFollowsTheBarButStopsAtTheSeventyPercentLimit()
    {
        Assert.AreEqual(0xFF, GlazePalette.OverlayAlphaFor(0));
        Assert.AreEqual(
            GlazePalette.GlassAlphaFor(40),
            GlazePalette.OverlayAlphaFor(40));
        Assert.AreEqual(
            GlazePalette.GlassAlphaFor(GlazePalette.OverlayMaxTransparency),
            GlazePalette.OverlayAlphaFor(GlazePalette.OverlayMaxTransparency));
        Assert.AreEqual(
            GlazePalette.GlassAlphaFor(GlazePalette.OverlayMaxTransparency),
            GlazePalette.OverlayAlphaFor(GlazePalette.MaxTransparency));
    }

    [TestMethod]
    public void SurfacesKeepThemeLightness()    {
        foreach (var baseColor in AccentPalette.All)
        {
            var light = GlazePalette.Create(light: true, baseColor.Id, accentId: null);
            Assert.IsGreaterThanOrEqualTo(0.64, light.Background.RelativeLuminance, $"Light background: {baseColor.Id}");
            Assert.IsGreaterThanOrEqualTo(0.70, light.Surface.RelativeLuminance, $"Light surface: {baseColor.Id}");
            Assert.IsGreaterThanOrEqualTo(0.66, light.Elevated.RelativeLuminance, $"Light elevated: {baseColor.Id}");

            var dark = GlazePalette.Create(light: false, baseColor.Id, accentId: null);
            Assert.IsLessThanOrEqualTo(0.07, dark.Background.RelativeLuminance, $"Dark background: {baseColor.Id}");
            Assert.IsLessThanOrEqualTo(0.08, dark.Surface.RelativeLuminance, $"Dark surface: {baseColor.Id}");
            Assert.IsLessThanOrEqualTo(0.10, dark.Elevated.RelativeLuminance, $"Dark elevated: {baseColor.Id}");
        }
    }

    /// <summary>
    /// Плашки внутри верхней панели остаются подложкой под подписями при любой прозрачности:
    /// на предельном ползунке они не растворяются в полосе, иначе текст теряется на обоях.
    /// Сама полоса при этом сохраняет выбранную прозрачность.
    /// </summary>
    [TestMethod]
    public void PillsKeepAReadableBackingAtTheTransparencyLimit()
    {
        Assert.AreEqual(255, GlazePalette.SurfaceAlphaFor(0));
        Assert.AreEqual(GlazePalette.SurfaceAlphaFloor, GlazePalette.SurfaceAlphaFor(GlazePalette.MaxTransparency));
        Assert.IsGreaterThan(
            GlazePalette.SurfaceAlphaFor(GlazePalette.MaxTransparency),
            GlazePalette.SurfaceAlphaFor(20),
            "Pills must still follow the slider below the floor.");
        Assert.AreEqual(
            (byte)Math.Round(255 * (1 - (0.9 * GlazePalette.DefaultTransparency / 100.0))),
            GlazePalette.SurfaceAlphaFor(GlazePalette.DefaultTransparency),
            "The floor must not affect the usual range.");
    }

    /// <summary>
    /// Светлота стекла считается по реальному просвечиванию обоев: ползунок прозрачности
    /// не ограничивается, поэтому на светлом рабочем столе тёмная тема получает светлую
    /// панель и тёмные подписи, а на тёмном — тёмную панель и светлые подписи.
    /// </summary>
    [TestMethod]
    public void GlassLightnessFollowsTheWallpaperAtHighTransparency()
    {
        var dark = GlazePalette.Create(light: false, baseColorId: null, accentId: null, glassTransparency: 0);
        var light = GlazePalette.Create(light: true, baseColorId: null, accentId: null, glassTransparency: 0);

        const double lightWallpaper = 0.31;
        const double darkWallpaper = 0.02;

        Assert.IsTrue(
            GlazePalette.GlassIsLight(dark, GlazePalette.MaxTransparency, lightWallpaper),
            "Dark theme over a light wallpaper must switch to dark captions.");
        Assert.IsFalse(
            GlazePalette.GlassIsLight(dark, GlazePalette.MaxTransparency, darkWallpaper),
            "Dark theme over a dark wallpaper keeps light captions.");
        Assert.IsTrue(
            GlazePalette.GlassIsLight(light, GlazePalette.MaxTransparency, darkWallpaper),
            "Light theme over a dark wallpaper stays light because its own glass is light.");
        Assert.IsFalse(
            GlazePalette.GlassIsLight(dark, 0, lightWallpaper),
            "An opaque panel does not depend on the wallpaper.");
    }

    private static void AssertContrast(
        GlazeColor foreground,
        IReadOnlyList<GlazeColor> backgrounds,
        double minimum,
        string message)
    {
        foreach (var background in backgrounds)
        {
            Assert.IsGreaterThanOrEqualTo(minimum, foreground.ContrastRatio(background), message);
        }
    }
}
