using GlazeShell.Core.Discovery;
using GlazeShell.Core.Models;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class ApplicationProcessMatcherTests
{
    private static readonly string[] LauncherImageNames = { "Update.exe", "Discord.exe" };

    [TestMethod]
    public void MatchesExactExecutablePath()
    {
        var application = Win32(@"C:\Apps\Contoso\app.exe");

        Assert.IsTrue(ApplicationProcessMatcher.MatchesExecutable(application, @"C:\Apps\Contoso\app.exe"));
    }

    [TestMethod]
    public void MatchesLauncherSpawnedProcessInSubdirectory()
    {
        // Discord: ярлык указывает на Discord.exe в корне каталога установки, а реально
        // приложение работает из подкаталога app-<version> с тем же именем образа.
        var application = Win32(@"C:\Users\me\AppData\Local\Discord\Discord.exe");

        Assert.IsTrue(ApplicationProcessMatcher.MatchesExecutable(
            application,
            @"C:\Users\me\AppData\Local\Discord\app-1.0.9261\Discord.exe"));
    }

    [TestMethod]
    public void MatchesProcessStartedByLauncherArgument()
    {
        // Squirrel: Update.exe --processStart Discord.exe поднимает Discord.exe из подкаталога.
        var application = Win32(@"C:\Users\me\AppData\Local\Discord\Update.exe", arguments: "--processStart Discord.exe");

        Assert.IsTrue(ApplicationProcessMatcher.MatchesExecutable(
            application,
            @"C:\Users\me\AppData\Local\Discord\app-1.0.9261\Discord.exe"));
    }

    [TestMethod]
    public void MatchesProcessStartArgumentWithQuotes()
    {
        var application = Win32(@"C:\Apps\Launcher\Update.exe", arguments: "--processStart \"Real App.exe\"");

        Assert.IsTrue(ApplicationProcessMatcher.MatchesExecutable(
            application,
            @"C:\Apps\Launcher\app-2.0\Real App.exe"));
    }

    [TestMethod]
    public void DoesNotMatchSiblingWithDifferentImageInSameDirectory()
    {
        // git-bash.exe и git-cmd.exe стоят в одном каталоге: работающий git-bash не должен
        // считаться запущенным git-cmd.
        var application = Win32(@"C:\Program Files\Git\git-cmd.exe", arguments: "--cd-to-home");

        Assert.IsFalse(ApplicationProcessMatcher.MatchesExecutable(
            application,
            @"C:\Program Files\Git\git-bash.exe"));
    }

    [TestMethod]
    public void DoesNotMatchExecutableFromUnrelatedDirectory()
    {
        var application = Win32(@"C:\Apps\Contoso\app.exe");

        Assert.IsFalse(ApplicationProcessMatcher.MatchesExecutable(application, @"C:\Other\Contoso\app.exe"));
    }

    [TestMethod]
    public void MatchesByProcessNameWhenPathIsUnknown()
    {
        var application = Win32(@"C:\Users\me\AppData\Local\Discord\Discord.exe");

        Assert.IsTrue(ApplicationProcessMatcher.Matches(application, executablePath: null, processName: "Discord"));
        Assert.IsFalse(ApplicationProcessMatcher.Matches(application, executablePath: null, processName: "Update"));
    }

    [TestMethod]
    public void MatchesMsixPackagePath()
    {
        var application = new Application(
            "msix:test",
            "Test",
            ApplicationType.Msix,
            packageFamilyName: "Microsoft.WindowsCalculator_8wekyb3d8bbwe",
            applicationUserModelId: "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App");

        Assert.IsTrue(ApplicationProcessMatcher.MatchesExecutable(
            application,
            @"C:\Program Files\WindowsApps\Microsoft.WindowsCalculator_11.0.0.0_x64__8wekyb3d8bbwe\Calculator.exe"));
    }

    [TestMethod]
    public void ReturnsInstallDirectoryAndImageNames()
    {
        var application = Win32(@"C:\Users\me\AppData\Local\Discord\Update.exe", arguments: "--processStart Discord.exe");

        Assert.AreEqual(@"C:\Users\me\AppData\Local\Discord", ApplicationProcessMatcher.GetInstallDirectory(application));

        var names = ApplicationProcessMatcher.GetImageNames(application);
        CollectionAssert.AreEquivalent(LauncherImageNames, names.ToArray());    }

    private static Application Win32(string executablePath, string? arguments = null) =>
        new("app:test", "Test", ApplicationType.Win32, executablePath, arguments: arguments);
}
