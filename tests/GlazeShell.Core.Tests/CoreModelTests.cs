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
}
