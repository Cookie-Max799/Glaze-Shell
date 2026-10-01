using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Windows.SystemEvents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlazeShell.Windows.Tests.SystemEvents;

[TestClass]
public sealed class ProcessEventMonitorTests
{
    private static readonly TimeSpan FastInterval = TimeSpan.FromMilliseconds(500);

    private readonly ConcurrentQueue<Exception> _errors = new();

    private void OnError(Exception exception) => _errors.Enqueue(exception);

    [TestMethod]
    public void StartSucceedsAndReportsNoErrors()
    {
        var events = new RecordingEventManager();
        var source = new FakeSnapshotSource();
        using var monitor = CreateMonitor(events, source, ProcessWatchOptions.Default);

        Assert.IsTrue(monitor.Start());
        Assert.IsEmpty(_errors);
    }

    [TestMethod]
    public void FirstSnapshotIsBaselineAndPublishesNothing()
    {
        var events = new RecordingEventManager();
        var source = new FakeSnapshotSource();
        source.Set(new ProcessSnapshotEntry(100, "existing", @"C:\apps\existing.exe"));

        using var monitor = CreateMonitor(events, source, ProcessWatchOptions.Default with { Interval = FastInterval });

        monitor.Start();
        WaitFor(() => source.CaptureCount >= 3, 5000);

        Assert.IsEmpty(events.Started);
        Assert.IsEmpty(events.Exited);
    }

    [TestMethod]
    public void PublishesStartedForNewProcess()
    {
        var events = new RecordingEventManager();
        var source = new FakeSnapshotSource();
        source.Set(new ProcessSnapshotEntry(100, "existing", @"C:\apps\existing.exe"));

        using var monitor = CreateMonitor(events, source, ProcessWatchOptions.Default with { Interval = FastInterval });
        monitor.Start();
        WaitFor(() => source.CaptureCount >= 1, 5000);

        source.Set(
            new ProcessSnapshotEntry(100, "existing", @"C:\apps\existing.exe"),
            new ProcessSnapshotEntry(200, "notepad", @"C:\Windows\System32\notepad.exe"));

        Assert.IsTrue(WaitFor(() => events.Started.Count == 1, 5000), "ProcessStarted was not published.");
        var started = events.Started.Single();
        Assert.AreEqual(200, started.ProcessId);
        Assert.AreEqual("notepad", started.ProcessName);
        Assert.AreEqual(@"C:\Windows\System32\notepad.exe", started.ExecutablePath);
        Assert.IsEmpty(events.Exited);
    }

    [TestMethod]
    public void PublishesExitedForMissingProcess()
    {
        var events = new RecordingEventManager();
        var source = new FakeSnapshotSource();
        source.Set(
            new ProcessSnapshotEntry(100, "existing", @"C:\apps\existing.exe"),
            new ProcessSnapshotEntry(200, "notepad", @"C:\Windows\System32\notepad.exe"));

        using var monitor = CreateMonitor(events, source, ProcessWatchOptions.Default with { Interval = FastInterval });
        monitor.Start();
        WaitFor(() => source.CaptureCount >= 1, 5000);

        source.Set(new ProcessSnapshotEntry(100, "existing", @"C:\apps\existing.exe"));

        Assert.IsTrue(WaitFor(() => events.Exited.Count == 1, 5000), "ProcessExited was not published.");
        var exited = events.Exited.Single();
        Assert.AreEqual(200, exited.ProcessId);
        Assert.AreEqual("notepad", exited.ProcessName);
        Assert.AreEqual(@"C:\Windows\System32\notepad.exe", exited.ExecutablePath);
        Assert.IsEmpty(events.Started);
    }

    [TestMethod]
    public void ExitIsPublishedBeforeStartOfRestartedProcess()
    {
        var events = new RecordingEventManager();
        var source = new FakeSnapshotSource();
        source.Set(new ProcessSnapshotEntry(100, "editor", @"C:\apps\editor.exe"));

        using var monitor = CreateMonitor(events, source, ProcessWatchOptions.Default with { Interval = FastInterval });
        monitor.Start();
        WaitFor(() => source.CaptureCount >= 1, 5000);

        // Тот же образ, другой PID: перезапуск должен читаться как «вышел, затем запустился».
        source.Set(new ProcessSnapshotEntry(101, "editor", @"C:\apps\editor.exe"));

        Assert.IsTrue(WaitFor(() => events.Exited.Count == 1 && events.Started.Count == 1, 5000));

        var order = events.Order.ToArray();
        Assert.IsTrue(order[0] is ProcessExited, "ProcessExited must be published before ProcessStarted.");
        Assert.IsTrue(order[1] is ProcessStarted, "ProcessStarted must be published after ProcessExited.");
    }

