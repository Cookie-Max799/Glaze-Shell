using System.Text.Json.Nodes;
using GlazeShell.Core.Configuration;
using GlazeShell.Core.Models;
using GlazeShell.Core.Persistence;
using GlazeShell.Data.Persistence;
using GlazeShell.Data.Serialization;

namespace GlazeShell.Data.Tests;

[TestClass]
public sealed class JsonUserDataStoreTests
{
    [TestMethod]
    public void MissingDocumentsAreCreatedWithDefaults()
    {
        using var data = new TempUserData();
        var store = data.CreateStore();

        var configuration = store.LoadConfiguration();
        var settings = store.LoadSettings();
        var layout = store.LoadLayout();

        Assert.AreEqual(PersistenceStatus.Created, configuration.Status);
        Assert.AreEqual(PersistenceStatus.Created, settings.Status);
        Assert.AreEqual(PersistenceStatus.Created, layout.Status);
        Assert.IsTrue(data.FileExists(UserDataFileNames.Configuration));
        Assert.IsTrue(data.FileExists(UserDataFileNames.Settings));
        Assert.IsTrue(data.FileExists(UserDataFileNames.Layout));
        Assert.AreEqual("Glaze Shell", configuration.Value.ApplicationName);
        Assert.AreEqual("GlazeShell", configuration.Value.DataDirectoryName);
        Assert.AreEqual(UserSettings.CreateDefault(), settings.Value);
        Assert.IsEmpty(layout.Value.Tabs);
    }

    [TestMethod]
    public void CreatedDocumentsCarrySchemaVersion()
    {
        using var data = new TempUserData();
        var store = data.CreateStore();

        _ = store.LoadSettings();

        StringAssert.Contains(data.Read(UserDataFileNames.Settings), "\"schemaVersion\": 1");
    }

    [TestMethod]
    public void SettingsRoundTripPreservesValues()
    {
        using var data = new TempUserData();
        var store = data.CreateStore();
        var expected = TempUserData.CreateSettings();

        store.SaveSettings(expected);

        var loaded = data.CreateStore().LoadSettings();

        Assert.AreEqual(PersistenceStatus.Loaded, loaded.Status);
        Assert.AreEqual(expected, loaded.Value);
    }

    [TestMethod]
    public void ConfigurationRoundTripPreservesValues()
    {
        using var data = new TempUserData();
        var store = data.CreateStore();
        var expected = TempUserData.CreateConfiguration("Glaze", "GlazeShellCustom");

        store.SaveConfiguration(expected);

        var loaded = data.CreateStore().LoadConfiguration();

        Assert.AreEqual(PersistenceStatus.Loaded, loaded.Status);
        Assert.AreEqual(expected.ApplicationName, loaded.Value.ApplicationName);
        Assert.AreEqual(expected.DataDirectoryName, loaded.Value.DataDirectoryName);
    }

    [TestMethod]
    public void LayoutRoundTripPreservesHierarchyAndMonitorBinding()
    {
        using var data = new TempUserData();
        var store = data.CreateStore();

        var item = new DesktopItem("item-1", "app-1", order: 0, label: "Terminal");
        var category = new ApplicationCategory("cat-1", "Development", [item], "Main group", order: 0);
        var bound = new DesktopTab("tab-1", "Work", [category], order: 0, monitorId: "00010001");
        var floating = new DesktopTab("tab-2", "Home", order: 1);
        var layout = new DesktopLayout([bound, floating], "tab-2", layoutVersion: 3);

        store.SaveLayout(layout);

        var loaded = data.CreateStore().LoadLayout();

        Assert.AreEqual(PersistenceStatus.Loaded, loaded.Status);
        var value = loaded.Value;
        Assert.HasCount(2, value.Tabs);
        Assert.AreEqual(3, value.LayoutVersion);
        Assert.AreEqual("tab-2", value.ActiveTabId);
        Assert.AreEqual("00010001", value.Tabs[0].MonitorId);
        Assert.IsNull(value.Tabs[1].MonitorId);
        Assert.HasCount(1, value.Tabs[0].Categories);
        Assert.AreEqual("Main group", value.Tabs[0].Categories[0].Description);
        Assert.AreEqual("Terminal", value.Tabs[0].Categories[0].Items[0].Label);
        Assert.AreEqual("app-1", value.Tabs[0].Categories[0].Items[0].ApplicationId);
    }

