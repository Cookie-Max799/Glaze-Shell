using GlazeShell.Data.Serialization;

namespace GlazeShell.Data.Tests;

[TestClass]
public sealed class PersistenceLimitsTests
{
    [TestMethod]
    public void IsValidIdentifierRejectsBlankValues()
    {
        Assert.IsFalse(PersistenceLimits.IsValidIdentifier(null));
        Assert.IsFalse(PersistenceLimits.IsValidIdentifier(string.Empty));
        Assert.IsFalse(PersistenceLimits.IsValidIdentifier("   "));
    }

    [TestMethod]
    public void IsValidIdentifierEnforcesMaxLength()
    {
        Assert.IsTrue(PersistenceLimits.IsValidIdentifier(new string('a', PersistenceLimits.MaxIdentifierLength)));
        Assert.IsFalse(PersistenceLimits.IsValidIdentifier(new string('a', PersistenceLimits.MaxIdentifierLength + 1)));
    }

    [TestMethod]
    public void IsValidIdentifierRejectsControlCharacters()
    {
        Assert.IsFalse(PersistenceLimits.IsValidIdentifier("tab\n1"));
    }

    [TestMethod]
    public void IsValidNameRejectsBlankAndControlValues()
    {
        Assert.IsFalse(PersistenceLimits.IsValidName(null));
        Assert.IsFalse(PersistenceLimits.IsValidName("  "));
        Assert.IsFalse(PersistenceLimits.IsValidName("a\tb"));
    }

    [TestMethod]
    public void IsValidNameEnforcesMaxLength()
    {
        Assert.IsTrue(PersistenceLimits.IsValidName(new string('a', PersistenceLimits.MaxNameLength)));
        Assert.IsFalse(PersistenceLimits.IsValidName(new string('a', PersistenceLimits.MaxNameLength + 1)));
    }

    [TestMethod]
    public void ContainsControlCharactersDetectsOnlyControlCharacters()
    {
        Assert.IsFalse(PersistenceLimits.ContainsControlCharacters("readable text"));
        Assert.IsFalse(PersistenceLimits.ContainsControlCharacters(string.Empty));
        Assert.IsTrue(PersistenceLimits.ContainsControlCharacters("line\nbreak"));
    }
}
