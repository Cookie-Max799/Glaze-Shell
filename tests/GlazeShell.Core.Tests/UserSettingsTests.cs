using GlazeShell.Core.Models;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class UserSettingsTests
{
    [TestMethod]
    public void DefaultSettingsHaveNoMonitorNames()
    {
        var settings = UserSettings.CreateDefault();

        Assert.IsEmpty(settings.MonitorNames);
    }

    [TestMethod]
    public void MonitorNamesAreTrimmedAndMergedCaseInsensitively()
    {
        var settings = new UserSettings(monitorNames: new Dictionary<string, string>
        {
            ["  MON-1  "] = "  Left  ",
            ["mon-1"] = "Right",
            ["MON-2"] = "   ",
            ["   "] = "Ignored"
        });

        Assert.HasCount(1, settings.MonitorNames);
        Assert.AreEqual("Right", settings.MonitorNames["MON-1"]);
    }

    [TestMethod]
    public void EmptyMonitorNamesNormalizeToEmptyMap()
    {
        var fromNull = new UserSettings(monitorNames: null);
        var fromEmpty = new UserSettings(monitorNames: new Dictionary<string, string>());
        var fromBlank = new UserSettings(monitorNames: new Dictionary<string, string> { [" "] = " " });

        Assert.IsEmpty(fromNull.MonitorNames);
        Assert.IsEmpty(fromEmpty.MonitorNames);
        Assert.IsEmpty(fromBlank.MonitorNames);
    }

    [TestMethod]
    public void DefaultThemeModeIsSystemAndAccentIsNull()
    {
        var settings = UserSettings.CreateDefault();

        Assert.AreEqual(ThemeMode.System, settings.ThemeMode);
        Assert.IsNull(settings.AccentId);
    }

    [TestMethod]
    public void AccentIsTrimmedAndKept()
    {
        var settings = new UserSettings(accentId: "  cobalt  ");

        Assert.AreEqual("cobalt", settings.AccentId);
    }

    [TestMethod]
    public void UnknownAccentIsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new UserSettings(accentId: "neon"));
    }

    [TestMethod]
    public void ThemeModeIsKept()
    {
        var settings = new UserSettings(themeMode: ThemeMode.Light);

        Assert.AreEqual(ThemeMode.Light, settings.ThemeMode);
    }

    [TestMethod]
    public void DefaultTopBarIsEnabled()
    {
        Assert.IsTrue(UserSettings.CreateDefault().TopBarEnabled);
    }

    [TestMethod]
    public void TopBarCanBeDisabled()
    {
        var settings = new UserSettings(topBarEnabled: false);

        Assert.IsFalse(settings.TopBarEnabled);
    }

    [TestMethod]
    public void DefaultDockIsEnabledWithNoPins()
    {
        var settings = UserSettings.CreateDefault();

        Assert.IsTrue(settings.DockEnabled);
        Assert.IsEmpty(settings.DockPinnedApps);
    }

    [TestMethod]
    public void DockCanBeDisabled()
    {
        var settings = new UserSettings(dockEnabled: false);

        Assert.IsFalse(settings.DockEnabled);
    }

    [TestMethod]
    public void DefaultDockReplacesTaskbar()
    {
        Assert.IsTrue(UserSettings.CreateDefault().DockReplacesTaskbar);
    }

    [TestMethod]
    public void TaskbarReplacementCanBeDisabled()
    {
        var settings = new UserSettings(dockReplacesTaskbar: false);

        Assert.IsFalse(settings.DockReplacesTaskbar);
    }

    [TestMethod]
    public void DockPinsAreTrimmedDeduplicatedAndOrdered()
    {
        var settings = new UserSettings(dockPinnedApps: ["  app-1  ", "app-1", "APP-2", "  ", "", "app-3"]);

        Assert.HasCount(3, settings.DockPinnedApps);
        Assert.AreEqual("app-1", settings.DockPinnedApps[0]);
        Assert.AreEqual("APP-2", settings.DockPinnedApps[1]);
        Assert.AreEqual("app-3", settings.DockPinnedApps[2]);
    }

    [TestMethod]
    public void DockPinsAreCappedAtTheLimit()
    {
        var pins = Enumerable.Range(0, UserSettings.MaxDockPinnedApps + 10).Select(index => $"app-{index}").ToList();

        var settings = new UserSettings(dockPinnedApps: pins);

        Assert.HasCount(UserSettings.MaxDockPinnedApps, settings.DockPinnedApps);
    }

    [TestMethod]
    public void EmptyDockPinsNormalizeToEmptyList()
    {
        Assert.IsEmpty(new UserSettings(dockPinnedApps: null).DockPinnedApps);
        Assert.IsEmpty(new UserSettings(dockPinnedApps: []).DockPinnedApps);
        Assert.IsEmpty(new UserSettings(dockPinnedApps: ["  "]).DockPinnedApps);
    }

    [TestMethod]
    public void DefaultExplorerHasNoFavoritesAndIconsView()
    {
        var settings = UserSettings.CreateDefault();

        Assert.IsEmpty(settings.ExplorerFavorites);
        Assert.AreEqual(ExplorerViewMode.Icons, settings.ExplorerViewMode);
    }

    [TestMethod]
    public void ExplorerFavoritesAreTrimmedDeduplicatedAndOrdered()
    {
        var settings = new UserSettings(explorerFavorites:
            ["  C:\\Work  ", "C:\\Work", "c:\\work", "C:\\Notes\\", ""]);

        Assert.HasCount(2, settings.ExplorerFavorites);
        Assert.AreEqual("C:\\Work", settings.ExplorerFavorites[0]);
        Assert.AreEqual("C:\\Notes", settings.ExplorerFavorites[1]);
    }

    [TestMethod]
    public void ExplorerFavoritesAreCappedAtTheLimit()
    {
        var favorites = Enumerable.Range(0, UserSettings.MaxExplorerFavorites + 10)
            .Select(index => $"C:\\Folders\\{index}")
            .ToList();

        var settings = new UserSettings(explorerFavorites: favorites);

        Assert.HasCount(UserSettings.MaxExplorerFavorites, settings.ExplorerFavorites);
    }

    [TestMethod]
    public void EmptyExplorerFavoritesNormalizeToEmptyList()
    {
        Assert.IsEmpty(new UserSettings(explorerFavorites: null).ExplorerFavorites);
        Assert.IsEmpty(new UserSettings(explorerFavorites: []).ExplorerFavorites);
        Assert.IsEmpty(new UserSettings(explorerFavorites: ["   "]).ExplorerFavorites);
    }

    [TestMethod]
    public void ExplorerViewModeIsKept()
    {
        var settings = new UserSettings(explorerViewMode: ExplorerViewMode.List);

        Assert.AreEqual(ExplorerViewMode.List, settings.ExplorerViewMode);
    }

    [TestMethod]
    public void UnknownExplorerViewModeIsRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new UserSettings(explorerViewMode: (ExplorerViewMode)42));
    }
}
