using GlazeShell.Core.Events;
using GlazeShell.Core.Models;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class GlazeEventsTests
{
    [TestMethod]
    public void WindowOpenedRejectsNullWindow()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new WindowOpened(null!));
    }

    [TestMethod]
    public void WindowClosedRejectsNullWindow()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new WindowClosed(null!));
    }

    [TestMethod]
    public void WindowStateChangedRejectsNullWindow()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new WindowStateChanged(null!));
    }

    [TestMethod]
    public void ForegroundWindowChangedAllowsNullWindow()
    {
        Assert.IsNull(new ForegroundWindowChanged(null).Window);
    }

    [TestMethod]
    public void ProcessStartedRequiresValidValues()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new ProcessStarted(42, " "));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ProcessStarted(-1, "tool"));
    }

    [TestMethod]
    public void ProcessStartedStoresOptionalPath()
    {
        var started = new ProcessStarted(42, "tool", @"C:\Apps\tool.exe");

        Assert.AreEqual(42, started.ProcessId);
        Assert.AreEqual("tool", started.ProcessName);
        Assert.AreEqual(@"C:\Apps\tool.exe", started.ExecutablePath);
    }

    [TestMethod]
    public void ProcessExitedAllowsOptionalValues()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ProcessExited(-1));

        var exited = new ProcessExited(42);

        Assert.AreEqual(42, exited.ProcessId);
        Assert.IsNull(exited.ProcessName);
        Assert.IsNull(exited.ExecutablePath);
    }

    [TestMethod]
    public void DisplayChangedCopiesMonitorList()
    {
        var monitors = new List<MonitorInfo> { Monitor() };
        var changed = new DisplayChanged(monitors);
        monitors.Clear();

        Assert.HasCount(1, changed.Monitors);
    }

    [TestMethod]
    public void DisplayChangedRejectsNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new DisplayChanged(null!));
    }

    [TestMethod]
    public void ApplicationChangedCopiesApplications()
    {
        var applications = new List<Application> { new("app:1", "One", ApplicationType.Win32, @"C:\One.exe") };
        var changed = new ApplicationChanged(applications);
        applications.Clear();

        Assert.HasCount(1, changed.Applications);
    }

    [TestMethod]
    public void ApplicationChangedRejectsNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ApplicationChanged(null!));
    }

    [TestMethod]
    public void DesktopChangedRejectsNullLayout()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new DesktopChanged(null!));
    }

    [TestMethod]
    public void SettingsChangedRejectsNullSettings()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SettingsChanged(null!));
    }

    [TestMethod]
    public void ThemeChangedRejectsNullTheme()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ThemeChanged(null!));
    }

    [TestMethod]
    public void ThemeChangedKeepsPreviousThemeId()
    {
        var changed = new ThemeChanged(DefaultTheme(), "previous");

        Assert.AreEqual("previous", changed.PreviousThemeId);
        Assert.AreEqual("default", changed.Theme.Id);
    }

    private static MonitorInfo Monitor() =>
        new("monitor-1", "\\\\.\\DISPLAY1", new MonitorBounds(0, 0, 1920, 1080));

    private static Theme DefaultTheme() =>
        new(
            "default",
            "Default",
            new ThemeMetadata("Default", "1.0"),
            new ThemeColors(),
            new ThemeFonts(),
            new ThemeDimensions(),
            new ThemeIcons());
}
