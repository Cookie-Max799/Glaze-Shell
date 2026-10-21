using System;
using System.Linq;
using GlazeShell.Core.Models;
using GlazeShell.Core.Recovery;
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

    [TestMethod]
    public void AccessibilityPageExistsInTheSidebar()
    {
        var page = SettingsCatalog.Pages.Single(item => item.Id == SettingsCatalog.AccessibilityPageId);

        Assert.AreEqual("Доступность", page.Title);
        Assert.AreEqual("Система", page.Section);
    }

    [TestMethod]
    public void AccessibilityPageExposesBothToggles()
    {
        var settings = SettingsCatalog.ForPage(SettingsCatalog.AccessibilityPageId);

        var reduceMotion = settings.Single(definition => definition.Id == "reduceMotion");
        var highContrast = settings.Single(definition => definition.Id == "highContrast");

        Assert.AreEqual(SettingKind.Toggle, reduceMotion.Kind);
        Assert.AreEqual(SettingKind.Toggle, highContrast.Kind);
    }

    [TestMethod]
    public void AccessibilityTogglesWriteIntoSettings()
    {
        var definition = SettingsCatalog.All.Single(item => item.Id == "reduceMotion");

        var written = definition.Write(UserSettings.CreateDefault(), true);

        Assert.IsTrue(written.ReduceMotion);
        Assert.IsTrue((bool)definition.Read(written)!);
    }

    [TestMethod]
    public void HighContrastToggleWritesIntoSettings()
    {
        var definition = SettingsCatalog.All.Single(item => item.Id == "highContrast");

        var written = definition.Write(UserSettings.CreateDefault(), true);

        Assert.IsTrue(written.HighContrast);
        Assert.IsTrue((bool)definition.Read(written)!);
    }

    [TestMethod]
    public void ResetRestoresAccessibilityDefaults()
    {
        var changed = UserSettingsEditor.With(
            UserSettings.CreateDefault(),
            reduceMotion: true,
            highContrast: true);

        var reset = SettingsCatalog.Reset(changed, SettingsCatalog.AccessibilityPageId);

        Assert.IsFalse(reset.ReduceMotion);
        Assert.IsFalse(reset.HighContrast);
    }

    [TestMethod]
    public void AccessibilitySettingsAreFoundBySearch()
    {
        var byTitle = SettingsCatalog.Search("контраст");
        var byKeyword = SettingsCatalog.Search("motion");

        Assert.IsTrue(byTitle.Any(definition => definition.Id == "highContrast"));
        Assert.IsTrue(byKeyword.Any(definition => definition.Id == "reduceMotion"));
    }

    [TestMethod]
    public void RecoveryPageExistsInTheSidebar()
    {
        var page = SettingsCatalog.Pages.Single(item => item.Id == SettingsCatalog.RecoveryPageId);

        Assert.AreEqual("Восстановление", page.Title);
        Assert.AreEqual("Система", page.Section);
    }

    [TestMethod]
    public void RecoveryPageExposesHotkeyAndSafeMode()
    {
        var settings = SettingsCatalog.ForPage(SettingsCatalog.RecoveryPageId);

        var hotkey = settings.Single(definition => definition.Id == "recoveryHotkeyEnabled");
        var combination = settings.Single(definition => definition.Id == "recoveryHotkeyId");
        var safeMode = settings.Single(definition => definition.Id == "safeModeEnabled");

        Assert.AreEqual(SettingKind.Toggle, hotkey.Kind);
        Assert.AreEqual(SettingKind.Choice, combination.Kind);
        Assert.AreEqual(SettingKind.Toggle, safeMode.Kind);
    }

    [TestMethod]
    public void RecoveryHotkeyChoicesAreRareCombinations()
    {
        var definition = SettingsCatalog.All.Single(item => item.Id == "recoveryHotkeyId");

        Assert.IsNotEmpty(definition.Choices);

        foreach (var choice in definition.Choices)
        {
            Assert.IsTrue(
                RecoveryHotkey.TryParse(choice.Value, out _),
                $"The choice '{choice.Value}' must be a supported recovery hotkey.");
        }
    }

    [TestMethod]
    public void RecoverySettingsWriteIntoSettings()
    {
        var hotkey = SettingsCatalog.All.Single(item => item.Id == "recoveryHotkeyEnabled");
        var safeMode = SettingsCatalog.All.Single(item => item.Id == "safeModeEnabled");

        var written = safeMode.Write(hotkey.Write(UserSettings.CreateDefault(), false), true);

        Assert.IsFalse(written.RecoveryHotkeyEnabled);
        Assert.IsTrue(written.SafeModeEnabled);
    }

    [TestMethod]
    public void ResetRestoresRecoveryDefaults()
    {
        var changed = UserSettingsEditor.With(
            UserSettings.CreateDefault(),
            recoveryHotkeyEnabled: false,
            safeModeEnabled: true);

        var reset = SettingsCatalog.Reset(changed, SettingsCatalog.RecoveryPageId);

        Assert.IsTrue(reset.RecoveryHotkeyEnabled);
        Assert.IsFalse(reset.SafeModeEnabled);
    }

    [TestMethod]
    public void RecoverySettingsAreFoundBySearch()
    {
        var byTitle = SettingsCatalog.Search("восстановление");
        var byKeyword = SettingsCatalog.Search("safe mode");

        Assert.IsTrue(byTitle.Any(definition => definition.Id == "recoveryHotkeyEnabled"));
        Assert.IsTrue(byKeyword.Any(definition => definition.Id == "safeModeEnabled"));
    }
}
