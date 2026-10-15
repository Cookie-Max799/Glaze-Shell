using GlazeShell.Infrastructure.Diagnostics;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class ProcessMetricsCollectorTests
{
    [TestMethod]
    public void CaptureReadsCurrentProcessMetrics()
    {
        var snapshot = ProcessMetricsCollector.Capture("unit-test");

        Assert.AreEqual("unit-test", snapshot.Label);
        Assert.IsGreaterThan(0, snapshot.WorkingSetBytes, "The working set of the running process must be positive.");
        Assert.IsGreaterThanOrEqualTo(0, snapshot.ManagedHeapBytes);
        Assert.IsGreaterThan(0, snapshot.ThreadCount, "A running process has at least one thread.");
        Assert.IsGreaterThanOrEqualTo(0, snapshot.HandleCount);
        Assert.IsGreaterThanOrEqualTo(TimeSpan.Zero, snapshot.Uptime);
    }

    [TestMethod]
    public void CaptureRejectsInvalidLabel()
    {
        Assert.ThrowsExactly<ArgumentException>(() => ProcessMetricsCollector.Capture(" "));
    }
}
