using System.Globalization;
using GlazeShell.Core.Models;
using GlazeShell.Data.Serialization;

namespace GlazeShell.Data.Tests;

[TestClass]
public sealed class SettingsDocumentMapperTests
{
    [TestMethod]
    public void MapsDefaultDocument()
    {
        var document = new SettingsDocument();

        Assert.IsTrue(SettingsDocumentMapper.TryMap(document, out var settings, out var diagnostics));

        Assert.IsEmpty(diagnostics);
        Assert.IsNotNull(settings);
        Assert.AreEqual(SettingsDocument.DefaultLanguage, settings.Language);
        Assert.IsTrue(settings.ShowDesktopOnStartup);
        Assert.IsTrue(settings.TopBarEnabled);
        Assert.IsTrue(settings.DockEnabled);
        Assert.IsTrue(settings.DockReplacesTaskbar);
        Assert.IsEmpty(settings.DockPinnedApps);
        Assert.IsEmpty(settings.MonitorNames);
    }

    [TestMethod]
    public void RejectsMissingDocument()
    {
        Assert.IsFalse(SettingsDocumentMapper.TryMap(null, out var settings, out var diagnostics));

        Assert.IsNull(settings);
        StringAssert.Contains(diagnostics.Single(), "missing");
    }

    [TestMethod]
    public void DefaultsLanguageWhenNull()
    {
        var document = new SettingsDocument { Language = null };

        Assert.IsTrue(SettingsDocumentMapper.TryMap(document, out var settings, out _));
        Assert.AreEqual(SettingsDocument.DefaultLanguage, settings!.Language);
    }

    [TestMethod]
    public void RejectsInvalidLanguage()
    {
        var document = new SettingsDocument { Language = "  " };

        Assert.IsFalse(SettingsDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "language");
    }

    [TestMethod]
    public void RejectsInvalidThemeIdentifier()
    {
        var document = new SettingsDocument { ActiveThemeId = "bad\ntheme" };

        Assert.IsFalse(SettingsDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "theme identifier");
    }

    [TestMethod]
    public void MapsThemeModeAndAccent()
    {
        var document = new SettingsDocument { ThemeMode = "dark", AccentId = "mint" };

        Assert.IsTrue(SettingsDocumentMapper.TryMap(document, out var settings, out _));

        Assert.AreEqual(ThemeMode.Dark, settings!.ThemeMode);
        Assert.AreEqual("mint", settings.AccentId);
    }

    [TestMethod]
    public void DefaultsThemeModeWhenNull()
    {
        var document = new SettingsDocument { ThemeMode = null, AccentId = null };

        Assert.IsTrue(SettingsDocumentMapper.TryMap(document, out var settings, out _));

        Assert.AreEqual(ThemeMode.System, settings!.ThemeMode);
        Assert.IsNull(settings.AccentId);
    }

    [TestMethod]
    public void RejectsInvalidThemeMode()
    {
        var document = new SettingsDocument { ThemeMode = "sepia" };

        Assert.IsFalse(SettingsDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "theme mode");
    }

    [TestMethod]
    public void RejectsInvalidAccent()
    {
        var document = new SettingsDocument { AccentId = "neon" };

        Assert.IsFalse(SettingsDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "accent");
    }