    [TestMethod]
    public void UnknownProcessIdIsPublishedWithoutPath()
    {
        var events = new RecordingEventManager();
        var source = new FakeSnapshotSource();
        using var monitor = CreateMonitor(events, source, ProcessWatchOptions.Default with { Interval = FastInterval });

        monitor.Start();
        WaitFor(() => source.CaptureCount >= 1, 5000);

        source.Set(new ProcessSnapshotEntry(300, "system", null));

        Assert.IsTrue(WaitFor(() => !events.Started.IsEmpty, 5000));
        var started = events.Started.Single();
        Assert.AreEqual("system", started.ProcessName);
        Assert.IsNull(started.ExecutablePath);
    }

    [TestMethod]
    public void SkipsSnapshotsWhenNobodySubscribes()
    {
        var events = new RecordingEventManager { Subscribers = false };
        var source = new FakeSnapshotSource();
        using var monitor = CreateMonitor(events, source, ProcessWatchOptions.Default with { Interval = FastInterval });

        monitor.Start();
        Thread.Sleep(1000);

        Assert.AreEqual(1, source.CaptureCount, "Only the baseline snapshot may be taken without subscribers.");
        Assert.IsEmpty(events.Started);
        Assert.IsEmpty(events.Exited);
    }

    [TestMethod]
    public void SnapshotFailureIsReportedAndDoesNotStopTheMonitor()
    {
        var events = new RecordingEventManager();
        var source = new FakeSnapshotSource { Failing = true };
        using var monitor = CreateMonitor(events, source, ProcessWatchOptions.Default with { Interval = FastInterval });

        monitor.Start();
        WaitFor(() => !_errors.IsEmpty, 5000);

        Assert.IsGreaterThan(0, _errors.Count, "A snapshot failure must be reported to the error handler.");

        // После сбоя источника монитор продолжает опрашивать систему.
        source.Failing = false;
        source.Set(new ProcessSnapshotEntry(400, "recovered", @"C:\apps\recovered.exe"));
        Assert.IsTrue(WaitFor(() => events.Started.Count == 1, 5000), "The monitor must recover after a snapshot failure.");
    }

    [TestMethod]
    public void StartThrowsWhenCalledTwice()
    {
        var events = new RecordingEventManager();
        using var monitor = CreateMonitor(events, new FakeSnapshotSource(), ProcessWatchOptions.Default);

        monitor.Start();

        Assert.Throws<InvalidOperationException>(() => monitor.Start());
    }

    [TestMethod]
    public void DisposeIsIdempotentAndStopsSnapshots()
    {
        var events = new RecordingEventManager();
        var source = new FakeSnapshotSource();
        var monitor = CreateMonitor(events, source, ProcessWatchOptions.Default with { Interval = FastInterval });

        monitor.Start();
        monitor.Dispose();
        monitor.Dispose();

        var after = source.CaptureCount;
        Thread.Sleep(1000);

        Assert.AreEqual(after, source.CaptureCount, "No snapshot may be taken after disposal.");
        Assert.IsEmpty(events.Started);
        Assert.IsEmpty(events.Exited);
    }

    private ProcessEventMonitor CreateMonitor(
        IEventManager events,
        IProcessSnapshotSource source,
        ProcessWatchOptions options) =>
        new(events, options, OnError, source);

    private static bool WaitFor(Func<bool> condition, int timeoutMilliseconds)
    {
        var deadline = Environment.TickCount64 + timeoutMilliseconds;

        while (Environment.TickCount64 < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(20);
        }

        return condition();
    }

    private sealed class FakeSnapshotSource : IProcessSnapshotSource
    {
        private readonly object _sync = new();
        private ProcessSnapshotEntry[] _entries = [];

        public int CaptureCount { get; private set; }

        public bool Failing { get; set; }

        public void Set(params ProcessSnapshotEntry[] entries)
        {
            lock (_sync)
            {
                _entries = entries;
            }
        }

        public IReadOnlyList<ProcessSnapshotEntry> Capture()
        {
            lock (_sync)
            {
                CaptureCount++;

                if (Failing)
                {
                    throw new InvalidOperationException("The snapshot is unavailable.");
                }

                return _entries;
            }
        }
    }

    private sealed class RecordingEventManager : IEventManager
    {
        public bool Subscribers { get; init; } = true;

        public ConcurrentQueue<ProcessStarted> Started { get; } = new();

        public ConcurrentQueue<ProcessExited> Exited { get; } = new();

        public ConcurrentQueue<GlazeEvent> Order { get; } = new();

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : GlazeEvent
        {
            return new NoopSubscription();
        }

        public void Publish<TEvent>(TEvent glazeEvent) where TEvent : GlazeEvent
        {
            Order.Enqueue(glazeEvent);

            switch (glazeEvent)
            {
                case ProcessStarted started:
                    Started.Enqueue(started);
                    break;
                case ProcessExited exited:
                    Exited.Enqueue(exited);
                    break;
                default:
                    break;
            }
        }

        public bool HasSubscribers<TEvent>() where TEvent : GlazeEvent
        {
            return Subscribers;
        }

        private sealed class NoopSubscription : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
