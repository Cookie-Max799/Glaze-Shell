using System.Runtime.InteropServices;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Shell;

/// <summary>
/// Скрывает системную панель задач (<c>Shell_TrayWnd</c>), пока dock Glaze Shell заменяет её.
/// </summary>
/// <remarks>
/// <para>
/// Панель задач не уничтожается и не отключается в реестре: окно <c>Shell_TrayWnd</c>
/// сдвигается за нижнюю границу своего монитора на несколько пикселей. Это обратимо,
/// не требует прав администратора и не меняет системные настройки. При выходе приложения
/// панель возвращается на исходное место.
/// </para>
/// <para>
/// После перезапуска Explorer панель создаётся заново в штатном положении, поэтому
/// скрытие повторяется по широковещательному сообщению <c>TaskbarCreated</c>. Смена
/// разрешения или раскладки мониторов также возвращает панель, и она скрывается снова.
/// </para>
/// <para>
/// Падение приложения может оставить панель сдвинутой: <see cref="RestoreIfOffScreen"/>
/// вызывается при следующем запуске и возвращает её на место даже без сохранённого
/// состояния. Дополнительно панель можно вернуть перезапуском Explorer.
/// </para>
/// </remarks>
public sealed class TaskbarHider : IDisposable
{
    private const string TaskbarClassName = "Shell_TrayWnd";
    private const string TaskbarCreatedMessageName = "TaskbarCreated";
    private const string EventWindowClassNamePrefix = "GlazeShellTaskbarEvents";

    /// <summary>Отступ скрытой панели за краем монитора, чтобы её нельзя было задеть курсором.</summary>
    private const int OffScreenGap = 16;

    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(5);

    private readonly object _sync = new();
    private readonly WindowProc _windowProcedure;
    private readonly WinEventProc _visibilityProcedure;
    private readonly Dictionary<nint, Rect> _taskbars = [];
    private nint _visibilityHook;
    private readonly string _eventWindowClassName = EventWindowClassNamePrefix + Guid.NewGuid().ToString("N");

    private Thread? _thread;
    private nint _eventWindow;
    private uint _threadId;
    private uint _taskbarCreatedMessage;

    private bool _hidden;

    private int _started;
    private int _disposed;

    public TaskbarHider()
    {
        _windowProcedure = OnWindowMessage;
        _visibilityProcedure = OnVisibilityChanged;
    }

