using System.Text.Json.Nodes;
using GlazeShell.Data.Persistence;

namespace GlazeShell.Data.Tests;

[TestClass]
public sealed class SchemaMigrationRunnerTests
{
    [TestMethod]
    public void AppliesMigrationsInSequence()
    {
        var runner = new SchemaMigrationRunner(3, UserDataDocumentKind.Settings, [new Migration(1, "second"), new Migration(2, "third")]);

        var (document, result) = runner.ParseAndMigrate("""{ "schemaVersion": 1, "value": "first" }""", 1);

        Assert.AreEqual("third", document["value"]!.GetValue<string>());
        Assert.AreEqual(3, document["schemaVersion"]!.GetValue<int>());
        Assert.AreEqual(1, result.FromVersion);
        Assert.AreEqual(3, result.ToVersion);
        CollectionAssert.AreEqual(new List<int> { 1, 2 }, result.AppliedVersions.ToList());
    }

    [TestMethod]
    public void CurrentDocumentIsNotChanged()
    {
        var runner = new SchemaMigrationRunner(2, UserDataDocumentKind.Settings, [new Migration(1, "second")]);

        var (document, result) = runner.ParseAndMigrate("""{ "schemaVersion": 2, "value": "second" }""", 2);

        Assert.AreEqual("second", document["value"]!.GetValue<string>());
        Assert.IsFalse(result.Migrated);
        Assert.IsEmpty(result.AppliedVersions);
    }

    [TestMethod]
    public void MissingMigrationLinkIsReported()
    {
        var runner = new SchemaMigrationRunner(3, UserDataDocumentKind.Settings, [new Migration(1, "second")]);

        var exception = Assert.ThrowsExactly<PersistenceException>(
            () => runner.ParseAndMigrate("""{ "schemaVersion": 1 }""", 1));

        StringAssert.Contains(exception.Message, "version 2");
    }

    [TestMethod]
    public void NewerDocumentIsReportedAsUnsupported()
    {
        var runner = new SchemaMigrationRunner(1, UserDataDocumentKind.Settings);

        var exception = Assert.ThrowsExactly<UnsupportedSchemaVersionException>(
            () => runner.ParseAndMigrate("""{ "schemaVersion": 5 }""", 5));

        Assert.AreEqual(5, exception.DocumentVersion);
        Assert.AreEqual(1, exception.CurrentVersion);
    }

    [TestMethod]
    public void InvalidJsonIsReported()
    {
        var runner = new SchemaMigrationRunner(1, UserDataDocumentKind.Settings);

        Assert.ThrowsExactly<PersistenceException>(() => runner.ParseAndMigrate("{ not json", 1));
        Assert.ThrowsExactly<PersistenceException>(() => runner.ParseAndMigrate("[1, 2, 3]", 1));
    }

    [TestMethod]
    public void DuplicatedMigrationForTheSameVersionIsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => new SchemaMigrationRunner(2, UserDataDocumentKind.Settings, [new Migration(1, "a"), new Migration(1, "b")]));
    }

    [TestMethod]
    public void MigrationOutOfRangeIsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new SchemaMigrationRunner(1, UserDataDocumentKind.Settings, [new Migration(1, "a")]));
        Assert.ThrowsExactly<ArgumentException>(() => new SchemaMigrationRunner(3, UserDataDocumentKind.Settings, [new Migration(3, "a")]));
    }

    [TestMethod]
    public void MigrationsOfAnotherDocumentAreNotApplied()
    {
        var runner = new SchemaMigrationRunner(2, UserDataDocumentKind.Layout, [new Migration(1, "second")]);

        // The chain is built for a single document, so a foreign migration cannot silently migrate it.
        Assert.ThrowsExactly<PersistenceException>(
            () => runner.ParseAndMigrate("""{ "schemaVersion": 1 }""", 1));
    }

    [TestMethod]
    public void UndefinedDocumentKindIsRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new SchemaMigrationRunner(1, (UserDataDocumentKind)42));
    }

    [TestMethod]
    public void CurrentVersionMustBePositive()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SchemaMigrationRunner(0, UserDataDocumentKind.Settings));
    }

    [TestMethod]
    public void SchemaVersionIsReadOnlyWhenValid()
    {
        var runner = new SchemaMigrationRunner(1, UserDataDocumentKind.Settings);

        var version = SchemaMigrationRunner.ReadSchemaVersion(new JsonObject { ["schemaVersion"] = 1 });
        var missing = SchemaMigrationRunner.ReadSchemaVersion(new JsonObject());
        var invalid = SchemaMigrationRunner.ReadSchemaVersion(new JsonObject { ["schemaVersion"] = "one" });

        Assert.AreEqual(1, version);
        Assert.IsNull(missing);
        Assert.IsNull(invalid);
    }

    private sealed class Migration(int fromVersion, string value) : IDataMigration
    {
        public UserDataDocumentKind Document => UserDataDocumentKind.Settings;

        public int FromVersion { get; } = fromVersion;

        public void Apply(JsonObject document) => document["value"] = value;
    }
}
