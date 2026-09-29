using GlazeShell.Core.Models;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class CoreModelTests
{
    [TestMethod]
    public void ApplicationRequiresLaunchIdentity()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Application("app-1", "Example"));
    }

    [TestMethod]
    public void ApplicationAcceptsExecutablePath()
    {
        var application = new Application(
            "app-1",
            "Example",
            ApplicationType.Win32,
            @"C:\Applications\Example.exe");

        Assert.IsTrue(application.IsLaunchable);
        Assert.AreEqual(ApplicationType.Win32, application.Type);
    }

    [TestMethod]
    public void DesktopLayoutRejectsUnknownActiveTab()
    {
        var tabs = new[] { new DesktopTab("home", "Home") };

        Assert.ThrowsExactly<ArgumentException>(() => new DesktopLayout(tabs, "unknown"));
    }

    [TestMethod]
    public void ThemeRejectsExecutableAsset()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new ThemeIcon("shell", "shell.exe"));
    }

    [TestMethod]
    public void ThemeCanBeCreatedWithoutWallpaper()
    {
        var theme = new Theme(
            "default",
            "Default",
            new ThemeMetadata("Default", "1.0"),
            new ThemeColors(),
            new ThemeFonts(),
            new ThemeDimensions(),
            new ThemeIcons());

        Assert.IsNull(theme.Wallpaper);
        Assert.AreEqual("1.0", theme.Metadata.Version);
    }

    [TestMethod]
    public void MonitorInfoRequiresPositiveScaleFactor()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MonitorInfo(
            "monitor-1",
            "\\\\.\\DISPLAY1",
            new MonitorBounds(0, 0, 1920, 1080),
            scaleFactor: 0));
    }

    [TestMethod]
    public void MonitorInfoAcceptsValidConfiguration()
    {
        var monitor = new MonitorInfo(
            "monitor-1",
            "\\\\.\\DISPLAY1",
            new MonitorBounds(0, 0, 2560, 1440),
            new MonitorBounds(0, 0, 2560, 1400),
            scaleFactor: 1.25,
            isPrimary: true,
            refreshRateHz: 144,
            orientation: MonitorOrientation.Landscape);

        Assert.IsTrue(monitor.IsPrimary);
        Assert.AreEqual(1.25, monitor.ScaleFactor);
        Assert.AreEqual(144, monitor.RefreshRateHz);
        Assert.IsNotNull(monitor.WorkingArea);
    }

    [TestMethod]
    public void MonitorBoundsRejectsNonPositiveDimensions()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MonitorBounds(0, 0, 0, 1080));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MonitorBounds(0, 0, 1920, 0));
    }
}
