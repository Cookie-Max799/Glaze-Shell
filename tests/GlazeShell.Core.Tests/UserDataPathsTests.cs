using GlazeShell.Core.Configuration;
using GlazeShell.Infrastructure.System;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class UserDataPathsTests
{
    private static GlazeShellConfiguration CreateConfiguration(string dataDirectoryName) =>
        new() { DataDirectoryName = dataDirectoryName };

    [TestMethod]
    public void DefaultConfigurationResolvesUnderLocalAppData()
    {
        var root = UserDataPaths.GetRootDirectory(GlazeShellConfiguration.CreateDefault());

        StringAssert.Contains(root, "GlazeShell", StringComparison.Ordinal);
        StringAssert.EndsWith(root, "GlazeShell");
    }

    [TestMethod]
    public void RejectsRelativeTraversal()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => UserDataPaths.GetRootDirectory(CreateConfiguration("..")));
    }

    [TestMethod]
    public void RejectsPathSeparators()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => UserDataPaths.GetRootDirectory(CreateConfiguration(@"..\Windows")));
    }

    [TestMethod]
    public void RejectsReservedDeviceName()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => UserDataPaths.GetRootDirectory(CreateConfiguration("NUL")));
    }

    [TestMethod]
    public void RejectsReservedDeviceNameWithExtension()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => UserDataPaths.GetRootDirectory(CreateConfiguration("con.txt")));
    }

    [TestMethod]
    public void RejectsTrailingDot()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => UserDataPaths.GetRootDirectory(CreateConfiguration("GlazeShell.")));
    }

    [TestMethod]
    public void RejectsTrailingSpace()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => UserDataPaths.GetRootDirectory(CreateConfiguration("GlazeShell ")));
    }

    [TestMethod]
    public void RejectsExcessiveLength()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => UserDataPaths.GetRootDirectory(CreateConfiguration(new string('a', 65))));
    }

    [TestMethod]
    public void RejectsEmptyName()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => UserDataPaths.GetRootDirectory(CreateConfiguration("   ")));
    }
}
