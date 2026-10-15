using System.Text;
using GlazeShell.Data.Persistence;

namespace GlazeShell.Data.Tests;

[TestClass]
public sealed class BoundedTextFileTests
{
    [TestMethod]
    public void ReadsTextWithinTheLimit()
    {
        using var temp = new TempUserData(createThemesDirectory: false);
        const string content = "{\"a\":1}";
        var path = temp.GetPath("document.json");
        File.WriteAllText(path, content);

        Assert.IsTrue(BoundedTextFile.TryRead(path, 1024, out var text, out var byteLength));

        Assert.AreEqual(content, text);
        Assert.AreEqual(new FileInfo(path).Length, byteLength);
    }

    [TestMethod]
    public void ReadsEmptyFile()
    {
        using var temp = new TempUserData(createThemesDirectory: false);
        var path = temp.GetPath("empty.json");
        File.WriteAllText(path, string.Empty);

        Assert.IsTrue(BoundedTextFile.TryRead(path, 1024, out var text, out var byteLength));

        Assert.AreEqual(string.Empty, text);
        Assert.AreEqual(0L, byteLength);
    }

    [TestMethod]
    public void RejectsFileAboveTheLimit()
    {
        using var temp = new TempUserData(createThemesDirectory: false);
        var path = temp.GetPath("document.json");
        File.WriteAllText(path, new string('x', 100));

        Assert.IsFalse(BoundedTextFile.TryRead(path, 10, out var text, out var byteLength));

        Assert.AreEqual(string.Empty, text);
        Assert.AreEqual(100L, byteLength);
    }

    [TestMethod]
    public void SkipsTheUtf8ByteOrderMark()
    {
        using var temp = new TempUserData(createThemesDirectory: false);
        const string content = "{\"a\":1}";
        var path = temp.GetPath("document.json");
        File.WriteAllBytes(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(content));

        Assert.IsTrue(BoundedTextFile.TryRead(path, 1024, out var text, out _));

        Assert.AreEqual(content, text);
    }

    [TestMethod]
    public void DecodesUtf16ContentMarkedByItsByteOrderMark()
    {
        using var temp = new TempUserData(createThemesDirectory: false);
        const string content = "{\"a\":1}";
        var path = temp.GetPath("document.json");
        File.WriteAllText(path, content, Encoding.Unicode);

        Assert.IsTrue(BoundedTextFile.TryRead(path, 1024, out var text, out _));

        Assert.AreEqual(content, text);
    }

    [TestMethod]
    public void ReportsMissingFileAsFileNotFound()
    {
        using var temp = new TempUserData(createThemesDirectory: false);
        var path = temp.GetPath("missing.json");

        Assert.ThrowsExactly<FileNotFoundException>(() => BoundedTextFile.TryRead(path, 1024, out _, out _));
    }
}
