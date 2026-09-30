using GlazeShell.Core.Events;
using GlazeShell.Core.Models;
using GlazeShell.Core.Services;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class ThemeManagerTests
{
    private static readonly string[] ExpectedThemeOrder = ["alpine", "sunset"];

    [TestMethod]
    public void WithoutThemesTheBuiltInThemeIsActive()
    {
        var manager = new ThemeManager();

        Assert.AreEqual(Theme.DefaultThemeId, manager.GetActiveTheme().Id);
        Assert.IsEmpty(manager.GetThemes());
    }

    [TestMethod]
    public void ThemesAreOrderedByNameRegardlessOfInputOrder()
    {
        var manager = new ThemeManager(themes: [CreateTheme("sunset", "Sunset"), CreateTheme("alpine", "Alpine")]);

        CollectionAssert.AreEqual(ExpectedThemeOrder, manager.GetThemes().Select(theme => theme.Id).ToArray());
    }

    [TestMethod]
    public void DuplicatedIdentifiersKeepTheFirstTheme()
    {
        var manager = new ThemeManager(themes: [CreateTheme("night", "Night"), CreateTheme("NIGHT", "Other Night")]);

        Assert.HasCount(1, manager.GetThemes());
        Assert.AreEqual("Night", manager.GetThemes()[0].Name);
    }

    [TestMethod]
    public void GetThemeIgnoresCaseAndRejectsEmptyIdentifier()
    {
        var manager = new ThemeManager(themes: [CreateTheme("midnight", "Midnight")]);

        Assert.AreEqual("Midnight", manager.GetTheme("MIDNIGHT")!.Name);
        Assert.IsNull(manager.GetTheme("absent"));
        Assert.ThrowsExactly<ArgumentException>(() => manager.GetTheme(" "));
    }

    [TestMethod]
    public void ConfiguredThemeIsSelectedAtStartup()
    {
        var manager = new ThemeManager(themes: [CreateTheme("midnight", "Midnight")], activeThemeId: "midnight");

        Assert.AreEqual("midnight", manager.GetActiveTheme().Id);
    }

    [TestMethod]
    public void UnknownConfiguredThemeFallsBackToBuiltInTheme()
    {
        var manager = new ThemeManager(themes: [CreateTheme("midnight", "Midnight")], activeThemeId: "absent");

        Assert.AreEqual(Theme.DefaultThemeId, manager.GetActiveTheme().Id);
    }

    [TestMethod]
    public void SetActiveThemePublishesChangeWithPreviousIdentifier()
    {
        var events = new EventManager();
        var published = new List<ThemeChanged>();
        using var subscription = events.Subscribe<ThemeChanged>(published.Add);

        var manager = new ThemeManager(events, [CreateTheme("midnight", "Midnight")]);
        Assert.IsTrue(manager.SetActiveTheme("midnight"));

        Assert.HasCount(1, published);
        Assert.AreEqual("midnight", published[0].Theme.Id);
        Assert.AreEqual(Theme.DefaultThemeId, published[0].PreviousThemeId);
    }

    [TestMethod]
    public void SelectingTheSameThemeDoesNotPublishChange()
    {
        var events = new EventManager();
        var published = new List<ThemeChanged>();
        using var subscription = events.Subscribe<ThemeChanged>(published.Add);

        var manager = new ThemeManager(events, [CreateTheme("midnight", "Midnight")], activeThemeId: "midnight");
        Assert.IsTrue(manager.SetActiveTheme("midnight"));

        Assert.IsEmpty(published);
    }

    [TestMethod]
    public void UnknownThemeKeepsTheActiveTheme()
    {
        var events = new EventManager();
        var published = new List<ThemeChanged>();
        using var subscription = events.Subscribe<ThemeChanged>(published.Add);

        var manager = new ThemeManager(events, [CreateTheme("midnight", "Midnight")], activeThemeId: "midnight");

        Assert.IsFalse(manager.SetActiveTheme("absent"));
        Assert.AreEqual("midnight", manager.GetActiveTheme().Id);
        Assert.IsEmpty(published);
    }

    [TestMethod]
    public void NullIdentifierReturnsToTheBuiltInTheme()
    {
        var events = new EventManager();
        var manager = new ThemeManager(events, [CreateTheme("midnight", "Midnight")], activeThemeId: "midnight");

        Assert.IsTrue(manager.SetActiveTheme(null));
        Assert.AreEqual(Theme.DefaultThemeId, manager.GetActiveTheme().Id);

        Assert.IsTrue(manager.SetActiveTheme("  "));
        Assert.AreEqual(Theme.DefaultThemeId, manager.GetActiveTheme().Id);
    }

    [TestMethod]
    public void ReplaceThemesKeepsTheActiveThemeWhenItSurvives()
    {
        var events = new EventManager();
        var published = new List<ThemeChanged>();
        using var subscription = events.Subscribe<ThemeChanged>(published.Add);

        var manager = new ThemeManager(events, [CreateTheme("midnight", "Midnight")], activeThemeId: "midnight");

        Assert.IsFalse(manager.ReplaceThemes([CreateTheme("midnight", "Midnight v2"), CreateTheme("dawn", "Dawn")]));
        Assert.AreEqual("midnight", manager.GetActiveTheme().Id);
        Assert.IsEmpty(published);
    }

    [TestMethod]
    public void ReplaceThemesSelectsTheFirstThemeWhenTheActiveOneDisappears()
    {
        var events = new EventManager();
        var published = new List<ThemeChanged>();
        using var subscription = events.Subscribe<ThemeChanged>(published.Add);

        var manager = new ThemeManager(events, [CreateTheme("midnight", "Midnight")], activeThemeId: "midnight");

        Assert.IsTrue(manager.ReplaceThemes([CreateTheme("dawn", "Dawn")]));
        Assert.AreEqual("dawn", manager.GetActiveTheme().Id);
        Assert.HasCount(1, published);
        Assert.AreEqual("midnight", published[0].PreviousThemeId);
    }

    [TestMethod]
    public void ReplaceThemesFallsBackToTheBuiltInThemeWhenTheSetIsEmptied()
    {
        var manager = new ThemeManager(themes: [CreateTheme("midnight", "Midnight")], activeThemeId: "midnight");

        Assert.IsTrue(manager.ReplaceThemes([]));
        Assert.AreEqual(Theme.DefaultThemeId, manager.GetActiveTheme().Id);
        Assert.IsEmpty(manager.GetThemes());
    }

    [TestMethod]
    public void ReplaceThemesAppliesAnExplicitIdentifier()
    {
        var manager = new ThemeManager(themes: [CreateTheme("midnight", "Midnight")], activeThemeId: "midnight");

        Assert.IsTrue(manager.ReplaceThemes([CreateTheme("dawn", "Dawn"), CreateTheme("midnight", "Midnight")], "dawn"));
        Assert.AreEqual("dawn", manager.GetActiveTheme().Id);
    }

    [TestMethod]
    public void GetThemesReturnsTheSameImmutableSnapshot()
    {
        var manager = new ThemeManager(themes: [CreateTheme("midnight", "Midnight")]);

        var first = manager.GetThemes();
        var second = manager.GetThemes();

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void BuiltInThemeDefinesTheColorsUiReliesOn()
    {
        var theme = Theme.CreateDefault();

        foreach (var name in new[]
                 {
                     "layerBackground",
                     "layerText",
                     "accentBackground",
                     "accentText"
                 })
        {
            Assert.IsNotNull(theme.Colors.Find(name), $"The built-in theme must define the color '{name}'.");
        }
    }

    [TestMethod]
    public void ColorLookupIgnoresCaseAndReturnsNullForUnknownName()
    {
        var colors = new ThemeColors([new ThemeColor("AccentBackground", "#FF0F6CBD")]);

        Assert.AreEqual("#FF0F6CBD", colors.Find("accentbackground")!.Value);
        Assert.IsNull(colors.Find("missing"));
    }

    [TestMethod]
    public void DuplicatedColorNamesAreRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new ThemeColors(
            [new ThemeColor("accent", "#FF000000"), new ThemeColor("ACCENT", "#FFFFFFFF")]));
    }

    [TestMethod]
    public void ThemeAssetPathMustStayInsideTheThemeDirectory()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new ThemeIcon("shell", "../outside.png"));
        Assert.ThrowsExactly<ArgumentException>(() => new ThemeIcon("shell", @"C:\Windows\System32\shell32.dll"));
        Assert.ThrowsExactly<ArgumentException>(() => new ThemeIcon("shell", @"\\server\share\icon.png"));
        Assert.ThrowsExactly<ArgumentException>(() => new ThemeIcon("shell", "ms-appx:///Assets/icon.png"));
    }

    [TestMethod]
    public void ThemeAssetPathMayBeRelativeToTheTheme()
    {
        var icon = new ThemeIcon("shell", "icons/shell.png");

        Assert.AreEqual("icons/shell.png", icon.Source);
    }

    [TestMethod]
    public void ThemeRejectsScriptsAndLoaders()
    {
        foreach (var source in new[] { "run.ps1", "run.cmd", "run.vbs", "run.js", "start.lnk", "setup.msi" })
        {
            Assert.ThrowsExactly<ArgumentException>(() => new ThemeIcon("shell", source), source);
        }
    }

    [TestMethod]
    public void WallpaperRejectsExecutableAssetsAndEscapingPaths()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new ThemeWallpaper("wallpaper.exe"));
        Assert.ThrowsExactly<ArgumentException>(() => new ThemeWallpaper("../../windows/wallpaper.jpg"));
    }

    private static Theme CreateTheme(string id, string name) => new(
        id,
        name,
        new ThemeMetadata(name, "1.0"),
        new ThemeColors([new ThemeColor("accent", "#FF0F6CBD")]),
        new ThemeFonts(),
        new ThemeDimensions(),
        new ThemeIcons());
}
