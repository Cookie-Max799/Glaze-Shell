using GlazeShell.Core.Models;

namespace GlazeShell.Core.Interfaces;

public interface IMonitorManager
{
    IReadOnlyList<MonitorInfo> GetMonitors();

    MonitorInfo? GetMonitor(string monitorId);

    MonitorInfo? GetPrimaryMonitor();

    MonitorInfo? GetMonitorForWindow(string windowId);

    MonitorInfo? GetMonitorForPoint(int x, int y);
}