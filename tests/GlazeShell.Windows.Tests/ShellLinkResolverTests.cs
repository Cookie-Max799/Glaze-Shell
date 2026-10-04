using GlazeShell.Windows.Shell;

namespace GlazeShell.Windows.Tests;

[TestClass]
public sealed class ShellLinkResolverTests
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
    public void ResolvesTargetFromLinkInfo()
    {
        var target = TestShortcutFactory.SelfExecutablePath;
        var shortcut = Path.Combine(_root, "LinkInfo.lnk");

        var builder = ShellLinkBuilder.WithLinkInfo();
        builder.Name = "Link Info";
        builder.Arguments = "--flag";
        builder.WorkingDirectory = Path.GetDirectoryName(target);
        builder.IconLocation = target;
        builder.WriteTo(shortcut, target);

        Assert.IsTrue(ShellLinkResolver.TryResolve(shortcut, out var resolved, out var error), error);
        Assert.IsNotNull(resolved);
        Assert.AreEqual(target, resolved!.Path, ignoreCase: true);
        Assert.AreEqual("Link Info", resolved.Name);
        Assert.AreEqual("--flag", resolved.Arguments);
        Assert.AreEqual(ShellLinkResolutionStrategy.ManagedLinkInfo, resolved.Strategy);
    }

    [TestMethod]
    public void ResolvesTargetWhenShortcutHasTargetIdList()
    {
        var target = TestShortcutFactory.SelfExecutablePath;
        var shortcut = Path.Combine(_root, "IdList.lnk");

        var builder = ShellLinkBuilder.WithLinkInfoAndTargetIdList();
        builder.Name = "Id List";
        builder.Arguments = "--flag";
        builder.WriteTo(shortcut, target);

        Assert.IsTrue(ShellLinkResolver.TryResolve(shortcut, out var resolved, out var error), error);
        Assert.IsNotNull(resolved);
        Assert.AreEqual(target, resolved!.Path, ignoreCase: true);
        Assert.AreEqual("Id List", resolved.Name);
        Assert.AreEqual("--flag", resolved.Arguments);
        Assert.AreEqual(ShellLinkResolutionStrategy.ManagedLinkInfo, resolved.Strategy);
    }

    [TestMethod]
    public void ResolvesTargetFromRelativePath()
    {
        var target = TestShortcutFactory.SelfExecutablePath;
        var shortcut = Path.Combine(_root, "Relative.lnk");

        var builder = ShellLinkBuilder.WithRelativePath();
        builder.Name = "Relative";
        builder.WriteTo(shortcut, target);

        Assert.IsTrue(ShellLinkResolver.TryResolve(shortcut, out var resolved, out var error), error);
        Assert.IsNotNull(resolved);
        Assert.AreEqual(target, resolved!.Path, ignoreCase: true);
        Assert.AreEqual(ShellLinkResolutionStrategy.ManagedRelativePath, resolved.Strategy);
    }

    [TestMethod]
    public void ExpandsEnvironmentVariablesInTarget()
    {
        var target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        var builder = ShellLinkBuilder.WithLinkInfo();
        builder.Name = "Env";
        builder.WriteTo(Path.Combine(_root, "Env.lnk"), "%SystemRoot%\\explorer.exe");

        var data = ShellLinkData.TryRead(Path.Combine(_root, "Env.lnk"));

        Assert.IsNotNull(data);
        Assert.AreEqual(target, data!.ResolveTargetPath(Path.Combine(_root, "Env.lnk")), ignoreCase: true);
    }

    [TestMethod]
    public void RejectsNonShellLinkFiles()
    {
        var path = Path.Combine(_root, "not-a-link.lnk");
        File.WriteAllText(path, "definitely not a binary shell link");

        Assert.IsNull(ShellLinkData.TryRead(path));
    }

    [TestMethod]
    public void RejectsTruncatedShellLinks()
    {
        var builder = ShellLinkBuilder.WithLinkInfo();
        var full = builder.BuildBytes(TestShortcutFactory.SelfExecutablePath, "Truncated");
        var truncated = full[..(ShellLinkData.HeaderSize / 2)];

        Assert.IsNull(ShellLinkData.TryParse(truncated));
    }

    [TestMethod]
    public void ReportsFailureForFileWithoutTarget()
    {
        var builder = ShellLinkBuilder.WithoutTarget();
        builder.Name = "No Target";
        var shortcut = Path.Combine(_root, "NoTarget.lnk");
        builder.WriteTo(shortcut, "placeholder.exe");

        Assert.IsFalse(ShellLinkResolver.TryResolve(shortcut, out var resolved, out var error));
        Assert.IsNull(resolved);
        Assert.IsNotNull(error);
    }
}
