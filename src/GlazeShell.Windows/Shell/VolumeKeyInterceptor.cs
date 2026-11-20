using System.Runtime.InteropServices;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Shell;

/// <summary>Действие аппаратных клавиш громкости.</summary>
public enum VolumeKeyAction
{
    /// <summary>Убавить громкость.</summary>
    Down,

    /// <summary>Прибавить громкость.</summary>
    Up,

    /// <summary>Переключить режим «без звука».</summary>
    Mute,
}

/// <summary>
/// Перехват системных клавиш громкости (мультимедийные клавиши клавиатуры и ноутбука).
/// </summary>
/// <remarks>
/// <para>
/// Windows на каждое нажатие показывает собственный индикатор громкости. Пока Glaze Shell
/// рисует свой, системный индикатор не нужен и мешает: он появляется в левом верхнем углу
/// поверх стеклянных панелей. Хук <c>WH_KEYBOARD_LL</c> забирает клавиши громкости себе,
/// приложение применяет уровень через Core Audio и показывает собственную плашку.
/// </para>
/// <para>
/// Если обработчик не смог изменить громкость (например, нет устройства вывода), клавиша
/// возвращается системе вызовом <see cref="User32.CallNextHookEx"/> — иначе громкость
/// перестала бы работать вовсе.
/// </para>
/// <para>
/// Хук ставится на выделенном фоновом потоке с петлёй сообщений: без активной петли
/// система молча убирает хук по таймауту.
/// </para>
/// </remarks>
public sealed class VolumeKeyInterceptor : IDisposable
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(2);

    private readonly User32.LowLevelKeyboardProc _keyboardProcedure;

    private Thread? _thread;
    private nint _hook;
    private uint _threadId;
    private int _started;
    private int _disposed;

    // Читается из хук-процедуры чужого потока ввода, пишется из любого: без volatile нельзя.
    private volatile bool _enabled;

    public VolumeKeyInterceptor()
    {
        _keyboardProcedure = OnKeyboardEvent;
    }

    /// <summary>
    /// Запрос действия клавиши громкости. Обработчик возвращает <see langword="true"/>,
    /// если уровень применён: тогда клавиша поглощается и системный индикатор не появится.
    /// <see langword="false"/> возвращает клавишу системе.
    /// </summary>
    public event Func<VolumeKeyAction, bool>? VolumeKeyPressed;

    /// <summary>Включает или выключает перехват. Хук остаётся установленным.</summary>
    public void SetEnabled(bool enabled) => _enabled = enabled;

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
            Name = "GlazeShell.VolumeKey",
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
            System.Diagnostics.Trace.WriteLine($"GlazeShell.VolumeKey: the hook failed. {exception}");
            return User32.CallNextHookEx(_hook, code, wParam, lParam);
        }
    }

    private nint HandleKeyboardEvent(nint wParam, nint lParam)
    {
        var input = Marshal.PtrToStructure<User32.KeyboardInput>(lParam);

        // Своё синтетическое нажатие пропускается: громкость, изменённую приложением,
        // нельзя превращать в повторное нажатие клавиши.
        if (input.ExtraInfo == User32.SyntheticMarker)
        {
            return User32.CallNextHookEx(_hook, 0, wParam, lParam);
        }

        var isDown = (nint)wParam is User32.WmKeydown or User32.WmSyskeydown;
        if (!isDown)
        {
            return User32.CallNextHookEx(_hook, 0, wParam, lParam);
        }

        var action = input.VkCode switch
        {
            User32.VkVolumeUp => VolumeKeyAction.Up,
            User32.VkVolumeDown => VolumeKeyAction.Down,
            User32.VkVolumeMute => VolumeKeyAction.Mute,
            _ => (VolumeKeyAction?)null,
        };

        if (action is null)
        {
            return User32.CallNextHookEx(_hook, 0, wParam, lParam);
        }

        var handled = false;

        try
        {
            handled = VolumeKeyPressed?.Invoke(action.Value) == true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            System.Diagnostics.Trace.WriteLine($"GlazeShell.VolumeKey: the handler failed. {exception}");
        }

        // Не удалось применить уровень — клавиша уходит системе, громкость меняется сама.
        return handled ? User32.LlhcBlocked : User32.CallNextHookEx(_hook, 0, wParam, lParam);
    }
}
