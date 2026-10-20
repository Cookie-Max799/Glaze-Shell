using GlazeShell.Core.Files;
using GlazeShell.Windows.FileSystem;

namespace GlazeShell.Windows.Tests.FileSystem;

[TestClass]
public sealed class NativeFileSystemBrowserTests
{
    [TestMethod]
    public void DirectoryExistsOnlyForReadablePaths()
    {
        var browser = new NativeFileSystemBrowser();
        var path = CreateTemporaryDirectory();

        try
        {
            Assert.IsTrue(browser.DirectoryExists(path));
            Assert.IsFalse(browser.DirectoryExists(Path.Combine(path, "missing-child")));
            Assert.IsFalse(browser.DirectoryExists("   "));
            Assert.IsFalse(browser.DirectoryExists(string.Empty));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [TestMethod]
    public void GetEntriesReturnsFilesAndFolders()
    {
        var browser = new NativeFileSystemBrowser();
        var path = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(path, "inner"));
            File.WriteAllText(Path.Combine(path, "note.txt"), "hello");

            var entries = browser.GetEntries(path);

            Assert.HasCount(2, entries);

            var folder = entries.Single(entry => entry.IsFolder);
            Assert.AreEqual("inner", folder.Name);
            Assert.AreEqual(FileSystemEntryKind.Folder, folder.Kind);
            Assert.IsNull(folder.Size);

            var file = entries.Single(entry => !entry.IsFolder);
            Assert.AreEqual("note.txt", file.Name);
            Assert.AreEqual(FileSystemEntryKind.File, file.Kind);
            Assert.AreEqual(5L, file.Size!.Value);
            Assert.AreEqual(".txt", file.Extension);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [TestMethod]
    public void GetEntriesReturnsEmptyListForMissingOrBlankPath()
    {
        var browser = new NativeFileSystemBrowser();
        var path = CreateTemporaryDirectory();

        try
        {
            Assert.IsEmpty(browser.GetEntries(Path.Combine(path, "missing-child")));
            Assert.IsEmpty(browser.GetEntries("   "));
            Assert.IsEmpty(browser.GetEntries(string.Empty));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [TestMethod]
    public void GetDrivesListsFixedDrives()
    {
        var browser = new NativeFileSystemBrowser();
        var drives = browser.GetDrives();

        Assert.IsNotEmpty(drives);
        Assert.IsTrue(drives.All(drive => drive.RootPath.Length > 0));
        Assert.IsTrue(drives.Any(drive => drive.Kind == DriveKind.Fixed));
    }

    [TestMethod]
    public void GetSystemFoldersReturnsExistingFolders()
    {
        var browser = new NativeFileSystemBrowser();
        var folders = browser.GetSystemFolders();

        Assert.IsNotEmpty(folders);
        Assert.IsTrue(folders.All(folder => folder.Name.Length > 0));
        Assert.IsTrue(folders.All(folder => Directory.Exists(folder.Path)));
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"glaze-explorer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
