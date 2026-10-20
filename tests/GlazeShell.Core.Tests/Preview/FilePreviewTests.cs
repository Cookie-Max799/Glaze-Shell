using GlazeShell.Core.Preview;

namespace GlazeShell.Core.Tests.Preview;

[TestClass]
public sealed class FilePreviewTests
{
    [TestMethod]
    public void ClassifyAlwaysReturnsFolderForDirectories()
    {
        Assert.AreEqual(FilePreviewKind.Folder, FilePreview.Classify("anything.xyz", isDirectory: true));
        Assert.AreEqual(FilePreviewKind.Folder, FilePreview.Classify("no-extension", isDirectory: true));
    }

    [TestMethod]
    public void ClassifyRecognizesImagesIgnoringCase()
    {
        Assert.AreEqual(FilePreviewKind.Image, FilePreview.Classify("photo.png", isDirectory: false));
        Assert.AreEqual(FilePreviewKind.Image, FilePreview.Classify("PHOTO.JPG", isDirectory: false));
        Assert.AreEqual(FilePreviewKind.Image, FilePreview.Classify(@"C:\Temp\scan.WebP", isDirectory: false));
    }

    [TestMethod]
    public void ClassifyRecognizesTextAndCode()
    {
        Assert.AreEqual(FilePreviewKind.Text, FilePreview.Classify("notes.txt", isDirectory: false));
        Assert.AreEqual(FilePreviewKind.Text, FilePreview.Classify("readme.md", isDirectory: false));
        Assert.AreEqual(FilePreviewKind.Text, FilePreview.Classify("settings.json", isDirectory: false));
        Assert.AreEqual(FilePreviewKind.Text, FilePreview.Classify("Program.cs", isDirectory: false));
        Assert.AreEqual(FilePreviewKind.Text, FilePreview.Classify(".gitignore", isDirectory: false));
    }

    [TestMethod]
    public void ClassifyRecognizesAudioVideoAndArchives()
    {
        Assert.AreEqual(FilePreviewKind.Audio, FilePreview.Classify("track.mp3", isDirectory: false));
        Assert.AreEqual(FilePreviewKind.Video, FilePreview.Classify("clip.mkv", isDirectory: false));
        Assert.AreEqual(FilePreviewKind.Archive, FilePreview.Classify("backup.7z", isDirectory: false));
    }

    [TestMethod]
    public void ClassifyFallsBackToOtherForUnknownOrMissingExtension()
    {
        Assert.AreEqual(FilePreviewKind.Other, FilePreview.Classify("setup.bin", isDirectory: false));
        Assert.AreEqual(FilePreviewKind.Other, FilePreview.Classify("LICENSE", isDirectory: false));
        Assert.AreEqual(FilePreviewKind.Other, FilePreview.Classify("trailing.", isDirectory: false));
        Assert.AreEqual(FilePreviewKind.Other, FilePreview.Classify(null, isDirectory: false));
        Assert.AreEqual(FilePreviewKind.Other, FilePreview.Classify(string.Empty, isDirectory: false));
    }
}