    [TestMethod]
    public void CorruptedDocumentIsIsolatedAndReplacedWithDefaults()
    {
        using var data = new TempUserData();
        data.Write(UserDataFileNames.Settings, "{ this is not json");

        var result = data.CreateStore().LoadSettings();

        Assert.AreEqual(PersistenceStatus.Recovered, result.Status);
        Assert.AreEqual(UserSettings.CreateDefault(), result.Value);
        Assert.IsTrue(result.UsedDefaults);

        var isolated = data.ListFiles(UserDataFileNames.RecoveryDirectory).ToArray();
        Assert.HasCount(1, isolated);
        StringAssert.StartsWith(isolated[0], UserDataFileNames.Settings);

        // The document is rewritten with defaults, so the next start is clean.
        Assert.AreEqual(PersistenceStatus.Loaded, data.CreateStore().LoadSettings().Status);
        StringAssert.StartsWith(data.Read(UserDataFileNames.Settings), "{");
    }

    [TestMethod]
    public void OversizedDocumentIsIsolatedWithoutParsing()
    {
        using var data = new TempUserData();
        var oversized = new string('x', (int)(PersistenceLimits.MaxDocumentBytes + 1024));
        data.Write(UserDataFileNames.Layout, oversized);

        var result = data.CreateStore().LoadLayout();

        Assert.AreEqual(PersistenceStatus.Recovered, result.Status);
        Assert.IsEmpty(result.Value.Tabs);
        Assert.HasCount(1, data.ListFiles(UserDataFileNames.RecoveryDirectory));
    }

