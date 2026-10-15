using GlazeShell.Infrastructure.Diagnostics;
using GlazeShell.Infrastructure.Logging;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class PerformanceMonitorTests
{
    [TestMethod]
    public void LogBaselineWritesBaselineEntry()
    {
        var logger = new RecordingLogger();
        using var monitor = new PerformanceMonitor(logger);

        monitor.LogBaseline();

        Assert.HasCount(1, logger.Messages);
        StringAssert.Contains(logger.Messages[0], "Baseline");
    }

    [TestMethod]
    public void LogSessionWritesEndAndSummaryAfterBaseline()
    {
        var logger = new RecordingLogger();
        using var monitor = new PerformanceMonitor(logger);

        monitor.LogBaseline();
        monitor.LogSession();

        Assert.HasCount(3, logger.Messages);
        StringAssert.Contains(logger.Messages[1], "End");
        StringAssert.Contains(logger.Messages[2], "Session summary");
    }

    [TestMethod]
    public void LogSessionWithoutBaselineWritesNothing()
    {
        var logger = new RecordingLogger();
        using var monitor = new PerformanceMonitor(logger);

        monitor.LogSession();

        Assert.IsEmpty(logger.Messages);
    }

    [TestMethod]
    public void StartSamplingRejectsNonPositiveInterval()
    {
        var logger = new RecordingLogger();
        using var monitor = new PerformanceMonitor(logger);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => monitor.StartSampling(TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => monitor.StartSampling(TimeSpan.FromMilliseconds(-1)));
    }

    [TestMethod]
    public void StartSamplingAfterDisposeThrows()
    {
        var logger = new RecordingLogger();
        var monitor = new PerformanceMonitor(logger);
        monitor.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => monitor.StartSampling(TimeSpan.FromMilliseconds(50)));
    }

    [TestMethod]
    public void DisposeIsIdempotent()
    {
        var logger = new RecordingLogger();
        var monitor = new PerformanceMonitor(logger);

        monitor.Dispose();
        monitor.Dispose();
    }

    [TestMethod]
    public void ConstructorRejectsNullLogger()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new PerformanceMonitor(null!));
    }

    private sealed class RecordingLogger : IGlazeLogger
    {
        private readonly List<string> _messages = [];

        public List<string> Messages => _messages;

        public void Write(GlazeLogLevel level, string component, string message, Exception? exception = null) =>
            _messages.Add($"{level}:{component}:{message}");
    }
}
