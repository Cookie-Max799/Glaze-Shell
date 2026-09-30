using GlazeShell.Core.Models;
using GlazeShell.Data.Serialization;
using GlazeShell.Data.Themes;

namespace GlazeShell.Data.Tests;

[TestClass]
public sealed class ThemeStoreTests
{
    private static readonly string[] NameOrder = ["second-id", "first-id"];

    private static readonly string[] SurvivingThemes = ["valid-one", "valid-two"];

    [TestMethod]
    public void MissingThemeDirectoryFallsBackToTheBuiltInTheme()
    {
        using var temp = new TempUserData(createThemesDirectory: false);

        var result = new ThemeStore(temp.Root).LoadThemes();

        Assert.IsEmpty(result.Themes);
        Assert.IsTrue(result.UsedFallback);
        Assert.HasCount(1, result.Diagnostics);
    }

    [TestMethod]
    public void ThemeFileIsLoadedIntoTheModel()
    {
        using var temp = new TempUserData();
        temp.WriteTheme("midnight.json", """
            {
              "id": "midnight",
              "name": "Midnight",
              "metadata": { "name": "Midnight", "version": "1.0", "author": "user" },
              "colors": [
                { "name": "accentBackground", "value": "#FF102030" }
              ],
              "fonts": [
                { "family": "Segoe UI", "size": 14, "weight": "Normal" }
              ],
              "dimensions": { "cornerRadius": 4, "spacing": 6, "iconSize": 20, "titleBarHeight": 30 },
              "icons": [
                { "name": "shell", "source": "icons/shell.png" }
              ],
              "wallpaper": { "source": "wall.jpg", "fit": "Fill" },
              "effects": { "animationsEnabled": true, "animationDurationMilliseconds": 120, "opacity": 0.9 }
            }
            """);

        var result = new ThemeStore(temp.Root).LoadThemes();

        Assert.HasCount(1, result.Themes);
        Assert.IsEmpty(result.Diagnostics);
        Assert.IsFalse(result.UsedFallback);

        var theme = result.Themes[0];
        Assert.AreEqual("midnight", theme.Id);
        Assert.AreEqual("Midnight", theme.Metadata.Name);
        Assert.AreEqual("user", theme.Metadata.Author);
        Assert.AreEqual("#FF102030", theme.Colors.Find("accentBackground")!.Value);
        Assert.AreEqual("Segoe UI", theme.Fonts.Fonts[0].Family);
        Assert.AreEqual(14d, theme.Fonts.Fonts[0].Size);
        Assert.AreEqual(4d, theme.Dimensions.CornerRadius);
        Assert.AreEqual(30, theme.Dimensions.TitleBarHeight);
        Assert.AreEqual("icons/shell.png", theme.Icons.Icons[0].Source);
        Assert.AreEqual(WallpaperFit.Fill, theme.Wallpaper!.Fit);
        Assert.AreEqual(120, theme.Effects.AnimationDurationMilliseconds);
        Assert.AreEqual(0.9d, theme.Effects.Opacity);
    }

    [TestMethod]
    public void OptionalSectionsFallBackToModelDefaults()
    {
        using var temp = new TempUserData();
        temp.WriteTheme("minimal.json", """
            {
              "id": "minimal",
              "name": "Minimal",
              "metadata": { "name": "Minimal", "version": "1.0" }
            }
            """);

        var result = new ThemeStore(temp.Root).LoadThemes();

        Assert.HasCount(1, result.Themes);
        Assert.IsEmpty(result.Diagnostics);

        var theme = result.Themes[0];
        Assert.IsNull(theme.Wallpaper);
        Assert.IsEmpty(theme.Colors.Colors);
        Assert.AreEqual(8d, theme.Dimensions.CornerRadius);
        Assert.AreEqual(150, theme.Effects.AnimationDurationMilliseconds);
    }

    [TestMethod]
    public void ThemesAreOrderedByNameNotByFileName()
    {
        using var temp = new TempUserData();
        temp.WriteTheme("z-file.json", Theme("first-id", "Zulu"));
        temp.WriteTheme("a-file.json", Theme("second-id", "Alpha"));

        var result = new ThemeStore(temp.Root).LoadThemes();

        CollectionAssert.AreEqual(NameOrder, result.Themes.Select(theme => theme.Id).ToArray());
    }

