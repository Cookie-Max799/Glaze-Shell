using System.Runtime.InteropServices;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Shell;

/// <summary>
/// Системная (глобальная) горячая клавиша Glaze Shell, например <c>Ctrl K</c> для поиска.
/// </summary>
/// <remarks>
/// <para>
/// Комбинация регистрируется через <c>RegisterHotKey</c>, поэтому нажатие доходит до
/// приложения, даже когда оно не в фокусе. Чтобы получить <c>WM_HOTKEY</c>, не подменяя
/// оконную процедуру окон WinUI, на отдельном фоновом потоке создаётся невидимое окно с
/// собственной петлёй сообщений — тем же приёмом, что и у <see cref="TaskbarHider"/>.
/// </para>
/// <para>
/// Комбинацию может уже занимать другая программа: тогда <see cref="Register"/> вернёт
/// <see langword="false"/>, и приложение продолжит работать без горячей клавиши.
/// </para>
/// <para>
/// Событие <see cref="Pressed"/> приходит с потока слушателя, поэтому подписчик должен сам
/// переключиться на поток интерфейса.
/// </para>
/// </remarks>
public sealed class GlobalHotkey : IDisposable
{
    /// <summary>Модификаторы системной горячей клавиши.</summary>
    [Flags]
    public enum Modifier : uint
    {
        None = 0,
        Alt = User32.ModAlt,
        Control = User32.ModControl,
        Shift = User32.ModShift,
        Windows = User32.ModWin,
    }

    private const string WindowClassNamePrefix = "GlazeShellHotkey";

    /// <summary>Идентификатор комбинации в пределах окна (0x474C — «GL»).</summary>
    private const int HotkeyId = 0x474C;

    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(5);

    private readonly Modifier _modifiers;
    private readonly uint _virtualKey;
    private readonly Action<Exception>? _errorHandler;
    private readonly WindowProc _windowProcedure;
    private readonly string _windowClassName = WindowClassNamePrefix + Guid.NewGuid().ToString("N");

    private Thread? _thread;
    private nint _window;
    private uint _threadId;
    private bool _registered;
    private int _started;
    private int _disposed;

    /// <param name="modifiers">Набор модификаторов.</param>
    /// <param name="virtualKey">Виртуальный код клавиши, например <c>(uint)'K'</c>.</param>
    /// <param name="errorHandler">Куда сообщать об исключении в обработчике <see cref="Pressed"/>.</param>
    public GlobalHotkey(Modifier modifiers, uint virtualKey, Action<Exception>? errorHandler = null)
    {
        _modifiers = modifiers;
        _virtualKey = virtualKey;
        _errorHandler = errorHandler;
        _windowProcedure = OnWindowMessage;
    }

    /// <summary>Нажатие зарегистрированной комбинации; приходит с потока слушателя.</summary>
    public event EventHandler? Pressed;

    /// <summary>
    /// Создаёт горячую клавишу и сразу регистрирует её. Возвращает <see langword="null"/>,
    /// если комбинацию занял другой процесс или окно не удалось создать.
    /// </summary>
    public static GlobalHotkey? TryRegister(
        Modifier modifiers,
        uint virtualKey,
        Action<Exception>? errorHandler = null)
    {
        var hotkey = new GlobalHotkey(modifiers, virtualKey, errorHandler);

        if (hotkey.Register())
        {
            return hotkey;
        }

        hotkey.Dispose();
        return null;
    }

    /// <summary>
    /// Запускает слушатель и регистрирует комбинацию. Возвращает <see langword="false"/>,
    /// если комбинация занята другим процессом или окно не удалось создать.
    /// </summary>
    public bool Register()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return Volatile.Read(ref _registered);
        }

        var ready = new ManualResetEventSlim(false);

        _thread = new Thread(() => ThreadMain(ready))
        {
            IsBackground = true,
            Name = "GlazeShell.Hotkey",
        };
        _thread.Start();

        if (!ready.Wait(StartTimeout))
        {
            Stop();
            return false;
        }

        return Volatile.Read(ref _registered);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Stop();
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

            _window = User32.CreateWindowExW(
                (uint)User32.WsExToolWindow,
                _windowClassName,
                "GlazeShell hotkey window",
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                Kernel32.GetModuleHandle(null),
                0);

            if (_window != 0)
            {
                _registered = User32.RegisterHotKey(_window, HotkeyId, (uint)_modifiers, _virtualKey);
            }

            ready.Set();

            if (_window == 0)
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
            _registered = false;
            _window = 0;
            _threadId = 0;
        }
    }

    private nint OnWindowMessage(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (message == User32.WmQuit)
        {
            return 0;
        }

        if (message == User32.WmHotkey && wParam == HotkeyId && Volatile.Read(ref _disposed) == 0)
        {
            try
            {
                Pressed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                // Исключение не должно проходить сквозь нативную оконную процедуру: это
                // завершило бы процесс. Ошибку отдаём обработчику вместо падения.
                _errorHandler?.Invoke(exception);
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
            ClassName = _windowClassName,
        };

        return User32.RegisterClassW(ref wndClass) != 0;
    }

    private void Stop()
    {
        if (_window != 0)
        {
            User32.UnregisterHotKey(_window, HotkeyId);
            User32.PostMessage(_window, User32.WmQuit, 0, 0);
        }
        else if (_threadId != 0)
        {
            User32.PostThreadMessage(_threadId, User32.WmQuit, 0, 0);
        }

        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
        _window = 0;
        _threadId = 0;
    }
}
