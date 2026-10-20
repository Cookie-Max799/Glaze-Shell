using GlazeShell.Core.Models;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class ExplorerViewModeTests
{
    [TestMethod]
    public void ToIdProducesDocumentIdentifiers()
    {
        Assert.AreEqual("icons", ExplorerViewMode.Icons.ToId());
        Assert.AreEqual("list", ExplorerViewMode.List.ToId());
    }

    [TestMethod]
    public void ParseAcceptsIdentifiersRegardlessOfCase()
    {
        Assert.IsTrue(ExplorerViewModeExtensions.TryParse("icons", out var icons));
        Assert.AreEqual(ExplorerViewMode.Icons, icons);

        Assert.IsTrue(ExplorerViewModeExtensions.TryParse("  LIST  ", out var list));
        Assert.AreEqual(ExplorerViewMode.List, list);
    }

    [TestMethod]
    public void ParseRejectsUnknownValueAndFallsBackToIcons()
    {
        Assert.IsFalse(ExplorerViewModeExtensions.TryParse("gallery", out var mode));
        Assert.AreEqual(ExplorerViewMode.Icons, mode);

        Assert.IsFalse(ExplorerViewModeExtensions.TryParse(null, out mode));
        Assert.AreEqual(ExplorerViewMode.Icons, mode);
    }
}