    [TestMethod]
    public void MalformedJsonIsSkippedAndReported()
    {
        using var temp = new TempUserData();
        temp.WriteTheme("broken.json", "{ this is not json }");
        temp.WriteTheme("valid.json", Theme("valid", "Valid"));

        var result = new ThemeStore(temp.Root).LoadThemes();

        Assert.HasCount(1, result.Themes);
        Assert.AreEqual("valid", result.Themes[0].Id);
        Assert.HasCount(1, result.Diagnostics);
        StringAssert.Contains(result.Diagnostics[0], "broken.json");
    }

    [TestMethod]
    public void ThemeWithoutIdentifierIsRejected()
    {
        using var temp = new TempUserData();
        temp.WriteTheme("no-id.json", """
            {
              "name": "No Id",
              "metadata": { "name": "No Id", "version": "1.0" }
            }
            """);

        var result = new ThemeStore(temp.Root).LoadThemes();

        Assert.IsEmpty(result.Themes);
        Assert.HasCount(1, result.Diagnostics);
        StringAssert.Contains(result.Diagnostics[0], "invalid identifier");
    }

    [TestMethod]
    public void ExecutableIconIsRejected()
    {
        using var temp = new TempUserData();
        temp.WriteTheme("dangerous.json", """
            {
              "id": "dangerous",
              "name": "Dangerous",
              "metadata": { "name": "Dangerous", "version": "1.0" },
              "icons": [ { "name": "payload", "source": "payload.dll" } ]
            }
            """);

        var result = new ThemeStore(temp.Root).LoadThemes();

        Assert.IsEmpty(result.Themes);
        Assert.HasCount(1, result.Diagnostics);
        StringAssert.Contains(result.Diagnostics[0], "not allowed");
    }

    [TestMethod]
    public void IconEscapingTheThemeDirectoryIsRejected()
    {
        using var temp = new TempUserData();
        temp.WriteTheme("escape.json", """
            {
              "id": "escape",
              "name": "Escape",
              "metadata": { "name": "Escape", "version": "1.0" },
              "icons": [ { "name": "shell", "source": "../../secrets/icon.png" } ]
            }
            """);

        var result = new ThemeStore(temp.Root).LoadThemes();

        Assert.IsEmpty(result.Themes);
        Assert.HasCount(1, result.Diagnostics);
    }

    [TestMethod]
    public void ColorMustBeAHexValue()
    {
        using var temp = new TempUserData();
        temp.WriteTheme("color.json", """
            {
              "id": "color",
              "name": "Color",
              "metadata": { "name": "Color", "version": "1.0" },
              "colors": [ { "name": "accent", "value": "rgb(1,2,3)" } ]
            }
            """);

        var result = new ThemeStore(temp.Root).LoadThemes();

        Assert.IsEmpty(result.Themes);
        Assert.HasCount(1, result.Diagnostics);
        StringAssert.Contains(result.Diagnostics[0], "#RGB");
    }

    [TestMethod]
    public void DuplicatedIdentifiersAcrossFilesAreSkipped()
    {
        using var temp = new TempUserData();
        temp.WriteTheme("a-first.json", Theme("shared", "First"));
        temp.WriteTheme("b-second.json", Theme("SHARED", "Second"));

        var result = new ThemeStore(temp.Root).LoadThemes();

        Assert.HasCount(1, result.Themes);
        Assert.AreEqual("First", result.Themes[0].Name);
        Assert.HasCount(1, result.Diagnostics);
        StringAssert.Contains(result.Diagnostics[0], "duplicates the identifier");
    }

    [TestMethod]
    public void OversizedThemeFileIsNotRead()
    {
        using var temp = new TempUserData();
        var padding = new string('a', (int)PersistenceLimits.MaxThemeBytes);
        temp.WriteTheme("huge.json", $$"""
            {
              "id": "huge",
              "name": "Huge",
              "metadata": { "name": "Huge", "version": "1.0", "description": "{{padding}}" }
            }
            """);

        var result = new ThemeStore(temp.Root).LoadThemes();

        Assert.IsEmpty(result.Themes);
        Assert.HasCount(1, result.Diagnostics);
        StringAssert.Contains(result.Diagnostics[0], "the limit is");
    }

