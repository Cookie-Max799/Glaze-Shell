using GlazeShell.Core.Files;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class FileSystemSorterTests
{
    private static FileSystemEntry CreateFolder(string name, DateTimeOffset? modified = null) =>
        new(name, $@"C:\{name}", FileSystemEntryKind.Folder, null, modified);

    private static FileSystemEntry CreateFile(string name, long size, DateTimeOffset? modified = null) =>
        new(name, $@"C:\{name}", FileSystemEntryKind.File, size, modified);

    [TestMethod]
    public void FoldersComeFirstEvenWhenAFileSortsBeforeThem()
    {
        var entries = new List<FileSystemEntry>
        {
            CreateFile("a.txt", 1),
            CreateFolder("z-folder"),
            CreateFolder("b-folder"),
        };

        var sorted = FileSystemSorter.Sort(entries, FileSystemSortColumn.Name, FileSystemSortDirection.Ascending);

        Assert.HasCount(3, sorted);
        Assert.AreEqual("b-folder", sorted[0].Name);
        Assert.AreEqual("z-folder", sorted[1].Name);
        Assert.AreEqual("a.txt", sorted[2].Name);
    }

    [TestMethod]
    public void NamesAreSortedCaseInsensitively()
    {
        var entries = new List<FileSystemEntry>
        {
            CreateFile("B.txt", 1),
            CreateFile("a.txt", 2),
            CreateFile("C.txt", 3),
        };

        var sorted = FileSystemSorter.Sort(entries, FileSystemSortColumn.Name, FileSystemSortDirection.Ascending);

        Assert.AreEqual("a.txt", sorted[0].Name);
        Assert.AreEqual("B.txt", sorted[1].Name);
        Assert.AreEqual("C.txt", sorted[2].Name);
    }

    [TestMethod]
    public void DescendingReversesEachGroupButKeepsFoldersOnTop()
    {
        var entries = new List<FileSystemEntry>
        {
            CreateFile("a.txt", 1),
            CreateFile("c.txt", 3),
            CreateFolder("first"),
            CreateFolder("second"),
        };

        var sorted = FileSystemSorter.Sort(entries, FileSystemSortColumn.Name, FileSystemSortDirection.Descending);

        Assert.AreEqual("second", sorted[0].Name);
        Assert.AreEqual("first", sorted[1].Name);
        Assert.AreEqual("c.txt", sorted[2].Name);
        Assert.AreEqual("a.txt", sorted[3].Name);
    }

    [TestMethod]
    public void SizeSortOrdersFilesByBytes()
    {
        var entries = new List<FileSystemEntry>
        {
            CreateFile("small.txt", 10),
            CreateFile("huge.txt", 4096),
            CreateFile("middle.txt", 256),
        };

        var sorted = FileSystemSorter.Sort(entries, FileSystemSortColumn.Size, FileSystemSortDirection.Descending);

        Assert.AreEqual("huge.txt", sorted[0].Name);
        Assert.AreEqual("middle.txt", sorted[1].Name);
        Assert.AreEqual("small.txt", sorted[2].Name);
    }

    [TestMethod]
    public void ModifiedSortUsesOldestFirstAndTreatsMissingDateAsMinimum()
    {
        var oldest = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var newest = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var entries = new List<FileSystemEntry>
        {
            CreateFile("new.txt", 1, newest),
            CreateFile("undated.txt", 2),
            CreateFile("old.txt", 3, oldest),
        };

        var sorted = FileSystemSorter.Sort(entries, FileSystemSortColumn.Modified, FileSystemSortDirection.Ascending);

        Assert.AreEqual("undated.txt", sorted[0].Name);
        Assert.AreEqual("old.txt", sorted[1].Name);
        Assert.AreEqual("new.txt", sorted[2].Name);
    }

    [TestMethod]
    public void KindSortGroupsByExtension()
    {
        var entries = new List<FileSystemEntry>
        {
            CreateFile("note.txt", 1),
            CreateFile("image.png", 2),
            CreateFile("archive.zip", 3),
        };

        var sorted = FileSystemSorter.Sort(entries, FileSystemSortColumn.Kind, FileSystemSortDirection.Ascending);

        Assert.AreEqual("image.png", sorted[0].Name);
        Assert.AreEqual("note.txt", sorted[1].Name);
        Assert.AreEqual("archive.zip", sorted[2].Name);
    }

    [TestMethod]
    public void EqualKeysKeepTheOriginalOrder()
    {
        var first = CreateFile("same.txt", 1);
        var second = CreateFile("same.txt", 1);

        var sorted = FileSystemSorter.Sort([first, second], FileSystemSortColumn.Name, FileSystemSortDirection.Ascending);

        Assert.AreSame(first, sorted[0]);
        Assert.AreSame(second, sorted[1]);
    }

    [TestMethod]
    public void SortingRejectsNullList()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => FileSystemSorter.Sort(
            null!,
            FileSystemSortColumn.Name,
            FileSystemSortDirection.Ascending));
    }
}
