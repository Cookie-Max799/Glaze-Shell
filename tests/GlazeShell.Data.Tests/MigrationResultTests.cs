using GlazeShell.Data.Persistence;

namespace GlazeShell.Data.Tests;

[TestClass]
public sealed class MigrationResultTests
{
    private static readonly int[] AppliedVersionRange = [1, 2];

    [TestMethod]
    public void NotMigratedWhenNoVersionsApplied()
    {
        var result = new MigrationResult(1, 1);

        Assert.AreEqual(1, result.FromVersion);
        Assert.AreEqual(1, result.ToVersion);
        Assert.IsEmpty(result.AppliedVersions);
        Assert.IsFalse(result.Migrated);
    }

    [TestMethod]
    public void ReportsAppliedVersions()
    {
        var result = new MigrationResult(1, 3, [1, 2]);

        Assert.IsTrue(result.Migrated);
        CollectionAssert.AreEqual(AppliedVersionRange, result.AppliedVersions.ToArray());
    }

    [TestMethod]
    public void CopiesAppliedVersions()
    {
        var versions = new List<int> { 1 };
        var result = new MigrationResult(1, 2, versions);
        versions.Add(2);

        Assert.HasCount(1, result.AppliedVersions);
    }

    [TestMethod]
    public void RejectsNonPositiveSourceVersion()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MigrationResult(0, 1));
    }

    [TestMethod]
    public void RejectsTargetLowerThanSource()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MigrationResult(2, 1));
    }
}