    [TestMethod]
    public void TooManyThemesAreNotRead()
    {
        using var temp = new TempUserData();
        for (var index = 0; index <= PersistenceLimits.MaxThemes; index++)
        {
            temp.WriteTheme($"theme-{index:D3}.json", Theme($"theme-{index:D3}", $"Theme {index:D3}"));
        }

        var result = new ThemeStore(temp.Root).LoadThemes();

        Assert.HasCount(PersistenceLimits.MaxThemes, result.Themes);
        StringAssert.Contains(result.Diagnostics[0], "more than the limit");
    }

    [TestMethod]
    public void NonJsonFilesAreIgnored()
    {
        using var temp = new TempUserData();
        temp.WriteTheme("notes.txt", "not a theme");
        temp.WriteTheme("script.json.bak", "not a theme either");
        temp.WriteTheme("valid.json", Theme("valid", "Valid"));

        var result = new ThemeStore(temp.Root).LoadThemes();

        Assert.HasCount(1, result.Themes);
        Assert.IsEmpty(result.Diagnostics);
    }

    [TestMethod]
    public void PropertyNamesAreCaseSensitive()
    {
        using var temp = new TempUserData();
        temp.WriteTheme("case.json", """
            {
              "Id": "cased",
              "Name": "Cased",
              "Metadata": { "name": "Cased", "version": "1.0" }
            }
            """);

        var result = new ThemeStore(temp.Root).LoadThemes();

        // Имена свойств с другим регистром не совпадают с полями модели,
        // поэтому документ читается как пустой и отклоняется, а не применяется частично.
        Assert.IsEmpty(result.Themes);
        Assert.HasCount(3, result.Diagnostics);
        StringAssert.Contains(string.Join(" ", result.Diagnostics), "invalid identifier");
        StringAssert.Contains(string.Join(" ", result.Diagnostics), "no metadata");
    }

    [TestMethod]
    public void UnknownWallpaperFitIsRejected()
    {
        using var temp = new TempUserData();
        temp.WriteTheme("fit.json", """
            {
              "id": "fit",
              "name": "Fit",
              "metadata": { "name": "Fit", "version": "1.0" },
              "wallpaper": { "source": "wall.jpg", "fit": "Stretchy" }
            }
            """);

        var result = new ThemeStore(temp.Root).LoadThemes();

        Assert.IsEmpty(result.Themes);
        Assert.HasCount(1, result.Diagnostics);
    }

    [TestMethod]
    public void NegativeDimensionsAreRejected()
    {
        using var temp = new TempUserData();
        temp.WriteTheme("negative.json", """
            {
              "id": "negative",
              "name": "Negative",
              "metadata": { "name": "Negative", "version": "1.0" },
              "dimensions": { "cornerRadius": -4 }
            }
            """);

        var result = new ThemeStore(temp.Root).LoadThemes();

        Assert.IsEmpty(result.Themes);
        Assert.HasCount(1, result.Diagnostics);
    }

    [TestMethod]
    public void ThemesDirectoryMustBeAnAbsolutePath()
    {
        using var temp = new TempUserData();

        Assert.ThrowsExactly<ArgumentException>(() => new ThemeStore("relative/path"));
        Assert.ThrowsExactly<ArgumentException>(() => new ThemeStore("  "));
    }

    [TestMethod]
    public void OneBrokenThemeDoesNotDiscardTheOthers()
    {
        using var temp = new TempUserData();
        temp.WriteTheme("a-valid.json", Theme("valid-one", "Valid One"));
        temp.WriteTheme("b-broken.json", "{ not json");
        temp.WriteTheme("c-invalid-id.json", Theme(string.Empty, "Invalid Id"));
        temp.WriteTheme("d-valid.json", Theme("valid-two", "Valid Two"));

        var result = new ThemeStore(temp.Root).LoadThemes();

        CollectionAssert.AreEqual(SurvivingThemes, result.Themes.Select(theme => theme.Id).ToArray());
        Assert.HasCount(2, result.Diagnostics);
    }

    private static string Theme(string id, string name) => $$"""
        {
          "id": "{{id}}",
          "name": "{{name}}",
          "metadata": { "name": "{{name}}", "version": "1.0" }
        }
        """;
}
