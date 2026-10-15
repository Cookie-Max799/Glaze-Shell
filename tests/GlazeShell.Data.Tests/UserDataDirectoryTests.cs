using GlazeShell.Data.Persistence;

namespace GlazeShell.Data.Tests;

[TestClass]
public sealed class UserDataDirectoryTests
{
    [TestMethod]
    public void RejectsReservedDeviceNames()
    {
        using var temp = new TempUserData(createThemesDirectory: false);
        var directory = new UserDataDirectory(temp.Root);

        foreach (var fileName in new[] { "CON", "con.json", "NUL", "PRN.txt", "AUX", "COM1", "lpt9.json" })
        {
            Assert.ThrowsExactly<PersistenceException>(() => directory.GetDocumentPath(fileName));
        }
    }

    [TestMethod]
    public void AcceptsNamesThatOnlyResembleDeviceNames()
    {
        using var temp = new TempUserData(createThemesDirectory: false);
        var directory = new UserDataDirectory(temp.Root);

        foreach (var fileName in new[] { "COM10.json", "CONSOLE.json", "COMM1.json", "NUL2.json" })
        {
            Assert.IsTrue(
                directory.GetDocumentPath(fileName).EndsWith(fileName, StringComparison.OrdinalIgnoreCase),
                fileName);
        }
    }

    [TestMethod]
    public void RejectsNamesWithTrailingDotOrSpace()
    {
        using var temp = new TempUserData(createThemesDirectory: false);
        var directory = new UserDataDirectory(temp.Root);

        // Win32 отбрасывает завершающие точку и пробел, поэтому «settings.json.»
        // открывает тот же файл, что и «settings.json».
        foreach (var fileName in new[] { "settings.json.", "settings.json " })
        {
            Assert.ThrowsExactly<PersistenceException>(() => directory.GetDocumentPath(fileName));
        }
    }

    [TestMethod]
    public void RejectsSeparatorsAndTraversal()
    {
        using var temp = new TempUserData(createThemesDirectory: false);
        var directory = new UserDataDirectory(temp.Root);

        foreach (var fileName in new[] { "sub/settings.json", @"sub\settings.json", "settings..json", @"C:\settings.json" })
        {
            Assert.ThrowsExactly<PersistenceException>(() => directory.GetDocumentPath(fileName));
        }
    }

    [TestMethod]
    public void WritesAndReadsDocument()
    {
        using var temp = new TempUserData(createThemesDirectory: false);
        var directory = new UserDataDirectory(temp.Root);
        const string content = "{\"a\":1}";

        directory.WriteDocument("settings.json", content, maxBytes: 1024);

        var result = directory.ReadDocument("settings.json", maxBytes: 1024);

        Assert.AreEqual(DocumentReadStatus.Loaded, result.Status);
        Assert.AreEqual(content, result.Content);
    }

    [TestMethod]
    public void ReportsDocumentAboveTheReadLimit()
    {
        using var temp = new TempUserData(createThemesDirectory: false);
        var directory = new UserDataDirectory(temp.Root);
        directory.WriteDocument("settings.json", new string('x', 64), maxBytes: 1024);

        var result = directory.ReadDocument("settings.json", maxBytes: 8);

        Assert.AreEqual(DocumentReadStatus.TooLarge, result.Status);
        Assert.IsNull(result.Content);
        StringAssert.Contains(result.Failure!, "the limit is 8 bytes");
    }

    [TestMethod]
    public void ReportsMissingDocument()
    {
        using var temp = new TempUserData(createThemesDirectory: false);
        var directory = new UserDataDirectory(temp.Root);

        var result = directory.ReadDocument("absent.json", maxBytes: 1024);

        Assert.AreEqual(DocumentReadStatus.Missing, result.Status);
    }

    [TestMethod]
    public void RejectsWritingAboveTheLimit()
    {
        using var temp = new TempUserData(createThemesDirectory: false);
        var directory = new UserDataDirectory(temp.Root);

        Assert.ThrowsExactly<PersistenceLimitExceededException>(
            () => directory.WriteDocument("settings.json", new string('x', 64), maxBytes: 8));

        Assert.IsFalse(temp.FileExists("settings.json"));
    }
}
