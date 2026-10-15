using GlazeShell.Core.Diagnostics;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class PerformanceSessionTests
{
    private const long OneMegabyte = 1024L * 1024L;

    [TestMethod]
    public void SummarizeComputesElapsedCpuAndMemoryDeltas()
    {
        var start = Snapshot(DateTimeOffset.UnixEpoch, workingSet: 100 * OneMegabyte, cpu: TimeSpan.Zero);
        var end = Snapshot(
            DateTimeOffset.UnixEpoch.AddSeconds(10),
            workingSet: 130 * OneMegabyte,
            managedHeap: 4 * OneMegabyte,
            threads: 24,
            handles: 512,
            cpu: TimeSpan.FromSeconds(5));

        var summary = PerformanceSession.Summarize(start, end, processorCount: 4);

        Assert.AreEqual(TimeSpan.FromSeconds(10), summary.Elapsed);

        // 5 процессорных секунд за 40 настенных секунд (10 с × 4 ядра) = 12.5%.
        Assert.AreEqual(12.5, summary.AverageCpuUtilizationPercent, 0.001);
        Assert.AreEqual(30 * OneMegabyte, summary.WorkingSetDeltaBytes);
        Assert.AreEqual(4 * OneMegabyte, summary.ManagedHeapDeltaBytes);
        Assert.AreEqual(24, summary.ThreadDelta);
        Assert.AreEqual(512, summary.HandleDelta);
    }

    [TestMethod]
    public void SummarizeReportsNegativeDeltasWhenResourcesAreReleased()
    {
        var start = Snapshot(DateTimeOffset.UnixEpoch, workingSet: 200 * OneMegabyte, threads: 30, handles: 900);
        var end = Snapshot(DateTimeOffset.UnixEpoch.AddSeconds(5), workingSet: 120 * OneMegabyte, threads: 22, handles: 700);

        var summary = PerformanceSession.Summarize(start, end, processorCount: 8);

        Assert.AreEqual(-80 * OneMegabyte, summary.WorkingSetDeltaBytes);
        Assert.AreEqual(-8, summary.ThreadDelta);
        Assert.AreEqual(-200, summary.HandleDelta);
    }

    [TestMethod]
    public void SummarizeTreatsZeroElapsedAsZeroCpu()
    {
        var timestamp = DateTimeOffset.UnixEpoch;
        var start = Snapshot(timestamp, cpu: TimeSpan.FromSeconds(1));
        var end = Snapshot(timestamp, cpu: TimeSpan.FromSeconds(9));

        var summary = PerformanceSession.Summarize(start, end, processorCount: 2);

        Assert.AreEqual(TimeSpan.Zero, summary.Elapsed);
        Assert.AreEqual(0, summary.AverageCpuUtilizationPercent, 0.001);
    }

    [TestMethod]
    public void SummarizeClampsCpuToHundredPercent()
    {
        var start = Snapshot(DateTimeOffset.UnixEpoch, cpu: TimeSpan.Zero);
        var end = Snapshot(DateTimeOffset.UnixEpoch.AddSeconds(1), cpu: TimeSpan.FromMinutes(5));

        var summary = PerformanceSession.Summarize(start, end, processorCount: 1);

        Assert.AreEqual(100, summary.AverageCpuUtilizationPercent, 0.001);
    }

    [TestMethod]
    public void SummarizeRejectsNonPositiveProcessorCount()
    {
        var start = Snapshot(DateTimeOffset.UnixEpoch);
        var end = Snapshot(DateTimeOffset.UnixEpoch.AddSeconds(1));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => PerformanceSession.Summarize(start, end, processorCount: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => PerformanceSession.Summarize(start, end, processorCount: -1));
    }

    [TestMethod]
    public void SummarizeRejectsNullSnapshots()
    {
        var snapshot = Snapshot(DateTimeOffset.UnixEpoch);

        Assert.ThrowsExactly<ArgumentNullException>(
            () => PerformanceSession.Summarize(null!, snapshot, processorCount: 1));
        Assert.ThrowsExactly<ArgumentNullException>(
            () => PerformanceSession.Summarize(snapshot, null!, processorCount: 1));
    }

    [TestMethod]
    public void SummaryLogStringContainsMeasuredValues()
    {
        var start = Snapshot(DateTimeOffset.UnixEpoch, workingSet: 100 * OneMegabyte);
        var end = Snapshot(DateTimeOffset.UnixEpoch.AddSeconds(2), workingSet: 110 * OneMegabyte, cpu: TimeSpan.FromSeconds(1));

        var text = PerformanceSession.Summarize(start, end, processorCount: 1).ToLogString();

        StringAssert.Contains(text, "session 2.0 s");
        StringAssert.Contains(text, "average cpu");
        StringAssert.Contains(text, "10.0 MB");
    }

    private static PerformanceSnapshot Snapshot(
        DateTimeOffset timestamp,
        long workingSet = 0,
        long managedHeap = 0,
        int threads = 0,
        int handles = 0,
        TimeSpan? cpu = null) =>
        new("test", timestamp, workingSet, 0, managedHeap, threads, handles, cpu ?? TimeSpan.Zero, TimeSpan.Zero);
}
