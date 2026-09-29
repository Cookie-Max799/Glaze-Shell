using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;

namespace GlazeShell.Windows.DisplayManagement;

public sealed class MonitorManager : IMonitorManager, IDisposable
{
    private readonly IEventManager _events;
    private MonitorEventMonitor? _monitor;

    public MonitorManager(IEventManager events)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public bool Start()
    {
        if (_monitor is not null)
        {
            return true;
        }

        var monitor = new MonitorEventMonitor(OnDisplayChanged);

        if (!monitor.Start())
        {
            monitor.Dispose();
            return false;
        }

        _monitor = monitor;
        return true;
    }

    public IReadOnlyList<MonitorInfo> GetMonitors() => NativeMonitorEnumerator.Enumerate();

    public MonitorInfo? GetMonitor(string monitorId)
    {
        if (!NativeMonitorEnumerator.TryParseId(monitorId, out var hmonitor))
        {
            return null;
        }

        return NativeMonitorEnumerator.Read(hmonitor);
    }

    public MonitorInfo? GetPrimaryMonitor() => NativeMonitorEnumerator.GetPrimaryMonitor();

    public MonitorInfo? GetMonitorForWindow(string windowId) => NativeMonitorEnumerator.GetMonitorForWindow(windowId);

    public MonitorInfo? GetMonitorForPoint(int x, int y) => NativeMonitorEnumerator.GetMonitorForPoint(x, y);

    public void Dispose()
    {
        _monitor?.Dispose();
        _monitor = null;
    }

    private void OnDisplayChanged() => _events.Publish(new DisplayChanged(NativeMonitorEnumerator.Enumerate()));
}