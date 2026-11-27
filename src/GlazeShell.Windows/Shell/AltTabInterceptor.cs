using System.Runtime.InteropServices;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Shell;

/// <summary>
/// Низкоуровневый перехват Alt+Tab и Alt+` для собственного переключателя приложений.
/// </summary>
/// <remarks>
/// <para>
/// Системный Alt+Tab зарезервирован Windows: без подавления на экране появились бы два
/// переключателя. Хук <c>WH_KEYBOARD_LL</c> забирает нажатия, пока открыт режим
/// переключения, и сообщает о них событиями. Сами события приходят с потока хука, поэтому
/// приложение обязано пересылать их на поток интерфейса и возвращаться немедленно: долгий
/// обработчик задерживает ввод всей системы.
/// </para>
/// <para>
/// Alt не блокируется: приложения и меню Windows ожидают его нажатие и отпускание, а
/// отпускание Alt — это и есть подтверждение выбора. Состояние Alt проверяется и хуком,
/// и опросом <c>GetAsyncKeyState</c>: хук мог включиться, когда клавиша уже была нажата.
/// </para>
/// <para>
/// Пока режим открыт, посторонние клавиши поглощаются: активное приложение не должно
/// получать ввод, пока пользователь выбирает приложение. Модификаторы пропускаются,
/// поэтому Shift продолжает работать для направления Tab и разворота свёрнутых окон.
/// </para>
/// </remarks>
public sealed class AltTabInterceptor : IDisposable
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(2);

    private readonly User32.LowLevelKeyboardProc _keyboardProcedure;

    private Thread? _thread;
    private nint _hook;
    private uint _threadId;
    private int _started;
    private int _disposed;

    // Читается из хук-процедуры чужого потока ввода, пишется из любого: нужно volatile.
    private volatile bool _enabled;

    // Состояние нажатий. Пишется и читается с потока хука, а сбрасывается ещё и с потока
    // интерфейса (после активации кликом), поэтому _switching объявлен volatile.
    private volatile bool _switching;
    private bool _altDown;

    public AltTabInterceptor()
    {
        _keyboardProcedure = OnKeyboardEvent;
    }

    /// <summary>Alt+Tab в состоянии покоя: открыть переключатель.</summary>
    /// <remarks>Аргумент — зажат ли Shift: назад выделяется последний элемент списка.</remarks>
    public event Action<bool>? SwitchStarted;

    /// <summary>Сдвиг выделения: +1 — вправо, −1 — влево.</summary>
    public event Action<int>? MoveRequested;

    public event Action<int>? MoveRowRequested;

    /// <summary>Esc: отменить переключение.</summary>
    public event Action? CancelRequested;

    /// <summary>Q: закрыть выделенное приложение.</summary>
    public event Action? CloseRequested;

    /// <summary>H: свернуть (скрыть) выделенное приложение, оставив его в списке.</summary>
    public event Action? HideRequested;

    /// <summary>Отпускание Alt: активировать выбранное приложение.</summary>
    /// <remarks>Аргумент — зажат ли Shift в момент отпускания.</remarks>
    public event Action<bool>? CommitRequested;

    /// <summary>Alt+`: переключить окна текущего приложения.</summary>
    public event Action? WindowCycleRequested;

    /// <summary>Включает или выключает перехват. Хук остаётся установленным.</summary>
    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;

        if (!enabled)
        {
            // Выключение посреди переключения не должно оставить режим «залипшим»:
            // следующее включение начнёт с чистого состояния.
            _switching = false;
            _altDown = false;
        }
    }

    /// <summary>
    /// Сбрасывает состояние режима, когда приложение уже активировано другим путём
    /// (кликом по значку). Следующее Tab при зажатом Alt снова откроет переключатель.
    /// </summary>
    public void ResetSwitching() => _switching = false;

    /// <summary>
    /// Запускает фоновый поток и устанавливает хук. Возвращает <see langword="false"/>,
    /// если хук установить не удалось (например, запрещён политикой).
    /// </summary>
    public bool Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return Volatile.Read(ref _hook) != 0;
        }

        var ready = new ManualResetEventSlim(false);

        _thread = new Thread(() => ThreadMain(ready))
        {
            IsBackground = true,
            Name = "GlazeShell.AltTab",
        };
        _thread.Start();

        return ready.Wait(StartTimeout) && Volatile.Read(ref _hook) != 0;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _enabled = false;

        if (_threadId != 0)
        {
            User32.PostThreadMessage(_threadId, User32.WmQuit, 0, 0);
        }

        _thread?.Join(TimeSpan.FromSeconds(1));
        _thread = null;
    }

    private void ThreadMain(ManualResetEventSlim ready)
    {
        try
        {
            _threadId = Kernel32.GetCurrentThreadId();

            _hook = User32.SetWindowsHookExW(
                User32.WhKeyboardLl,
                _keyboardProcedure,
                Kernel32.GetModuleHandle(null),
                0 /* глобальный хук: все потоки */);

            ready.Set();

            if (_hook == 0)
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
            if (_hook != 0)
            {
                _ = User32.UnhookWindowsHookEx(_hook);
                _hook = 0;
            }

            _threadId = 0;
        }
    }

    private nint OnKeyboardEvent(int code, nint wParam, nint lParam)
    {
        if (code < 0 || !_enabled || Volatile.Read(ref _disposed) != 0)
        {
            return User32.CallNextHookEx(_hook, code, wParam, lParam);
        }

        try
        {
            return HandleKeyboardEvent(wParam, lParam);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            // Ошибка обработчика не должна ронять хук-процедуру: это заморозило бы ввод.
            System.Diagnostics.Trace.WriteLine($"GlazeShell.AltTab: the hook failed. {exception}");
            return User32.CallNextHookEx(_hook, code, wParam, lParam);
        }
    }

    private nint HandleKeyboardEvent(nint wParam, nint lParam)
    {
        var input = Marshal.PtrToStructure<User32.KeyboardInput>(lParam);

        if (input.ExtraInfo == User32.SyntheticMarker)
        {
            return User32.CallNextHookEx(_hook, 0, wParam, lParam);
        }

        var isDown = (nint)wParam is User32.WmKeydown or User32.WmSyskeydown;
        var isUp = (nint)wParam is User32.WmKeyup or User32.WmSyskeyup;

        if (!isDown && !isUp)
        {
            return User32.CallNextHookEx(_hook, 0, wParam, lParam);
        }

        var key = unchecked((int)input.VkCode);

        if (key is User32.VkMenu or User32.VkLeftMenu or User32.VkRightMenu)
        {
            return HandleAltKey(isDown, wParam, lParam);
        }

        if (IsModifier(key))
        {
            return User32.CallNextHookEx(_hook, 0, wParam, lParam);
        }

        if (!isDown)
        {
            // Отпускание служебных клавиш режима не должно доходить до активного окна,
            // иначе приложение получит «хвост» нажатия, которое мы забрали.
            return _switching
                ? User32.LlhcBlocked
                : User32.CallNextHookEx(_hook, 0, wParam, lParam);
        }

        var altHeld = _altDown || IsKeyDown(User32.VkMenu);

        if (!_switching && altHeld && input.ScanCode == User32.ScanCodeBacktick)
        {
            Raise(WindowCycleRequested);
            return User32.LlhcBlocked;
        }

        if (key == User32.VkTab && altHeld)
        {
            _altDown = true;

            if (!_switching)
            {
                _switching = true;
                Raise(() => SwitchStarted?.Invoke(IsKeyDown(User32.VkShift)));
            }
            else
            {
                var delta = IsKeyDown(User32.VkShift) ? -1 : 1;
                Raise(() => MoveRequested?.Invoke(delta));
            }

            return User32.LlhcBlocked;
        }

        if (!_switching)
        {
            return User32.CallNextHookEx(_hook, 0, wParam, lParam);
        }

        switch (key)
        {
            case User32.VkLeft:
                Raise(() => MoveRequested?.Invoke(-1));
                break;

            case User32.VkRight:
                Raise(() => MoveRequested?.Invoke(1));
                break;

            case User32.VkUp:
                Raise(() => MoveRowRequested?.Invoke(-1));
                break;

            case User32.VkDown:
                Raise(() => MoveRowRequested?.Invoke(1));
                break;

            case User32.VkQ:
                Raise(CloseRequested);
                break;

            case User32.VkH:
                Raise(HideRequested);
                break;

            case User32.VkEscape:
                Raise(CancelRequested);
                _switching = false;
                break;

            default:
                // Пока панель открыта, активное приложение не должно получать ввод.
                break;
        }

        return User32.LlhcBlocked;
    }

    private nint HandleAltKey(bool isDown, nint wParam, nint lParam)
    {
        if (isDown)
        {
            _altDown = true;
            return User32.CallNextHookEx(_hook, 0, wParam, lParam);
        }

        _altDown = false;

        if (!_switching)
        {
            return User32.CallNextHookEx(_hook, 0, wParam, lParam);
        }

        _switching = false;

        var shift = IsKeyDown(User32.VkShift);
        Raise(() => CommitRequested?.Invoke(shift));

        // Alt отпущен — система должна увидеть отпускание, иначе модификатор «залипнет».
        return User32.CallNextHookEx(_hook, 0, wParam, lParam);
    }

    private static bool IsModifier(int key) =>
        key is User32.VkShift or 0x11 /* Ctrl */ or User32.VkMenu or User32.VkLeftMenu or User32.VkRightMenu;

    private static bool IsKeyDown(int key) => (User32.GetAsyncKeyState(key) & 0x8000) != 0;

    private static void Raise(Action? action)
    {
        if (action is null)
        {
            return;
        }

        try
        {
            action();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            System.Diagnostics.Trace.WriteLine($"GlazeShell.AltTab: the handler failed. {exception}");
        }
    }
}
