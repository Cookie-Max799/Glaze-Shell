using GlazeShell.Core.Configuration;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class UserDataDirectoryNameTests
{
    [TestMethod]
    public void AcceptsOrdinaryNames()
    {
        Assert.AreEqual("GlazeShell", UserDataDirectoryName.Validate("GlazeShell"));
        Assert.IsTrue(UserDataDirectoryName.IsValid("Glaze Shell 2"));
        Assert.IsTrue(UserDataDirectoryName.IsValid("custom.data"));
    }

    [TestMethod]
    public void RejectsReservedDeviceNames()
    {
        Assert.IsFalse(UserDataDirectoryName.IsValid("NUL"));
        Assert.IsFalse(UserDataDirectoryName.IsValid("con.txt"));
        Assert.IsFalse(UserDataDirectoryName.IsValid("LPT3"));
    }

    [TestMethod]
    public void RejectsTraversalAndSeparators()
    {
        Assert.IsFalse(UserDataDirectoryName.IsValid(".."));
        Assert.IsFalse(UserDataDirectoryName.IsValid("."));
        Assert.IsFalse(UserDataDirectoryName.IsValid(@"..\Windows"));
        Assert.IsFalse(UserDataDirectoryName.IsValid("a/b"));
    }

    [TestMethod]
    public void RejectsTrailingDotOrSpace()
    {
        Assert.IsFalse(UserDataDirectoryName.IsValid("GlazeShell."));
        Assert.IsFalse(UserDataDirectoryName.IsValid("GlazeShell "));
    }

    [TestMethod]
    public void RejectsEmptyAndExcessiveNames()
    {
        Assert.IsFalse(UserDataDirectoryName.IsValid(null));
        Assert.IsFalse(UserDataDirectoryName.IsValid("   "));
        Assert.IsFalse(UserDataDirectoryName.IsValid(new string('a', UserDataDirectoryName.MaxLength + 1)));
        Assert.IsTrue(UserDataDirectoryName.IsValid(new string('a', UserDataDirectoryName.MaxLength)));
    }

    [TestMethod]
    public void ValidateThrowsForInvalidNames()
    {
        Assert.ThrowsExactly<ArgumentException>(() => UserDataDirectoryName.Validate("NUL"));
    }
}
