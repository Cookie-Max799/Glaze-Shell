using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Windows.Applications;
using GlazeShell.Windows.Shell;

namespace GlazeShell.Windows.Tests;

[TestClass]
public sealed class WindowsApplicationLauncherTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        _root = TestShortcutFactory.CreateTemporaryRoot();
    }

    [TestCleanup]
    public void Cleanup()
    {
        TestShortcutFactory.DeleteRoot(_root);
    }

    [TestMethod]
    public async Task LaunchFailsForUnknownApplication()
    {
        var launcher = CreateLauncher(new FakeApplicationManager());

        var result = await launcher.LaunchAsync("missing");

        Assert.IsFalse(result.Started);
        Assert.IsNotNull(result.Error);
    }

    [TestMethod]
    public async Task LaunchFailsWhenTargetIsMissing()
    {
        var manager = new FakeApplicationManager().Add(
            new Application("app:missing", "Missing", ApplicationType.Win32, Path.Combine(_root, "nope.exe")));

        var launcher = CreateLauncher(manager);

        var result = await launcher.LaunchAsync("app:missing");

        Assert.IsFalse(result.Started);
        Assert.Contains("does not exist", result.Error!);
    }

    [TestMethod]
    public async Task LaunchStartsAShortLivedProcess()
    {
        var comspec = Environment.GetEnvironmentVariable("ComSpec");

        if (string.IsNullOrWhiteSpace(comspec) || !File.Exists(comspec))
        {
            Assert.Inconclusive("ComSpec is unavailable.");
            return;
        }

        var manager = new FakeApplicationManager().Add(
            new Application("app:exit", "Exit", ApplicationType.Win32, comspec, arguments: "/c exit 0"));

        var launcher = CreateLauncher(manager);

        var result = await launcher.LaunchAsync("app:exit");

        Assert.IsTrue(result.Started, result.Error);
        Assert.IsNotNull(result.ProcessId);
    }

    [TestMethod]
    public async Task IsRunningDetectsTheCurrentProcess()
    {
        var executable = TestShortcutFactory.SelfExecutablePath;
        var manager = new FakeApplicationManager().Add(new Application("app:self", "Self", ApplicationType.Win32, executable));

        var launcher = CreateLauncher(manager);

        var running = await launcher.IsRunningAsync("app:self");

        Assert.IsTrue(running);
    }

    [TestMethod]
    public async Task CloseReturnsFalseWhenNothingIsRunning()
    {
        var executable = TestShortcutFactory.SelfExecutablePath;
        var manager = new FakeApplicationManager().Add(new Application("app:idle", "Idle", ApplicationType.Win32, executable));

        var launcher = CreateLauncher(manager, new StubProcessInspector());

        Assert.IsFalse(await launcher.CloseAsync("app:idle"));
    }

    [TestMethod]
    public void ProcessInspectorFindsTheCurrentProcessByPath()
    {
        var inspector = new ProcessInspector();

        var matches = inspector.FindProcessesByPath(TestShortcutFactory.SelfExecutablePath);

        Assert.Contains(Environment.ProcessId, matches);
    }

    [TestMethod]
    public void PackageResolverReturnsNullForUnknownFamily()
    {
        var resolver = new PackageInstallLocationResolver();

        Assert.IsNull(resolver.ResolveInstallLocation("GlazeShell.Unknown_1234567890abc"));
    }

    private static WindowsApplicationLauncher CreateLauncher(
        FakeApplicationManager manager,
        IProcessInspector? inspector = null)
    {
        return new WindowsApplicationLauncher(
            manager,
            inspector ?? new ProcessInspector(),
            new PackageInstallLocationResolver());
    }
}
