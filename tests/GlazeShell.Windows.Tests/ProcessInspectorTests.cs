using GlazeShell.Windows.Applications;

namespace GlazeShell.Windows.Tests;

[TestClass]
public sealed class ProcessInspectorTests
{
    [TestMethod]
    public void FindsTheCurrentProcessByDirectory()
    {
        var inspector = new ProcessInspector();
        var directory = Path.GetDirectoryName(TestShortcutFactory.SelfExecutablePath)!;

        var matches = inspector.FindProcessesByDirectory(directory);

        Assert.Contains(Environment.ProcessId, matches);
    }

    [TestMethod]
    public void DoesNotFindTheCurrentProcessInUnrelatedDirectory()
    {
        var inspector = new ProcessInspector();
        var directory = Path.Combine(Path.GetTempPath(), "glaze-unrelated-directory");

        var matches = inspector.FindProcessesByDirectory(directory);

        CollectionAssert.DoesNotContain(new List<int>(matches), Environment.ProcessId);
    }

    [TestMethod]
    public void RejectsBlankDirectory()
    {
        var inspector = new ProcessInspector();

        Assert.ThrowsExactly<ArgumentException>(() => inspector.FindProcessesByDirectory("  "));
    }

    [TestMethod]
    public void FindsImageByDirectory()
    {
        var inspector = new ProcessInspector();
        var path = TestShortcutFactory.SelfExecutablePath;
        var directory = Path.GetDirectoryName(path)!;
        var image = Path.GetFileName(path);

        var matches = inspector.FindProcessesByImageInDirectory(directory, [image]);

        Assert.Contains(Environment.ProcessId, matches);
    }

    [TestMethod]
    public void ReturnsEmptyWhenNoImageNames()
    {
        var inspector = new ProcessInspector();
        var directory = Path.GetDirectoryName(TestShortcutFactory.SelfExecutablePath)!;

        var matches = inspector.FindProcessesByImageInDirectory(directory, []);

        Assert.IsEmpty(matches);
    }

    [TestMethod]
    public void DoesNotMatchOtherImageInDirectory()
    {
        var inspector = new ProcessInspector();
        var directory = Path.GetDirectoryName(TestShortcutFactory.SelfExecutablePath)!;

        var matches = inspector.FindProcessesByImageInDirectory(directory, ["glaze-nonexistent.exe"]);

        CollectionAssert.DoesNotContain(new List<int>(matches), Environment.ProcessId);
    }

    [TestMethod]
    public void RejectsNullImageNames()
    {
        var inspector = new ProcessInspector();
        var directory = Path.GetDirectoryName(TestShortcutFactory.SelfExecutablePath)!;

        Assert.ThrowsExactly<ArgumentNullException>(() => inspector.FindProcessesByImageInDirectory(directory, null!));
    }

    [TestMethod]
    public void PackageLookupDoesNotMatchUnpackagedProcess()
    {
        var inspector = new ProcessInspector();

        var matches = inspector.FindProcessesByPackage("Contoso.Nonexistent_1234567890abc");

        CollectionAssert.DoesNotContain(new List<int>(matches), Environment.ProcessId);
    }

    [TestMethod]
    public void RejectsBlankPackage()
    {
        var inspector = new ProcessInspector();

        Assert.ThrowsExactly<ArgumentException>(() => inspector.FindProcessesByPackage("  "));
    }

    [TestMethod]
    public void DoesNotFindTheCurrentProcessByUnrelatedPath()
    {
        var inspector = new ProcessInspector();
        var path = Path.Combine(Path.GetTempPath(), "glaze-unrelated-directory", "tool.exe");

        var matches = inspector.FindProcessesByPath(path);

        CollectionAssert.DoesNotContain(new List<int>(matches), Environment.ProcessId);
    }

    [TestMethod]
    public void RejectsBlankPath()
    {
        var inspector = new ProcessInspector();

        Assert.ThrowsExactly<ArgumentException>(() => inspector.FindProcessesByPath("  "));
    }
}
