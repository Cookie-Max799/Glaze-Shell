using System;
using System.Linq;
using GlazeShell.Core.Models;
using GlazeShell.Core.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class SettingsCatalogTests
{
    [TestMethod]
    public void PagesAreUniqueAndHaveTitles()
    {
        var ids = SettingsCatalog.Pages.Select(page => page.Id).ToArray();

        Assert.HasCount(ids.Length, ids.Distinct(StringComparer.Ordinal).ToArray(), "Page ids must be unique.");
        Assert.IsTrue(
            SettingsCatalog.Pages.All(page => !string.IsNullOrWhiteSpace(page.Title)),
            "Every page needs a title for the sidebar.");
    }

    [TestMethod]
    public void EverySettingBelongsToAKnownPage()
    {
        var pageIds = SettingsCatalog.Pages.Select(page => page.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var definition in SettingsCatalog.All)
        {
            Assert.Contains(
                definition.PageId,
                pageIds,
                $"Setting '{definition.Id}' points at an unknown page '{definition.PageId}'.");
        }
    }

    [TestMethod]
    public void SettingIdsAreUnique()
    {
        var ids = SettingsCatalog.All.Select(definition => definition.Id).ToArray();

        Assert.HasCount(ids.Length, ids.Distinct(StringComparer.Ordinal).ToArray(), "Setting ids must be unique.");
    }

    [TestMethod]
    public void ChoiceSettingsProvideChoices()
    {
        foreach (var definition in SettingsCatalog.All.Where(item => item.Kind == SettingKind.Choice))
        {
            Assert.IsNotEmpty(definition.Choices, $"Choice setting '{definition.Id}' has no choices.");
        }
    }

    [TestMethod]
    public void EmptyQueryReturnsEverySetting()
    {
        Assert.HasCount(SettingsCatalog.All.Count, SettingsCatalog.Search("   "));
    }

    [TestMethod]
    public void SearchMatchesTitleAndKeywordsCaseInsensitively()
    {
        var byTitle = SettingsCatalog.Search("DOCK");
        var byKeyword = SettingsCatalog.Search("taskbar");

        Assert.Contains(
            SettingsCatalog.All.First(definition => definition.Id == "dockEnabled"),
            byTitle,
            "Search must match the page title case-insensitively.");
        Assert.IsTrue(
            byKeyword.Any(definition => definition.Id == "dockReplacesTaskbar"),
            "Search must match English keywords such as 'taskbar'.");
    }

    [TestMethod]
    public void SearchForUnknownTextReturnsNothing()
    {
        Assert.IsEmpty(SettingsCatalog.Search("нет-такой-настройки-xyz"));
    }

    [TestMethod]
    public void ForPageReturnsOnlyThatPageSettings()
    {
        var dockSettings = SettingsCatalog.ForPage(SettingsCatalog.DockPageId);

        Assert.IsNotEmpty(dockSettings, "The dock page must expose settings in the base slice.");
        Assert.IsTrue(
            dockSettings.All(definition => definition.PageId == SettingsCatalog.DockPageId),
            "ForPage must not leak settings from other pages.");
        Assert.IsEmpty(SettingsCatalog.ForPage("no-such-page"));
    }

    [TestMethod]
    public void ResetRestoresDefaultsForThePageOnly()
    {
        var changed = UserSettingsEditor.With(
            UserSettings.CreateDefault(),
            dockEnabled: false,
            topBarEnabled: false,
            themeMode: ThemeMode.Dark);

        var reset = SettingsCatalog.Reset(changed, SettingsCatalog.DockPageId);

        Assert.IsTrue(reset.DockEnabled, "Reset must restore the dock default.");
        Assert.IsFalse(reset.TopBarEnabled, "Reset must not touch settings of other pages.");
        Assert.AreEqual(ThemeMode.Dark, reset.ThemeMode, "Reset must not touch settings of other pages.");
    }

    [TestMethod]
    public void WriteRoundTripsEverySettingToItsDefault()
    {
        // Каждая настройка должна уметь прочитать значение по умолчанию и записать его
        // обратно без исключений и потери остальных полей.
        var defaults = UserSettings.CreateDefault();

        foreach (var definition in SettingsCatalog.All)
        {
            var value = definition.Read(defaults);

            Assert.IsNotNull(value, $"Setting '{definition.Id}' must read a non-null default value.");

            var written = definition.Write(defaults, value);

            Assert.IsNotNull(written, $"Setting '{definition.Id}' must write a settings copy.");
        }
    }
}
