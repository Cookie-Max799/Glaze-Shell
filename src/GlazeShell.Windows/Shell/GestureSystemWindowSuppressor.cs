using GlazeShell.Infrastructure.Logging;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Shell;

/// <summary>
/// Упреждающее подавление системных окон, открываемых жестом тачпада (поиск,
/// меню «Пуск», Widgets): окно скрывается в момент показа, до первой отрисовки,
/// поэтому на экране не остаётся вспышки.
/// </summary>
/// <remarks>
/// Реактивная схема (закрытие по смене переднего плана) видит окно уже после
/// показа — кадр успевает отрисоваться. Хук <c>EVENT_OBJECT_SHOW</c> доставляется
/// синхронно в момент показа окна, и <c>ShowWindowAsync(SW_HIDE)</c> из колбэка
/// убирает окно до того, как оно станет видимым. Хук держится на выделенном
/// потоке с циклом сообщений (требование WinEvent) и живёт, пока оболочка
/// заменяет панель задач.
/// </remarks>
public sealed class GestureSystemWindowSuppressor : IDisposable
{
    private const uint ObjectIdWindow = 0;

    private readonly FileLogger _logger;
    private readonly WinEventProc _callback;
    private readonly List<nint> _hooks = [];
    private readonly object _hooksSync = new();

    private Thread? _thread;
    private volatile uint _threadId;
    private int _started;
    private bool _enabled;

    public GestureSystemWindowSuppressor(FileLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _callback = OnWinEvent;
    }

    /// <summary>Запускает поток с хуком. Идемпотентно: повторный вызов ничего не делает.</summary>
    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        var ready = new ManualResetEventSlim(false);
        _thread = new Thread(() => ThreadMain(ready))
        {
            IsBackground = true,
            Name = "GlazeShell.GestureSuppressor",
        };
        _thread.Start();

        if (!ready.Wait(TimeSpan.FromSeconds(5)))
        {
            _logger.Write(
                GlazeLogLevel.Warning,
                "GestureSuppressor",
                "The suppressor thread did not start in time; system windows may flash on the gesture.");
        }
    }

    /// <summary>Включает или выключает скрытие окон без пересоздания хука.</summary>
    public void SetEnabled(bool enabled) => _enabled = enabled;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            return;
        }

        if (_threadId != 0)
        {
            User32.PostThreadMessage(_threadId, User32.WmQuit, 0, 0);
        }

        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    private void ThreadMain(ManualResetEventSlim ready)
    {
        try
        {
            _threadId = Kernel32.GetCurrentThreadId();

            // Показ и создание: оба события нужны, потому что часть системных окон
            // создаётся заранее скрытыми и вспыхивает именно в EVENT_OBJECT_SHOW.
            var flags = User32.WineventOutOfContext | User32.WineventSkipOwnProcess;
            InstallHook(User32.EventObjectCreate, User32.EventObjectShow, flags);

            ready.Set();

            while (User32.GetMessage(out var message, 0, 0, 0) > 0)
            {
                User32.TranslateMessage(ref message);
                User32.DispatchMessage(ref message);
            }
        }
        finally
        {
            lock (_hooksSync)
            {
                foreach (var hook in _hooks)
                {
                    _ = User32.UnhookWinEvent(hook);
                }

                _hooks.Clear();
            }

            _threadId = 0;
        }
    }

    private void InstallHook(uint eventMin, uint eventMax, uint flags)
    {
        var hook = User32.SetWinEventHook(eventMin, eventMax, 0, _callback, 0, 0, flags);

        if (hook != 0)
        {
            lock (_hooksSync)
            {
                _hooks.Add(hook);
            }
        }
        else
        {
            _logger.Write(
                GlazeLogLevel.Warning,
                "GestureSuppressor",
                $"The WinEvent hook {eventMin}..{eventMax} was not installed.");
        }
    }

    private void OnWinEvent(nint hook, uint winEvent, nint hwnd, uint objectId, uint childId, uint eventThread, uint eventTime)
    {
        if (!_enabled || hwnd == 0 || objectId != ObjectIdWindow)
        {
            return;
        }

        if (User32.GetWindowThreadProcessId(hwnd, out var processId) == 0)
        {
            return;
        }

        if (!ShellForeground.IsGestureSystemUi(processId))
        {
            return;
        }

        // Скрытие прямо из колбэка: событие приходит до того, как окно успевает
        // отрисоваться, поэтому окно не появляется на экране ни на один кадр.
        _ = User32.ShowWindowAsync(hwnd, User32.SwHide);
        _ = User32.SetWindowPos(hwnd, 0, 0, 0, 0, 0,
            User32.SwpHideWindow | User32.SwpNoActivate | User32.SwpNoMove |
            User32.SwpNoSize | User32.SwpNoZOrder | User32.SwpAsyncWindowPos);
    }
}
