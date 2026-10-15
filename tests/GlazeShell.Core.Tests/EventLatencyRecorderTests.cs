using GlazeShell.Core.Diagnostics;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class EventLatencyRecorderTests
{
    [TestMethod]
    public void EmptyRecorderReportsNoDispatches()
    {
        var recorder = new EventLatencyRecorder();

        var stats = recorder.Snapshot();

        Assert.AreEqual(0L, stats.Count);
        Assert.AreEqual(TimeSpan.Zero, stats.Average);
        Assert.AreEqual("no events were dispatched", stats.ToLogString());
    }

    [TestMethod]
    public void RecorderTracksCountMinimumMaximumAndAverage()
    {
        var recorder = new EventLatencyRecorder();

        recorder.Record(TimeSpan.FromMilliseconds(10));
        recorder.Record(TimeSpan.FromMilliseconds(30));
        recorder.Record(TimeSpan.FromMilliseconds(20));

        var stats = recorder.Snapshot();

        Assert.AreEqual(3L, stats.Count);
        Assert.AreEqual(TimeSpan.FromMilliseconds(10), stats.Minimum);
        Assert.AreEqual(TimeSpan.FromMilliseconds(30), stats.Maximum);
        Assert.AreEqual(TimeSpan.FromMilliseconds(20), stats.Average);
    }

    [TestMethod]
    public void ResetClearsCollectedStatistics()
    {
        var recorder = new EventLatencyRecorder();
        recorder.Record(TimeSpan.FromMilliseconds(5));

        recorder.Reset();

        Assert.AreEqual(0L, recorder.Snapshot().Count);
    }

    [TestMethod]
    public void ConcurrentRecordsAreAllCounted()
    {
        var recorder = new EventLatencyRecorder();

        Parallel.For(0, 1000, i => recorder.Record(TimeSpan.FromTicks(i)));

        var stats = recorder.Snapshot();
        Assert.AreEqual(1000L, stats.Count);
        Assert.AreEqual(TimeSpan.Zero, stats.Minimum);
    }
}
