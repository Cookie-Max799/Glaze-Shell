using System.Text;
using GlazeShell.Core.Preview;
using GlazeShell.Windows.FileSystem;

namespace GlazeShell.Windows.Tests.FileSystem;

[TestClass]
public sealed class NativeFilePreviewReaderTests
{
    [TestMethod]
    public void ReadReturnsNullForMissingOrBlankPath()
    {
        var reader = new NativeFilePreviewReader();
        var path = CreateTemporaryDirectory();

        try
        {
            Assert.IsNull(reader.Read(null!));
            Assert.IsNull(reader.Read("   "));
            Assert.IsNull(reader.Read(string.Empty));
            Assert.IsNull(reader.Read(Path.Combine(path, "missing.txt")));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [TestMethod]
    public void ReadFolderReturnsSummaryWithItemCount()
    {
        var reader = new NativeFilePreviewReader();
        var path = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(path, "inner"));
            File.WriteAllText(Path.Combine(path, "a.txt"), "a");
            File.WriteAllText(Path.Combine(path, "b.txt"), "b");

            var preview = reader.Read(path);

            Assert.IsNotNull(preview);
            Assert.AreEqual(FilePreviewKind.Folder, preview!.Kind);
            Assert.AreEqual(3, preview.ItemCount);
            Assert.IsNull(preview.Content);
            Assert.IsFalse(preview.IsTruncated);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [TestMethod]
    public void ReadPlainTextFileReturnsItsContent()
    {
        var reader = new NativeFilePreviewReader();
        var path = CreateTemporaryDirectory();

        try
        {
            var file = Path.Combine(path, "notes.txt");
            File.WriteAllText(file, "первая строка" + Environment.NewLine + "вторая строка");

            var preview = reader.Read(file);

            Assert.IsNotNull(preview);
            Assert.AreEqual(FilePreviewKind.Text, preview!.Kind);
            Assert.AreEqual("первая строка" + Environment.NewLine + "вторая строка", preview.Content);
            Assert.IsFalse(preview.IsTruncated);
            Assert.AreEqual("notes.txt", preview.Title);
            Assert.AreEqual(new FileInfo(file).Length, preview.SizeBytes);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [TestMethod]
    public void ReadStripsByteOrderMarkAndDetectsEncoding()
    {
        var reader = new NativeFilePreviewReader();
        var path = CreateTemporaryDirectory();

        try
        {
            var utf8 = Path.Combine(path, "utf8.txt");
            File.WriteAllBytes(utf8, [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("привет")]);

            var utf16 = Path.Combine(path, "utf16.txt");
            File.WriteAllBytes(utf16, [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("мир")]);

            Assert.AreEqual("привет", reader.Read(utf8)?.Content);
            Assert.AreEqual("мир", reader.Read(utf16)?.Content);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [TestMethod]
    public void ReadTreatsBinaryFileWithZeroBytesAsNotText()
    {
        var reader = new NativeFilePreviewReader();
        var path = CreateTemporaryDirectory();

        try
        {
            var file = Path.Combine(path, "fake.txt");
            File.WriteAllBytes(file, [0x01, 0x00, 0x02, 0x00, 0x03]);

            var preview = reader.Read(file);

            Assert.IsNotNull(preview);
            Assert.IsNull(preview!.Content);
            Assert.AreEqual(FilePreviewKind.Text, preview.Kind);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [TestMethod]
    public void ReadTruncatesLongTextFile()
    {
        var reader = new NativeFilePreviewReader();
        var path = CreateTemporaryDirectory();

        try
        {
            var file = Path.Combine(path, "long.txt");
            File.WriteAllText(file, new string('a', NativeFilePreviewReader.MaxTextCharacters + 1000));

            var preview = reader.Read(file);

            Assert.IsNotNull(preview);
            Assert.IsTrue(preview!.IsTruncated);
            Assert.HasCount(NativeFilePreviewReader.MaxTextCharacters, preview.Content!);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [TestMethod]
    public void ReadNonTextKindsReturnsMetadataOnly()
    {
        var reader = new NativeFilePreviewReader();
        var path = CreateTemporaryDirectory();

        try
        {
            var image = Path.Combine(path, "photo.png");
            File.WriteAllBytes(image, [0x89, 0x50, 0x4E, 0x47]);

            var archive = Path.Combine(path, "backup.zip");
            File.WriteAllBytes(archive, [0x50, 0x4B]);

            var imagePreview = reader.Read(image);
            Assert.IsNotNull(imagePreview);
            Assert.AreEqual(FilePreviewKind.Image, imagePreview!.Kind);
            Assert.IsNull(imagePreview.Content);
            Assert.AreEqual(4, imagePreview.SizeBytes);

            var archivePreview = reader.Read(archive);
            Assert.IsNotNull(archivePreview);
            Assert.AreEqual(FilePreviewKind.Archive, archivePreview!.Kind);
            Assert.IsNull(archivePreview.Content);
            Assert.AreEqual(2, archivePreview.SizeBytes);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"glaze-preview-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
