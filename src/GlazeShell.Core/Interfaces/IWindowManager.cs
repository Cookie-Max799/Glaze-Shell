using GlazeShell.Core.Models;

namespace GlazeShell.Core.Interfaces;

public interface IWindowManager
{
    IReadOnlyList<WindowInfo> GetWindows();

    WindowInfo? GetWindow(string windowId);

    WindowInfo? GetForegroundWindow();

    bool FocusWindow(string windowId);

    bool MinimizeWindow(string windowId);

    bool MaximizeWindow(string windowId);

    bool RestoreWindow(string windowId);

    bool CloseWindow(string windowId);
}
