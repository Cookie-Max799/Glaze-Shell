using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.WindowManagement;

public sealed class WindowManager : IWindowManager, IDisposable
{
    private readonly IEventManager _events;
    private readonly bool _skipOwnProcess;
    private WindowEventMonitor? _monitor;

    public WindowManager(IEventManager events, bool skipOwnProcess = true)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _skipOwnProcess = skipOwnProcess;
    }

    public bool Start()
    {
        if (_monitor is not null)
        {
            return true;
        }

        var monitor = new WindowEventMonitor(_events, _skipOwnProcess);

        if (!monitor.Start())
        {
            monitor.Dispose();
            return false;
        }

        _monitor = monitor;
        return true;
    }

    public IReadOnlyList<WindowInfo> GetWindows() => NativeWindowEnumerator.Enumerate();

    public WindowInfo? GetWindow(string windowId) =>
        NativeWindowEnumerator.TryParseId(windowId, out var handle)
            ? NativeWindowEnumerator.Read(handle, includeProcess: true)
            : null;

    public WindowInfo? GetForegroundWindow()
    {
        var handle = User32.GetForegroundWindow();
        return handle == 0 ? null : NativeWindowEnumerator.Read(handle, includeProcess: true);
    }

    public bool FocusWindow(string windowId) =>
        NativeWindowEnumerator.TryParseId(windowId, out var handle) && Focus(handle);

    public bool MinimizeWindow(string windowId) => Show(windowId, User32.SwMinimize);

    public bool MaximizeWindow(string windowId) => Show(windowId, User32.SwMaximize);

    public bool RestoreWindow(string windowId) => Show(windowId, User32.SwRestore);

    public bool CloseWindow(string windowId) =>
        NativeWindowEnumerator.TryParseId(windowId, out var handle) &&
        User32.IsWindow(handle) &&
        User32.PostMessage(handle, User32.WmClose, 0, 0);

    public void Dispose()
    {
        _monitor?.Dispose();
        _monitor = null;
    }

    private static bool Show(string windowId, int command) =>
        NativeWindowEnumerator.TryParseId(windowId, out var handle) &&
        User32.IsWindow(handle) &&
        User32.ShowWindowAsync(handle, command);

    private static bool Focus(nint handle)
    {
        if (!User32.IsWindow(handle))
        {
            return false;
        }

        User32.ShowWindowAsync(handle, User32.SwRestore);
        User32.SetWindowPos(handle, 0, 0, 0, 0, 0, User32.SwpNoMove | User32.SwpNoSize | User32.SwpShowWindow);

        if (User32.SetForegroundWindow(handle))
        {
            return User32.GetForegroundWindow() == handle;
        }

        var foreground = User32.GetForegroundWindow();

        if (foreground == handle)
        {
            return true;
        }

        var foregroundThread = foreground != 0 ? User32.GetWindowThreadProcessId(foreground, out _) : 0;
        var targetThread = User32.GetWindowThreadProcessId(handle, out _);
        var currentThread = Kernel32.GetCurrentThreadId();

        var attachedForeground = false;
        var attachedTarget = false;

        if (foregroundThread != 0 && foregroundThread != currentThread)
        {
            attachedForeground = User32.AttachThreadInput(foregroundThread, currentThread, true);
        }

        if (targetThread != 0 && targetThread != currentThread)
        {
            attachedTarget = User32.AttachThreadInput(targetThread, currentThread, true);
        }

        try
        {
            User32.SetForegroundWindow(handle);
        }
        finally
        {
            if (attachedTarget)
            {
                User32.AttachThreadInput(targetThread, currentThread, false);
            }

            if (attachedForeground)
            {
                User32.AttachThreadInput(foregroundThread, currentThread, false);
            }
        }

        return User32.GetForegroundWindow() == handle;
    }
}