using GlazeShell.Core.Recovery;

namespace GlazeShell.Core.Tests.Recovery;

[TestClass]
public sealed class CrashTrackerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void EmptyHistoryNeverEntersSafeMode()
    {
        var tracker = new CrashTracker();

        Assert.IsFalse(tracker.ShouldEnterSafeMode(Now));
        Assert.AreEqual(0, tracker.CountRecent(Now));
    }

    [TestMethod]
    public void ThreeCrashesInsideTheWindowEnterSafeMode()
    {
        var tracker = new CrashTracker(
        [
            Now.AddMinutes(-4),
            Now.AddMinutes(-2),
            Now.AddMinutes(-1),
        ]);

        Assert.IsTrue(tracker.ShouldEnterSafeMode(Now));
        Assert.AreEqual(CrashTracker.CrashesForSafeMode, tracker.CountRecent(Now));
    }

    [TestMethod]
    public void TwoCrashesAreNotEnough()
    {
        var tracker = new CrashTracker([Now.AddMinutes(-4), Now.AddMinutes(-3)]);

        Assert.IsFalse(tracker.ShouldEnterSafeMode(Now));
        Assert.AreEqual(2, tracker.CountRecent(Now));
    }

    [TestMethod]
    public void CrashesOutsideTheWindowAreNotCounted()
    {
        var tracker = new CrashTracker(
        [
            Now.AddMinutes(-10),
            Now.AddMinutes(-9),
            Now.AddMinutes(-8),
            Now.AddMinutes(-1),
        ]);

        Assert.AreEqual(1, tracker.CountRecent(Now));
        Assert.IsFalse(tracker.ShouldEnterSafeMode(Now));
    }

    [TestMethod]
    public void PruneDropsCrashesOutsideTheWindow()
    {
        var tracker = new CrashTracker([Now.AddMinutes(-30), Now.AddMinutes(-2)]);

        var remaining = tracker.Prune(Now);

        Assert.HasCount(1, remaining);
        Assert.AreEqual(Now.AddMinutes(-2), remaining[0]);
    }

    [TestMethod]
    public void RecordCrashAddsTheMoment()
    {
        var tracker = new CrashTracker();

        tracker.RecordCrash(Now);

        Assert.HasCount(1, tracker.Crashes);
        Assert.AreEqual(1, tracker.CountRecent(Now));
    }

    [TestMethod]
    public void HistoryIsSortedFromEarlyToLate()
    {
        var tracker = new CrashTracker([Now, Now.AddMinutes(-5)]);

        Assert.AreEqual(Now.AddMinutes(-5), tracker.Crashes[0]);
        Assert.AreEqual(Now, tracker.Crashes[1]);
    }
}
