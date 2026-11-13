using System.Runtime.InteropServices;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Shell;

/// <summary>
/// Низкоуровневый перехват клавиши Windows, Win+Tab и Win+S.
/// </summary>
/// <remarks>
/// <para>
/// Пока Glaze Shell заменяет панель задач, штатное меню «Пуск» не должно открываться.
/// Клавиша перехватывается глобальным хуком <c>WH_KEYBOARD_LL</c>, а событие
/// <see cref="Pressed"/> позволяет приложению показать свои панели вместо системного UI.
/// </para>
/// <para>
/// Перехватывается одиночное нажатие, Win+Tab и Win+S. Если пока клавиша Windows удерживается
/// нажата любая другая клавиша, перехват «отпускает» клавишу: в систему отправляется
/// синтетическое нажатие <c>Win</c>, поэтому сочетания <c>Win+R</c>, <c>Win+Space</c>,
/// <c>Win+Shift+S</c> и остальные работают как обычно. Своё синтетическое событие помечено
/// меткой <c>ExtraInfo</c> и пропускается хуком; остальной ввод, включая помеченный
/// <c>LLKHF_INJECTED</c>, обрабатывается — жест тачпада «три пальца» Windows превращает
/// в Win+Tab или Win+S, и он должен открывать панели Glaze Shell, а не системный UI.
/// </para>
/// <para>
/// Хук ставится на выделенном фоновом потоке с петлёй сообщений (как у
/// <see cref="GlobalHotkey"/>): без активной петли система молча убирает хук по
/// таймауту. Блокировка включается динамически через <see cref="SetEnabled"/>.
/// </para>
/// </remarks>
public sealed class WindowsKeyInterceptor : IDisposable
{
    private const string WindowClassNamePrefix = "GlazeShellWinKey";

    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(5);

    private readonly User32.LowLevelKeyboardProc _keyboardProcedure;
    private readonly string _windowClassName = WindowClassNamePrefix + Guid.NewGuid().ToString("N");

    private Thread? _thread;
    private nint _hook;
    private uint _threadId;
    private int _started;
    private int _disposed;

    // Включение блокировки без блокировки: читается из хук-процедуры чужого потока
    // ввода и пишется из любого потока, поэтому без volatile не обойтись.
    private volatile bool _enabled;

    // Состояние физического нажатия Windows. Пишется и читается только из хук-потока.
    private bool _winHeld;
    private bool _winForwarded;
    private bool _gestureSuppressed;
    private ushort _winVirtualKey;

    /// <param name="errorHandler">Куда сообщать об исключении в обработчике <see cref="Pressed"/>.</param>
    public WindowsKeyInterceptor(Action<Exception>? errorHandler = null)
    {
        _keyboardProcedure = OnKeyboardEvent;
    }

    /// <summary>Одиночное нажатие клавиши Windows; приходит с потока хука.</summary>
    public event EventHandler? Pressed;

    /// <summary>
    /// Жест «три пальца» по тачпаду: Windows превращает его в Win+S (поиск) или Win+Tab
    /// (представление задач). Событие приходит с потока хука вместо <see cref="Pressed"/>,
    /// чтобы приложение показало меню Glaze Shell, а не только панели.
    /// </summary>
    public event EventHandler? GesturePressed;

    /// <summary>
    /// Включает или выключает перехват. Пока выключено, клавиша проходит в систему
    /// как обычно; хук остаётся установленным, поэтому включение мгновенно.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;

        if (!enabled)
        {
            // Отпускание после выключения не должно считаться завершением перехваченного
            // нажатия: иначе следующее включение ждало бы «лишнего» нажатия Windows.
            _winHeld = false;
            _winForwarded = false;
            _gestureSuppressed = false;
        }
    }

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
            Name = "GlazeShell.WinKey",
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
            // Ошибка обработчика не должна ронять хук-процедуру: это заморозило бы
            // ввод всей системы.
            System.Diagnostics.Trace.WriteLine($"GlazeShell.WinKey: the hook failed. {exception}");
            return User32.CallNextHookEx(_hook, code, wParam, lParam);
        }
    }

    private nint HandleKeyboardEvent(nint wParam, nint lParam)
    {
        var input = Marshal.PtrToStructure<User32.KeyboardInput>(lParam);

        // Пропускаем только собственное синтетическое нажатие Win (метка ExtraInfo).
        // Остальной ввод, включая помеченный LLKHF_INJECTED, обрабатываем: жест тачпада
        // приходит как Win+Tab и тоже должен открывать панели Glaze Shell.
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

        var isWin = input.VkCode is User32.VkLeftWindows or User32.VkRightWindows;

        if (isWin && isDown)
        {
            if (_winHeld)
            {
                // Автоповтор удерживаемой клавиши: меню не открывается повторно.
                return User32.LlhcBlocked;
            }

            _winHeld = true;
            _winForwarded = false;
            _gestureSuppressed = false;
            _winVirtualKey = (ushort)input.VkCode;
            return User32.LlhcBlocked;
        }

        if (isWin && isUp)
        {
            if (!_winHeld)
            {
                // Клавиша была нажата до включения перехвата — не мешаем системе.
                return User32.CallNextHookEx(_hook, 0, wParam, lParam);
            }

            var forwarded = _winForwarded;
            var gesture = _gestureSuppressed;

            _winHeld = false;
            _winForwarded = false;
            _gestureSuppressed = false;

            if (forwarded)
            {
                return User32.CallNextHookEx(_hook, 0, wParam, lParam);
            }

            try
            {
                if (gesture)
                {
                    GesturePressed?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    Pressed?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                System.Diagnostics.Trace.WriteLine($"GlazeShell.WinKey: the press handler failed. {exception}");
            }

            // «Пуск» открывается на отпускание: без блокировки меню открылось бы.
            return User32.LlhcBlocked;
        }

        if (_winHeld && !_winForwarded && input.VkCode is User32.VkTab or User32.VkS)
        {
            // Win+Tab и Win+S: жест тачпада «три пальца» Windows превращает в одно из этих
            // сочетаний (по умолчанию — в поиск). Ни Task View, ни поиск Windows не открываем:
            // Win остаётся «не пересланной», поэтому на её отпускании придёт GesturePressed,
            // и приложение покажет меню Glaze Shell вместо системного интерфейса.
            _gestureSuppressed = true;
            return User32.LlhcBlocked;
        }

        if (_winHeld && isDown && !_winForwarded)
        {
            // Пока Win удерживается, нажата другая клавиша — это сочетание. Возвращаем
            // системе отменённое нажатие Win, чтобы сочетание сработало.
            User32.PressVirtualKey(_winVirtualKey);
            _winForwarded = true;
        }

        return User32.CallNextHookEx(_hook, 0, wParam, lParam);
    }
}
