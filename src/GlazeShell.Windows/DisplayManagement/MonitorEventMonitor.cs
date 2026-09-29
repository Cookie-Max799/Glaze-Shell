using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.DisplayManagement;

internal sealed class MonitorEventMonitor : IDisposable
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(5);

    private readonly Action _displayChanged;
    private readonly WindowProc _windowProcedure;
    private readonly string _className = "GlazeShellMonitorEvents" + Guid.NewGuid().ToString("N");

    private Thread? _thread;
    private nint _hwnd;
    private uint _threadId;
    private int _started;
    private int _disposed;

    internal MonitorEventMonitor(Action displayChanged)
    {
        _displayChanged = displayChanged ?? throw new ArgumentNullException(nameof(displayChanged));
        _windowProcedure = OnWindowMessage;
    }

    internal bool Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException("The monitor event monitor has already been started.");
        }

        var ready = new ManualResetEventSlim(false);
        _thread = new Thread(() => ThreadMain(ready))
        {
            IsBackground = true,
            Name = "GlazeShell.MonitorEvents",
        };
        _thread.Start();

        if (!ready.Wait(StartTimeout))
        {
            Stop();
            return false;
        }

        return _hwnd != 0;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Stop();
    }

    private void Stop()
    {
        if (_hwnd != 0)
        {
            User32.PostMessage(_hwnd, User32.WmQuit, 0, 0);
        }
        else if (_threadId != 0)
        {
            User32.PostThreadMessage(_threadId, User32.WmQuit, 0, 0);
        }

        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
        _hwnd = 0;
        _threadId = 0;
    }

    private void ThreadMain(ManualResetEventSlim ready)
    {
        try
        {
            _threadId = Kernel32.GetCurrentThreadId();

            if (!RegisterClass())
            {
                ready.Set();
                return;
            }

            _hwnd = User32.CreateWindowExW(
                0,
                _className,
                "GlazeShell monitor event window",
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                Kernel32.GetModuleHandle(null),
                0);

            ready.Set();

            if (_hwnd == 0)
            {
                return;
            }

            while (User32.GetMessage(out var message, 0, 0, 0) > 0)
            {
                User32.TranslateMessage(ref message);
                User32.DispatchMessage(ref message);
            }
        }
        finally
        {
            _hwnd = 0;
            _threadId = 0;
        }
    }

    private nint OnWindowMessage(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (message == User32.WmDisplayChange)
        {
            _displayChanged();
            return 0;
        }

        if (message == User32.WmQuit)
        {
            return 0;
        }

        return User32.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private bool RegisterClass()
    {
        var wndClass = new WndClass
        {
            Style = 0,
            Proc = _windowProcedure,
            ClassExtraBytes = 0,
            WindowExtraBytes = 0,
            Instance = Kernel32.GetModuleHandle(null),
            Icon = 0,
            Cursor = 0,
            Background = 0,
            MenuName = null,
            ClassName = _className,
        };

        return User32.RegisterClassW(ref wndClass) != 0;
    }
}