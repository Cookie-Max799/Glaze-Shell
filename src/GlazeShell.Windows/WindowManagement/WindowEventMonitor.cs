using System.Collections.Concurrent;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.WindowManagement;

internal sealed class WindowEventMonitor : IDisposable
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(5);

    private const uint ObjectIdWindow = 0;

    private readonly IEventManager _events;
    private readonly bool _skipOwnProcess;
    private readonly WinEventProc _callback;
    private readonly ConcurrentDictionary<nint, WindowInfo> _known = new();

    private Thread? _thread;
    private readonly List<nint> _hooks = [];
    private uint _threadId;
    private int _started;

    internal WindowEventMonitor(IEventManager events, bool skipOwnProcess = true)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _skipOwnProcess = skipOwnProcess;
        _callback = OnWinEvent;
    }

    internal bool Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException("The window event monitor has already been started.");
        }

        var ready = new ManualResetEventSlim(false);
        _thread = new Thread(() => ThreadMain(ready))
        {
            IsBackground = true,
            Name = "GlazeShell.WindowEvents",
        };
        _thread.Start();

        if (!ready.Wait(StartTimeout))
        {
            Stop();
            return false;
        }

        return _hooks.Count > 0;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            return;
        }

        Stop();
    }

    private void Stop()
    {
        UnhookAll();

        if (_thread is not null && _threadId != 0)
        {
            User32.PostThreadMessage(_threadId, User32.WmQuit, 0, 0);
            _thread.Join(TimeSpan.FromSeconds(2));
            _thread = null;
        }
    }

    private void ThreadMain(ManualResetEventSlim ready)
    {
        try
        {
            _threadId = Kernel32.GetCurrentThreadId();

            var flags = User32.WineventOutOfContext;
            if (_skipOwnProcess)
            {
                flags |= User32.WineventSkipOwnProcess;
            }

            InstallHook(User32.EventSystemForeground, User32.EventSystemMinimizeend, flags);
            InstallHook(User32.EventObjectCreate, User32.EventObjectCloaked, flags);

            ready.Set();

            if (_hooks.Count == 0)
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
            UnhookAll();
            _threadId = 0;
        }
    }

    private void InstallHook(uint eventMin, uint eventMax, uint flags)
    {
        var hook = User32.SetWinEventHook(eventMin, eventMax, 0, _callback, 0, 0, flags);

        if (hook != 0)
        {
            _hooks.Add(hook);
        }
    }

    private void UnhookAll()
    {
        foreach (var hook in _hooks)
        {
            User32.UnhookWinEvent(hook);
        }

        _hooks.Clear();
    }

    private void OnWinEvent(nint hook, uint winEvent, nint hwnd, uint objectId, uint childId, uint eventThread, uint eventTime)
    {
        if (hwnd == 0 || objectId != ObjectIdWindow)
        {
            return;
        }

        switch (winEvent)
        {
            case User32.EventSystemForeground:
                Publish(new ForegroundWindowChanged(NativeWindowEnumerator.Read(hwnd, includeProcess: false)));
                break;

            case User32.EventObjectCreate:
            case User32.EventObjectShow:
            case User32.EventObjectUncloaked:
                PublishAppearance(hwnd, opened: true);
                break;

            case User32.EventObjectDestroy:
            case User32.EventObjectHide:
            case User32.EventObjectCloaked:
                PublishAppearance(hwnd, opened: false);
                break;

            case User32.EventSystemMinimizestart:
                PublishState(hwnd, WindowState.Minimized);
                break;

            case User32.EventSystemMinimizeend:
                PublishState(hwnd, NativeWindowEnumerator.Read(hwnd, includeProcess: false) is { } restored
                    ? restored.State
                    : WindowState.Normal);
                break;
        }
    }

    private void PublishAppearance(nint hwnd, bool opened)
    {
        var window = NativeWindowEnumerator.Read(hwnd, includeProcess: false);

        if (opened)
        {
            if (window is not null)
            {
                _known[hwnd] = window;
                Publish(new WindowOpened(window));
            }

            return;
        }

        window ??= _known.GetValueOrDefault(hwnd);
        _known.TryRemove(hwnd, out _);

        if (window is not null)
        {
            Publish(new WindowClosed(window));
        }
    }

    private void PublishState(nint hwnd, WindowState state)
    {
        var window = NativeWindowEnumerator.Read(hwnd, includeProcess: false) ?? _known.GetValueOrDefault(hwnd);

        if (window is null)
        {
            return;
        }

        window = new WindowInfo(
            window.Id,
            window.Title,
            window.ProcessId,
            window.ProcessName,
            window.ExecutablePath,
            window.Type,
            state,
            window.IsForeground,
            window.OwnerId,
            window.CanResize);

        _known[hwnd] = window;
        Publish(new WindowStateChanged(window));
    }

    private void Publish(GlazeEvent glazeEvent) => _events.Publish(glazeEvent);
}