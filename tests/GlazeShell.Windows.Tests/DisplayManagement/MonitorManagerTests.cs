using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using GlazeShell.Core.Events;
using GlazeShell.Core.Services;
using GlazeShell.Windows.DisplayManagement;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Tests.DisplayManagement;

[TestClass]
public sealed class MonitorManagerTests
{
    private const int EventTimeoutMilliseconds = 6000;

    private readonly ConcurrentQueue<Exception> _errors = new();

    private EventManager CreateEvents() => new(exception => _errors.Enqueue(exception));

    [TestCleanup]
    public void Cleanup()
    {
        if (_errors.TryDequeue(out var failure))
        {
            Assert.Fail($"An event dispatch failed: {failure.Message}");
        }
    }

    [TestMethod]
    public void EnumeratesAtLeastOneMonitor()
    {
        using var manager = new MonitorManager(CreateEvents());

        var monitors = manager.GetMonitors();

        Assert.IsGreaterThanOrEqualTo(1, monitors.Count, "A desktop environment must expose at least one monitor.");
    }

    [TestMethod]
    public void PrimaryMonitorIsInEnumeration()
    {
        using var manager = new MonitorManager(CreateEvents());

        var monitors = manager.GetMonitors();
        var primary = manager.GetPrimaryMonitor();

        Assert.IsNotNull(primary, "A desktop environment must expose a primary monitor.");
        Assert.IsTrue(monitors.Any(monitor => monitor.IsPrimary), "The primary monitor must be included in the enumeration.");
        Assert.IsNotNull(monitors.FirstOrDefault(monitor => monitor.Id == primary!.Id));
    }

    [TestMethod]
    public void EnumeratedMonitorsHaveValidGeometry()
    {
        using var manager = new MonitorManager(CreateEvents());

        foreach (var monitor in manager.GetMonitors())
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(monitor.Id));
            Assert.IsFalse(string.IsNullOrWhiteSpace(monitor.DeviceName));
            Assert.IsGreaterThan(0, monitor.Bounds.Width);
            Assert.IsGreaterThan(0, monitor.Bounds.Height);
            Assert.IsGreaterThan(0.0, monitor.ScaleFactor);
        }
    }

    [TestMethod]
    public async Task DisplayChangeBroadcastRaisesDisplayChanged()
    {
        var events = CreateEvents();
        using var manager = new MonitorManager(events);

        if (!manager.Start())
        {
            Assert.Inconclusive("The monitor event window could not be created in this environment.");
            return;
        }

        var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = events.Subscribe<DisplayChanged>(changed => received.TrySetResult(changed.Monitors.Count));
        await Task.Delay(200);

        _ = SendMessageTimeout(
            (nint)0xFFFF, // HWND_BROADCAST
            User32.WmDisplayChange,
            0,
            0,
            2, // SMTO_ABORTIFHUNG
            2000,
            out _);

        var done = await Task.WhenAny(received.Task, Task.Delay(EventTimeoutMilliseconds));
        Assert.AreEqual(received.Task, done, "The DisplayChanged event was not raised after WM_DISPLAYCHANGE broadcast.");
        Assert.IsTrue(received.Task.IsCompletedSuccessfully && received.Task.Result >= 1);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessageTimeout(
        nint window,
        uint message,
        nint wParam,
        nint lParam,
        uint flags,
        uint timeout,
        out nint result);
}