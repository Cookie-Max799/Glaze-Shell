using GlazeShell.Core.Discovery;
using GlazeShell.Core.Models;
using GlazeShell.Windows.Shell;

namespace GlazeShell.Windows.Tests;

[TestClass]
public sealed class StartMenuShortcutSourceTests
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
    public void DiscoversShortcutInGivenRoot()
    {
        var target = TestShortcutFactory.SelfExecutablePath;
        TestShortcutFactory.CreateShortcut(Path.Combine(_root, "Test Application.lnk"), target, "Test description");

        var source = new StartMenuShortcutSource(new[] { _root });
        var result = source.Discover();

        Assert.HasCount(1, result.Candidates, string.Join("; ", result.Warnings));
        Assert.AreEqual("Test Application", result.Candidates[0].Name);
        Assert.AreEqual(ApplicationType.Win32, result.Candidates[0].Type);
        Assert.AreEqual("start-menu", result.Candidates[0].Source);
        Assert.AreEqual("Test description", result.Candidates[0].Description);
        Assert.IsTrue(result.Candidates[0].HasLaunchIdentity);
    }

    [TestMethod]
    public void DiscoversShortcutInNestedDirectory()
    {
        var nested = Path.Combine(_root, "Group", "Deep");
        Directory.CreateDirectory(nested);
        TestShortcutFactory.CreateShortcut(Path.Combine(nested, "Nested.lnk"), TestShortcutFactory.SelfExecutablePath);

        var source = new StartMenuShortcutSource(new[] { _root });
        var result = source.Discover();

        Assert.HasCount(1, result.Candidates, string.Join("; ", result.Warnings));
        Assert.AreEqual("Nested", result.Candidates[0].Name);
    }

    [TestMethod]
    public void SkipsTargetsThatAreNotExecutables()
    {
        var script = TestShortcutFactory.NonExecutableScriptPath(_root);
        TestShortcutFactory.CreateShortcut(Path.Combine(_root, "Script.lnk"), script);
        TestShortcutFactory.CreateShortcut(Path.Combine(_root, "Allowed.lnk"), TestShortcutFactory.SelfExecutablePath);

        var source = new StartMenuShortcutSource(new[] { _root });
        var result = source.Discover();

        Assert.HasCount(1, result.Candidates, string.Join("; ", result.Warnings));
        Assert.AreEqual("Allowed", result.Candidates[0].Name);
        Assert.AreEqual(1, result.SkippedCount);
    }

    [TestMethod]
    public void SkipsMissingTargetsWhenExistingTargetIsRequired()
    {
        var missing = Path.Combine(_root, "missing.exe");
        TestShortcutFactory.CreateShortcut(Path.Combine(_root, "Missing.lnk"), missing);

        var source = new StartMenuShortcutSource(new[] { _root });
        var result = source.Discover();

        Assert.IsEmpty(result.Candidates);
        Assert.AreEqual(1, result.SkippedCount);
    }

    [TestMethod]
    public void StopsAtConfiguredDepth()
    {
        var nested = _root;

        for (var index = 0; index < 4; index++)
        {
            nested = Path.Combine(nested, "level" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        Directory.CreateDirectory(nested);
        TestShortcutFactory.CreateShortcut(Path.Combine(nested, "Deep.lnk"), TestShortcutFactory.SelfExecutablePath);

        var source = new StartMenuShortcutSource(new[] { _root }, new ApplicationDiscoveryOptions { MaxStartMenuDepth = 2 });
        var result = source.Discover();

        Assert.IsEmpty(result.Candidates);
        Assert.IsNotEmpty(result.Warnings);
    }

    [TestMethod]
    public void HonoursCancellation()
    {
        TestShortcutFactory.CreateShortcut(Path.Combine(_root, "Cancelled.lnk"), TestShortcutFactory.SelfExecutablePath);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var source = new StartMenuShortcutSource(new[] { _root });

        Assert.ThrowsExactly<OperationCanceledException>(() => source.Discover(cancellation.Token));
    }

    [TestMethod]
    public void ReadsArgumentsAndWorkingDirectory()
    {
        var target = TestShortcutFactory.SelfExecutablePath;
        var arguments = "--glaze-shell-test";

        TestShortcutFactory.CreateShortcut(Path.Combine(_root, "WithArguments.lnk"), target, arguments: arguments);

        var source = new StartMenuShortcutSource(new[] { _root });
        var result = source.Discover();

        Assert.HasCount(1, result.Candidates, string.Join("; ", result.Warnings));
        Assert.AreEqual(arguments, result.Candidates[0].Arguments);
    }

    [TestMethod]
    public void DiscoversRealStartMenuShortcuts()
    {
        var source = new StartMenuShortcutSource();
        var result = source.Discover();

        foreach (var candidate in result.Candidates)
        {
            Assert.IsTrue(candidate.HasLaunchIdentity, candidate.Name);
            Assert.IsTrue(File.Exists(candidate.ExecutablePath), candidate.ExecutablePath);
        }
    }
}