    [TestMethod]
    public void RejectsTooManyMonitorNames()
    {
        var document = new SettingsDocument
        {
            MonitorNames = Enumerable.Range(0, PersistenceLimits.MaxMonitorNames + 1)
                .ToDictionary(static index => $"monitor-{index}", static index => $"Display {index}")
        };

        Assert.IsFalse(SettingsDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(
            diagnostics.Single(),
            PersistenceLimits.MaxMonitorNames.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void RejectsInvalidMonitorIdentifier()
    {
        var document = new SettingsDocument
        {
            MonitorNames = new Dictionary<string, string> { ["  "] = "Display" }
        };

        Assert.IsFalse(SettingsDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "monitor identifier");
    }

    [TestMethod]
    public void RejectsInvalidMonitorName()
    {
        var document = new SettingsDocument
        {
            MonitorNames = new Dictionary<string, string> { ["monitor-1"] = "\n" }
        };

        Assert.IsFalse(SettingsDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "monitor name");
    }

    [TestMethod]
    public void MapsMonitorNames()
    {
        var document = new SettingsDocument
        {
            MonitorNames = new Dictionary<string, string> { ["monitor-1"] = "Left", ["monitor-2"] = "Right" }
        };

        Assert.IsTrue(SettingsDocumentMapper.TryMap(document, out var settings, out _));

        Assert.HasCount(2, settings!.MonitorNames);
        Assert.AreEqual("Left", settings.MonitorNames["monitor-1"]);
    }

    [TestMethod]
    public void MapsDisabledTopBar()
    {
        var document = new SettingsDocument { TopBarEnabled = false };

        Assert.IsTrue(SettingsDocumentMapper.TryMap(document, out var settings, out _));

        Assert.IsFalse(settings!.TopBarEnabled);
    }

    [TestMethod]
    public void MapsDisabledDock()
    {
        var document = new SettingsDocument { DockEnabled = false };

        Assert.IsTrue(SettingsDocumentMapper.TryMap(document, out var settings, out _));

        Assert.IsFalse(settings!.DockEnabled);
    }

    [TestMethod]
    public void MapsDisabledTaskbarReplacement()
    {
        var document = new SettingsDocument { DockReplacesTaskbar = false };

        Assert.IsTrue(SettingsDocumentMapper.TryMap(document, out var settings, out _));

        Assert.IsFalse(settings!.DockReplacesTaskbar);
    }

    [TestMethod]
    public void MapsDockPinsDeduplicatedInOrder()
    {
        var document = new SettingsDocument { DockPinnedApps = ["app-1", "APP-1", "app-2"] };

        Assert.IsTrue(SettingsDocumentMapper.TryMap(document, out var settings, out _));

        Assert.HasCount(2, settings!.DockPinnedApps);
        Assert.AreEqual("app-1", settings.DockPinnedApps[0]);
        Assert.AreEqual("app-2", settings.DockPinnedApps[1]);
    }

    [TestMethod]
    public void RejectsInvalidDockPin()
    {
        var document = new SettingsDocument { DockPinnedApps = ["bad\npin"] };

        Assert.IsFalse(SettingsDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "dock pinned application");
    }

    [TestMethod]
    public void RejectsTooManyDockPins()
    {
        var document = new SettingsDocument
        {
            DockPinnedApps = Enumerable.Range(0, UserSettings.MaxDockPinnedApps + 1)
                .Select(static index => $"app-{index}")
                .ToList()
        };

        Assert.IsFalse(SettingsDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(
            diagnostics.Single(),
            UserSettings.MaxDockPinnedApps.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void FromSettingsMapsValues()
    {
        var settings = new UserSettings(
            startWithWindows: true,
            minimizeToTray: true,
            language: "de-DE",
            activeThemeId: "midnight",
            monitorNames: new Dictionary<string, string> { ["monitor-1"] = "Left" },
            themeMode: ThemeMode.Light,
            accentId: "rose",
            topBarEnabled: false,
            dockEnabled: false,
            dockPinnedApps: ["app-1", "app-2"],
            dockReplacesTaskbar: false);

        var document = SettingsDocument.FromSettings(settings);

        Assert.IsTrue(document.StartWithWindows);
        Assert.IsTrue(document.MinimizeToTray);
        Assert.AreEqual("de-DE", document.Language);
        Assert.AreEqual("midnight", document.ActiveThemeId);
        Assert.AreEqual("light", document.ThemeMode);
        Assert.AreEqual("rose", document.AccentId);
        Assert.IsFalse(document.TopBarEnabled);
        Assert.IsFalse(document.DockEnabled);
        Assert.IsFalse(document.DockReplacesTaskbar);
        Assert.HasCount(2, document.DockPinnedApps!);
        Assert.HasCount(1, document.MonitorNames!);
    }

    [TestMethod]
    public void FromSettingsOmitsEmptyDockPins()
    {
        var document = SettingsDocument.FromSettings(new UserSettings());

        Assert.IsNull(document.DockPinnedApps);
    }

    [TestMethod]
    public void FromSettingsRejectsNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => SettingsDocument.FromSettings(null!));
    }

    [TestMethod]
    public void MapsExplorerDefaults()
    {
        var document = new SettingsDocument();

        Assert.IsTrue(SettingsDocumentMapper.TryMap(document, out var settings, out var diagnostics));

        Assert.IsEmpty(diagnostics);
        Assert.IsNotNull(settings);
        Assert.IsEmpty(settings.ExplorerFavorites);
        Assert.AreEqual(ExplorerViewMode.Icons, settings.ExplorerViewMode);
    }

    [TestMethod]
    public void MapsExplorerViewModeAndFavorites()
    {
        var document = new SettingsDocument
        {
            ExplorerViewMode = "list",
            ExplorerFavorites = [" C:\\Work ", "C:\\Notes\\"]
        };

        Assert.IsTrue(SettingsDocumentMapper.TryMap(document, out var settings, out var diagnostics));

        Assert.IsEmpty(diagnostics);
        Assert.IsNotNull(settings);
        Assert.AreEqual(ExplorerViewMode.List, settings.ExplorerViewMode);
        Assert.HasCount(2, settings.ExplorerFavorites);
        Assert.AreEqual("C:\\Work", settings.ExplorerFavorites[0]);
        Assert.AreEqual("C:\\Notes", settings.ExplorerFavorites[1]);
    }

    [TestMethod]
    public void RejectsInvalidExplorerViewMode()
    {
        var document = new SettingsDocument { ExplorerViewMode = "gallery" };

        Assert.IsFalse(SettingsDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "Explorer view mode");
    }

    [TestMethod]
    public void RejectsTooManyExplorerFavorites()
    {
        var document = new SettingsDocument
        {
            ExplorerFavorites = Enumerable.Range(0, UserSettings.MaxExplorerFavorites + 1)
                .Select(static index => $"C:\\Folders\\{index}")
                .ToList()
        };

        Assert.IsFalse(SettingsDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(
            diagnostics.Single(),
            UserSettings.MaxExplorerFavorites.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void FromSettingsMapsExplorerValues()
    {
        var settings = new UserSettings(
            explorerFavorites: ["C:\\Work"],
            explorerViewMode: ExplorerViewMode.List);

        var document = SettingsDocument.FromSettings(settings);

        Assert.AreEqual("list", document.ExplorerViewMode);
        Assert.HasCount(1, document.ExplorerFavorites!);
        Assert.AreEqual("C:\\Work", document.ExplorerFavorites![0]);
    }

    [TestMethod]
    public void FromSettingsOmitsEmptyExplorerFavorites()
    {
        var document = SettingsDocument.FromSettings(new UserSettings());

        Assert.IsNull(document.ExplorerFavorites);
        Assert.AreEqual("icons", document.ExplorerViewMode);
    }
}
