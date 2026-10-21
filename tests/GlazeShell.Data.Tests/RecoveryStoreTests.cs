using GlazeShell.Core.Recovery;
using GlazeShell.Data.Recovery;
using GlazeShell.Data.Serialization;

namespace GlazeShell.Data.Tests;

[TestClass]
public sealed class RecoveryStoreTests
{
    [TestMethod]
    public void MissingDocumentGivesEmptyState()
    {
        using var temp = new TempUserData();
        var store = new RecoveryStore(temp.Root);

        var snapshot = store.Load(out var diagnostics);

        Assert.IsTrue(snapshot.SessionCompleted);
        Assert.IsEmpty(snapshot.Crashes);
        Assert.IsEmpty(diagnostics);
    }

    [TestMethod]
    public void SavedStateIsReadBack()
    {
        using var temp = new TempUserData();
        var store = new RecoveryStore(temp.Root);
        var moment = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

        store.Save(new RecoverySnapshot(SessionCompleted: false, [moment]));

        var snapshot = store.Load(out var diagnostics);

        Assert.IsFalse(snapshot.SessionCompleted);
        Assert.HasCount(1, snapshot.Crashes);
        Assert.AreEqual(moment, snapshot.Crashes[0]);
        Assert.IsEmpty(diagnostics);
    }

    [TestMethod]
    public void CrashesAreSortedAndLimited()
    {
        using var temp = new TempUserData();
        var store = new RecoveryStore(temp.Root);
        var earliest = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

        var crashes = Enumerable
            .Range(0, RecoveryStore.MaxStoredCrashes + 5)
            .Select(index => earliest.AddMinutes(index))
            .Reverse()
            .ToArray();

        store.Save(new RecoverySnapshot(SessionCompleted: true, crashes));

        var snapshot = store.Load(out _);

        Assert.HasCount(RecoveryStore.MaxStoredCrashes, snapshot.Crashes);
        Assert.AreEqual(earliest.AddMinutes(5), snapshot.Crashes[0]);
    }

    [TestMethod]
    public void CorruptedDocumentIsIsolated()
    {
        using var temp = new TempUserData();
        var store = new RecoveryStore(temp.Root);

        temp.Write(RecoveryStore.FileName, "{ this is not json");

        var snapshot = store.Load(out var diagnostics);

        Assert.IsTrue(snapshot.SessionCompleted);
        Assert.IsNotEmpty(diagnostics);
        Assert.IsFalse(temp.FileExists(RecoveryStore.FileName), "The damaged document must not stay in place.");
        Assert.IsNotEmpty(temp.ListFiles(UserDataFileNames.RecoveryDirectory));
    }

    [TestMethod]
    public void UnsupportedSchemaIsIsolated()
    {
        using var temp = new TempUserData();
        var store = new RecoveryStore(temp.Root);

        temp.Write(
            RecoveryStore.FileName,
            "{\"schemaVersion\":99,\"sessionCompleted\":false,\"crashes\":[]}");

        var snapshot = store.Load(out var diagnostics);

        Assert.IsTrue(snapshot.SessionCompleted);
        Assert.IsNotEmpty(diagnostics);
        Assert.IsNotEmpty(temp.ListFiles(UserDataFileNames.RecoveryDirectory));
    }

    [TestMethod]
    public void CompletedSessionIsStoredAsCompleted()
    {
        using var temp = new TempUserData();
        var store = new RecoveryStore(temp.Root);

        store.Save(new RecoverySnapshot(SessionCompleted: true, []));
        var snapshot = store.Load(out _);

        Assert.IsTrue(snapshot.SessionCompleted);
    }
}
