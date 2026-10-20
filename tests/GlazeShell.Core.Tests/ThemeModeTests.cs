using GlazeShell.Core.Models;
using GlazeShell.Core.Services;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class ThemeModeTests
{
    [TestMethod]
    public void ToIdCoversAllModes()
    {
        Assert.AreEqual("system", ThemeMode.System.ToId());
        Assert.AreEqual("light", ThemeMode.Light.ToId());
        Assert.AreEqual("dark", ThemeMode.Dark.ToId());
    }

    [TestMethod]
    public void TryParseIsCaseAndWhitespaceInsensitive()
    {
        Assert.IsTrue(ThemeModeExtensions.TryParse(" Light ", out var light));
        Assert.AreEqual(ThemeMode.Light, light);

        Assert.IsTrue(ThemeModeExtensions.TryParse("DARK", out var dark));
        Assert.AreEqual(ThemeMode.Dark, dark);

        Assert.IsTrue(ThemeModeExtensions.TryParse("system", out var system));
        Assert.AreEqual(ThemeMode.System, system);
    }

    [TestMethod]
    public void TryParseRejectsUnknownValues()
    {
        Assert.IsFalse(ThemeModeExtensions.TryParse("blue", out var mode));
        Assert.AreEqual(ThemeMode.System, mode);

        Assert.IsFalse(ThemeModeExtensions.TryParse(null, out _));
        Assert.IsFalse(ThemeModeExtensions.TryParse("   ", out _));
    }

    [TestMethod]
    public void ResolvePassesThroughExplicitModes()
    {
        Assert.AreEqual(ThemeMode.Light, ThemeModeResolver.Resolve(ThemeMode.Light, systemUsesLightTheme: false));
        Assert.AreEqual(ThemeMode.Dark, ThemeModeResolver.Resolve(ThemeMode.Dark, systemUsesLightTheme: true));
    }

    [TestMethod]
    public void ResolveFollowsSystemForSystemMode()
    {
        Assert.AreEqual(ThemeMode.Light, ThemeModeResolver.Resolve(ThemeMode.System, systemUsesLightTheme: true));
        Assert.AreEqual(ThemeMode.Dark, ThemeModeResolver.Resolve(ThemeMode.System, systemUsesLightTheme: false));
    }

    [TestMethod]
    public void AccentPaletteFindsKnownAccents()
    {
        Assert.HasCount(6, AccentPalette.All);

        var accent = AccentPalette.Find("MINT");

        Assert.IsNotNull(accent);
        Assert.AreEqual("#3FCF9B", accent.Hex);
        Assert.IsTrue(AccentPalette.IsKnown("slate"));
        Assert.IsFalse(AccentPalette.IsKnown("neon"));
        Assert.IsFalse(AccentPalette.IsKnown(null));
        Assert.IsNull(AccentPalette.Find(""));
    }
}