    /// <summary>
    /// Запускает прослушивание пересоздания панели задач. Без него скрытие всё равно
    /// работает, но после перезапуска Explorer панель останется видимой до вызова
    /// <see cref="Hide"/>.
    /// </summary>
    public bool Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return true;
        }

        var ready = new ManualResetEventSlim(false);

        _thread = new Thread(() => ThreadMain(ready))
        {
            IsBackground = true,
            Name = "GlazeShell.TaskbarEvents",
        };
        _thread.Start();

        if (!ready.Wait(StartTimeout))
        {
            Stop();
            return false;
        }

        return _eventWindow != 0;
    }

    /// <summary>
    /// Скрывает панель задач за краем её монитора. Исходное положение запоминается для
    /// <see cref="Restore"/>; повторный вызов для уже скрытой панели ничего не меняет.
    /// </summary>
    public bool Hide()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        if (Volatile.Read(ref _started) == 0)
        {
            Start();
        }

        return HideTaskbar();
    }

    /// <summary>
    /// Возвращает панель задач на место, если она была скрыта этим экземпляром.
    /// </summary>
    public bool Restore()
    {
        lock (_sync)
        {
            _hidden = false;
            var restored = true;
            foreach (var (taskbar, bounds) in _taskbars)
            {
                if (User32.IsWindow(taskbar))
                {
                    restored &= RestoreTaskbar(taskbar, bounds);
                }
            }

            _taskbars.Clear();
            return restored;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Restore();
        Stop();
    }

    /// <summary>
    /// Возвращает панель задач на место, если она оказалась за нижней границей своего
    /// монитора. Нужна после падения предыдущего экземпляра приложения: сохранённого
    /// положения в этом случае нет, поэтому панель ставится к нижнему краю монитора.
    /// </summary>
    public static bool RestoreIfOffScreen()
    {
        var restored = false;
        User32.EnumWindows((taskbar, _) =>
        {
            if (!IsTaskbar(taskbar) || !User32.GetWindowRect(taskbar, out var bounds) ||
                !TryGetMonitorBounds(taskbar, out var monitor))
            {
                return true;
            }

            var below = IsBelowMonitor(bounds, monitor);
            if (below || !User32.IsWindowVisible(taskbar))
            {
                restored |= User32.SetWindowPos(taskbar, 0, bounds.Left,
                    below ? monitor.Bottom - (bounds.Bottom - bounds.Top) : bounds.Top,
                    0, 0, User32.SwpNoSize | User32.SwpNoZOrder | User32.SwpNoActivate | User32.SwpShowWindow);
            }

            return true;
        }, 0);
        return restored;
    }

    /// <summary>
    /// Проверяет, что окно целиком находится ниже монитора. Внутренний и статический,
    /// чтобы правило можно было проверить тестом без реальной панели задач.
    /// </summary>
    internal static bool IsBelowMonitor(Rect bounds, Rect monitor) => bounds.Top >= monitor.Bottom;

    private bool HideTaskbar()
    {
        lock (_sync)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return false;
            }

            _hidden = true;
            var found = false;
            var success = true;
            User32.EnumWindows((taskbar, _) =>
            {
                if (!IsTaskbar(taskbar))
                {
                    return true;
                }

                found = true;
                if (!User32.GetWindowRect(taskbar, out var bounds) || !TryGetMonitorBounds(taskbar, out var monitor))
                {
                    success = false;
                    return true;
                }

                _taskbars.TryAdd(taskbar, bounds);
                success &= User32.SetWindowPos(taskbar, 0, bounds.Left, monitor.Bottom + OffScreenGap,
                    0, 0, User32.SwpNoSize | User32.SwpNoZOrder | User32.SwpNoActivate | User32.SwpHideWindow);
                return true;
            }, 0);
            return found && success;
        }
    }

    private static bool IsTaskbar(nint hwnd) =>
        User32.GetWindowClassName(hwnd) is TaskbarClassName or "Shell_SecondaryTrayWnd";

    private void OnVisibilityChanged(nint hook, uint winEvent, nint hwnd, uint objectId, uint childId, uint eventThread, uint eventTime)
    {
        if (objectId == 0 && childId == 0 && hwnd != 0 && IsTaskbar(hwnd) && Volatile.Read(ref _disposed) == 0)
        {
            lock (_sync)
            {
                if (_hidden)
                {
                    _ = HideTaskbar();
                }
            }
        }
    }

    private static bool RestoreTaskbar(nint taskbar, Rect bounds)
    {
        if (taskbar == 0 || !User32.IsWindow(taskbar))
        {
            return false;
        }

        return User32.SetWindowPos(
            taskbar,
            0,
            bounds.Left,
            bounds.Top,
            bounds.Right - bounds.Left,
            bounds.Bottom - bounds.Top,
            User32.SwpNoZOrder | User32.SwpNoActivate | User32.SwpShowWindow);
    }

    private static bool TryGetMonitorBounds(nint window, out Rect monitor)
    {
        monitor = default;

        var handle = User32.MonitorFromWindow(window, User32.MonitorDefaultToNearest);

        if (handle == 0)
        {
            return false;
        }

        var info = new MonitorInfoEx { Size = (uint)Marshal.SizeOf<MonitorInfoEx>() };

        if (!User32.GetMonitorInfoW(handle, ref info))
        {
            return false;
        }

        monitor = info.Monitor;
        return true;
    }

    private void ThreadMain(ManualResetEventSlim ready)
    {
        try
        {
            _threadId = Kernel32.GetCurrentThreadId();
            _visibilityHook = User32.SetWinEventHook(User32.EventObjectShow, User32.EventObjectShow, 0,
                _visibilityProcedure, 0, 0, User32.WineventOutOfContext | User32.WineventSkipOwnProcess);
            _taskbarCreatedMessage = User32.RegisterWindowMessage(TaskbarCreatedMessageName);

            if (!RegisterClass())
            {
                ready.Set();
                return;
            }

            _eventWindow = User32.CreateWindowExW(
                (uint)User32.WsExToolWindow,
                _eventWindowClassName,
                "GlazeShell taskbar event window",
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

            if (_eventWindow == 0)
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
            if (_visibilityHook != 0)
            {
                _ = User32.UnhookWinEvent(_visibilityHook);
                _visibilityHook = 0;
            }

            if (_eventWindow != 0)
            {
                _ = User32.DestroyWindow(_eventWindow);
            }
            _eventWindow = 0;
            _threadId = 0;
        }
    }

    private nint OnWindowMessage(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (message == User32.WmQuit)
        {
            return 0;
        }

        if ((_taskbarCreatedMessage != 0 && message == _taskbarCreatedMessage) ||
            message == User32.WmDisplayChange)
        {
            bool hidden;

            lock (_sync)
            {
                hidden = _hidden;
            }

            // Перезапуск Explorer создаёт новую панель, а смена разрешения возвращает
            // старую на штатное место: в обоих случаях скрытие повторяется.
            if (hidden && Volatile.Read(ref _disposed) == 0)
            {
                _ = HideTaskbar();
            }

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
            ClassName = _eventWindowClassName,
        };

        return User32.RegisterClassW(ref wndClass) != 0;
    }

    private void Stop()
    {
        if (_eventWindow != 0)
        {
            User32.PostMessage(_eventWindow, User32.WmQuit, 0, 0);
        }
        else if (_threadId != 0)
        {
            User32.PostThreadMessage(_threadId, User32.WmQuit, 0, 0);
        }

        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
        _eventWindow = 0;
        _threadId = 0;
    }
}