    [TestMethod]
    public void DocumentWithDuplicateTabIdsIsRejected()
    {
        using var data = new TempUserData();
        data.Write(
            UserDataFileNames.Layout,
            """
            {
              "schemaVersion": 1,
              "layoutVersion": 1,
              "tabs": [
                { "id": "tab-1", "name": "First", "order": 0, "categories": [] },
                { "id": "tab-1", "name": "Second", "order": 1, "categories": [] }
              ]
            }
            """);

        var result = data.CreateStore().LoadLayout();

        Assert.AreEqual(PersistenceStatus.Recovered, result.Status);
        Assert.IsEmpty(result.Value.Tabs);
        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.Contains("duplicated", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void DocumentWithUnknownActiveTabIsRejected()
    {
        using var data = new TempUserData();
        data.Write(
            UserDataFileNames.Layout,
            """
            {
              "schemaVersion": 1,
              "layoutVersion": 1,
              "activeTabId": "tab-missing",
              "tabs": [ { "id": "tab-1", "name": "First", "order": 0, "categories": [] } ]
            }
            """);

        var result = data.CreateStore().LoadLayout();

        Assert.AreEqual(PersistenceStatus.Recovered, result.Status);
        Assert.IsNull(result.Value.ActiveTabId);
    }

    [TestMethod]
    public void DocumentWithoutSchemaVersionIsReadAsCurrentVersion()
    {
        using var data = new TempUserData();
        data.Write(
            UserDataFileNames.Layout,
            """
            {
              "layoutVersion": 1,
              "tabs": [ { "id": "tab-1", "name": "First", "order": 0, "categories": [] } ]
            }
            """);

        var result = data.CreateStore().LoadLayout();

        Assert.AreEqual(PersistenceStatus.Loaded, result.Status);
        Assert.HasCount(1, result.Value.Tabs);
    }

    [TestMethod]
    public void DocumentOfNewerSchemaVersionIsLeftUntouched()
    {
        using var data = new TempUserData();
        const string futureDocument = """{ "schemaVersion": 99, "applicationName": "Future" }""";
        data.Write(UserDataFileNames.Configuration, futureDocument);

        var result = data.CreateStore().LoadConfiguration();

        Assert.AreEqual(PersistenceStatus.Unsupported, result.Status);
        Assert.AreEqual(futureDocument, data.Read(UserDataFileNames.Configuration));
        Assert.IsEmpty(data.ListFiles(UserDataFileNames.RecoveryDirectory));
        Assert.IsTrue(result.UsedDefaults);
    }

    [TestMethod]
    public void MigratedDocumentCreatesBackupAndRewritesFile()
    {
        using var data = new TempUserData();
        data.Write(UserDataFileNames.Settings, """{ "schemaVersion": 1, "language": "old" }""");

        var store = data.CreateStore(new SchemaVersionSet(1, 2, 1), [new RenameLanguageMigration(1)]);
        var result = store.LoadSettings();

        Assert.AreEqual(PersistenceStatus.Migrated, result.Status);
        Assert.AreEqual("old", result.Value.Language);

        var backups = data.ListFiles(UserDataFileNames.BackupsDirectory).ToArray();
        Assert.HasCount(1, backups);
        StringAssert.Contains(data.Read(UserDataFileNames.Settings), "\"schemaVersion\": 2");
        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.Contains("backup", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void MigrationFailureFallsBackToBackedUpDefaults()
    {
        using var data = new TempUserData();
        data.Write(UserDataFileNames.Settings, """{ "schemaVersion": 1, "language": "old" }""");

        var store = data.CreateStore(new SchemaVersionSet(1, 2, 1), [new FailingMigration(1)]);
        var result = store.LoadSettings();

        Assert.AreEqual(PersistenceStatus.Recovered, result.Status);
        Assert.AreEqual(UserSettings.CreateDefault(), result.Value);
        Assert.HasCount(1, data.ListFiles(UserDataFileNames.RecoveryDirectory));
    }

    [TestMethod]
    public void DocumentWithoutRegisteredMigrationIsIsolated()
    {
        using var data = new TempUserData();
        data.Write(UserDataFileNames.Settings, """{ "schemaVersion": 1, "language": "old" }""");

        var result = data.CreateStore(new SchemaVersionSet(1, 2, 1)).LoadSettings();

        Assert.AreEqual(PersistenceStatus.Recovered, result.Status);
    }

    [TestMethod]
    public void MigrationOfAnotherDocumentDoesNotAffectConfiguration()
    {
        using var data = new TempUserData();
        data.Write(UserDataFileNames.Configuration, """{ "schemaVersion": 1, "applicationName": "Glaze" }""");

        var store = data.CreateStore(new SchemaVersionSet(1, 2, 1), [new RenameLanguageMigration(1)]);
        var result = store.LoadConfiguration();

        Assert.AreEqual(PersistenceStatus.Loaded, result.Status);
        Assert.AreEqual("Glaze", result.Value.ApplicationName);
        Assert.IsEmpty(data.ListFiles(UserDataFileNames.BackupsDirectory));
    }

    [TestMethod]
    public void MigrationWithoutBackupDoesNotRewriteTheDocument()
    {
        using var data = new TempUserData();
        const string original = """{ "schemaVersion": 1, "language": "old" }""";
        data.Write(UserDataFileNames.Settings, original);
        data.BlockDirectory(UserDataFileNames.BackupsDirectory);
        data.BlockDirectory(UserDataFileNames.RecoveryDirectory);

        var store = data.CreateStore(new SchemaVersionSet(1, 2, 1), [new RenameLanguageMigration(1)]);
        var result = store.LoadSettings();

        Assert.AreEqual(PersistenceStatus.Recovered, result.Status);
        Assert.AreEqual(UserSettings.CreateDefault(), result.Value);
        Assert.AreEqual(original, data.Read(UserDataFileNames.Settings));
    }

    [TestMethod]
    public void RecoveryKeepsTheOriginalDocumentWhenItCannotBeIsolated()
    {
        using var data = new TempUserData();
        const string original = "not json at all";
        data.Write(UserDataFileNames.Settings, original);
        data.BlockDirectory(UserDataFileNames.RecoveryDirectory);

        var result = data.CreateStore().LoadSettings();

        Assert.AreEqual(PersistenceStatus.Recovered, result.Status);
        Assert.AreEqual(UserSettings.CreateDefault(), result.Value);
        Assert.AreEqual(original, data.Read(UserDataFileNames.Settings));
    }

    [TestMethod]
    public void InvalidDataDirectoryNameIsRejected()
    {
        using var data = new TempUserData();
        data.Write(UserDataFileNames.Configuration, """{ "schemaVersion": 1, "applicationName": "Glaze", "dataDirectoryName": "..\\Windows" }""");

        var result = data.CreateStore().LoadConfiguration();

        Assert.AreEqual(PersistenceStatus.Recovered, result.Status);
        Assert.AreEqual("GlazeShell", result.Value.DataDirectoryName);
    }

    [TestMethod]
    public void ControlCharactersInNamesAreRejected()
    {
        using var data = new TempUserData();
        data.Write(
            UserDataFileNames.Layout,
            """{ "schemaVersion": 1, "layoutVersion": 1, "tabs": [ { "id": "tab-1", "name": "Home\r\nInjected", "order": 0, "categories": [] } ] }""");

        var result = data.CreateStore().LoadLayout();

        Assert.AreEqual(PersistenceStatus.Recovered, result.Status);
    }

    [TestMethod]
    public void IdentifiersWithControlCharactersAreRejected()
    {
        using var data = new TempUserData();
        data.Write(
            UserDataFileNames.Layout,
            """{ "schemaVersion": 1, "layoutVersion": 1, "activeTabId": "tab-1", "tabs": [ { "id": "tab-1\r\nX", "name": "Home", "order": 0, "categories": [] } ] }""");

        var result = data.CreateStore().LoadLayout();

        Assert.AreEqual(PersistenceStatus.Recovered, result.Status);
    }

    [TestMethod]
    public void WriteRejectsContentAboveTheLimit()
    {
        using var data = new TempUserData();
        var store = data.CreateStore();
        store.SaveLayout(TempUserData.CreateLayout(new DesktopTab("tab-1", "Home")));

        var oversized = new DesktopTab("tab-2", new string('n', 5 * 1024 * 1024));

        Assert.ThrowsExactly<PersistenceLimitExceededException>(
            () => store.SaveLayout(TempUserData.CreateLayout(oversized)));

        // The previously written document stays intact.
        Assert.AreEqual(PersistenceStatus.Loaded, data.CreateStore().LoadLayout().Status);
    }

    [TestMethod]
    public void WriteIsAtomicAndLeavesNoTemporaryFiles()
    {
        using var data = new TempUserData();
        var store = data.CreateStore();
        var layout = TempUserData.CreateLayout(new DesktopTab("tab-1", "Home"));

        store.SaveLayout(layout);
        store.SaveLayout(layout);

        var leftovers = Directory.GetFiles(data.Root, "*.tmp", SearchOption.AllDirectories);
        Assert.IsEmpty(leftovers);
        Assert.AreEqual(PersistenceStatus.Loaded, data.CreateStore().LoadLayout().Status);
    }

    [TestMethod]
    public void FlushIsSafeToCall()
    {
        using var data = new TempUserData();
        var store = data.CreateStore();

        store.Flush();

        Assert.AreEqual(data.Root, store.RootDirectory);
    }

    [TestMethod]
    public void StoreRejectsRelativeRootDirectory()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new JsonUserDataStore("relative\\GlazeShell"));
        Assert.ThrowsExactly<ArgumentException>(() => new JsonUserDataStore("  "));
    }

    [TestMethod]
    public void RecoveryIsReportedToTheErrorReporter()
    {
        using var data = new TempUserData();
        data.Write(UserDataFileNames.Layout, "not json at all");

        _ = data.CreateStore().LoadLayout();

        Assert.IsTrue(data.Reported.Any(static message => message.Contains("recovered", StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class RenameLanguageMigration(int fromVersion) : IDataMigration
    {
        public UserDataDocumentKind Document => UserDataDocumentKind.Settings;

        public int FromVersion { get; } = fromVersion;

        public void Apply(JsonObject document)
        {
            document["language"] = document["language"]?.GetValue<string>() ?? "system";
        }
    }

    private sealed class FailingMigration(int fromVersion) : IDataMigration
    {
        public UserDataDocumentKind Document => UserDataDocumentKind.Settings;

        public int FromVersion { get; } = fromVersion;

        public void Apply(JsonObject document) =>
            throw new InvalidOperationException("The migration cannot be applied.");
    }
}
