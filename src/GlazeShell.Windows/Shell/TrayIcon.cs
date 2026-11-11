using System.Runtime.InteropServices;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Shell;

/// <summary>
/// Значок Glaze Shell в области уведомлений (трей) с контекстным меню
/// «Открыть / Настройки / Выход».
/// </summary>
/// <remarks>
/// <para>
/// WinUI не даёт готового компонента трея, поэтому значок ставится напрямую через
/// <c>Shell_NotifyIcon</c> на собственное невидимое окно. Окно создаётся на отдельном
/// фоновом потоке с собственной петлёй сообщений — тем же приёмом, что у
/// <see cref="GlobalHotkey"/> и <see cref="TaskbarHider"/>: подменять оконную процедуру
/// окон WinUI не нужно.
/// </para>
/// <para>
/// Окно остаётся обычным top-level окном (не <c>HWND_MESSAGE</c>): только такие окна
/// получают широковещательное сообщение повторного запуска, по которому приложение
/// показывает главное окно, даже если оно свёрнуто в трей.
/// </para>
/// <para>
/// События приходят с потока значка, поэтому подписчик должен сам переключиться на поток
/// интерфейса. Исключение из обработчика не должно проходить сквозь нативную процедуру,
/// иначе оно завершит процесс: ошибка передаётся в <c>errorHandler</c>.
/// </para>
/// </remarks>
public sealed class TrayIcon : IDisposable
{
    /// <summary>
    /// Имя широковещательного сообщения повторного запуска. Второй экземпляр не находит
    /// видимое окно, если главное свёрнуто в трей, и просит работающий экземпляр показать его.
    /// </summary>
    internal const string ActivationMessageName = "GlazeShell.ShowMainWindow";

    private const string WindowClassNamePrefix = "GlazeShellTray";

    /// <summary>Идентификатор значка в пределах окна.</summary>
    private const uint TrayIconId = 0x474C;

    /// <summary>Сообщение обратного вызова значка: <c>WM_APP + 1</c>.</summary>
    private const uint CallbackMessage = User32.WmApp + 1;

    private const int OpenCommand = 1;
    private const int SettingsCommand = 2;
    private const int ExitCommand = 3;

    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(5);

    private readonly WindowProc _windowProcedure;
    private readonly Action<Exception>? _errorHandler;
    private readonly string _windowClassName = WindowClassNamePrefix + Guid.NewGuid().ToString("N");

    private Thread? _thread;
    private nint _window;
    private nint _icon;
    private uint _threadId;
    private uint _activationMessage;
    private uint _taskbarCreatedMessage;
    private bool _registered;
    private bool _ownsIcon;
    private int _started;
    private int _disposed;

    private TrayIcon(Action<Exception>? errorHandler)
    {
        _errorHandler = errorHandler;
        _windowProcedure = OnWindowMessage;
    }

    /// <summary>Просьба показать главное окно: двойной клик или пункт меню «Открыть».</summary>
    public event EventHandler? OpenRequested;

    /// <summary>Просьба открыть настройки.</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>Просьба завершить приложение (пункт меню «Выход»).</summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// Создаёт значок трея. Возвращает <see langword="null"/>, если систему уведомлений
    /// или окно создать не удалось: приложение продолжит работать, но закрытие в трей
    /// будет недоступно.
    /// </summary>
    public static TrayIcon? TryCreate(Action<Exception>? errorHandler = null)
    {
        var tray = new TrayIcon(errorHandler);

        if (tray.Register())
        {
            return tray;
        }

        tray.Dispose();
        return null;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Stop();
    }

    /// <summary>
    /// Запускает поток значка и ставит иконку. Возвращает <see langword="false"/>, если
    /// окно или иконка не создались.
    /// </summary>
    private bool Register()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return _window != 0;
        }

        var ready = new ManualResetEventSlim(false);

        _thread = new Thread(() => ThreadMain(ready))
        {
            IsBackground = true,
            Name = "GlazeShell.Tray",
        };
        _thread.Start();

        if (!ready.Wait(StartTimeout))
        {
            Stop();
            return false;
        }

        return _registered;
    }

    private void ThreadMain(ManualResetEventSlim ready)
    {
        var added = false;

        try
        {
            _threadId = Kernel32.GetCurrentThreadId();
            _activationMessage = User32.RegisterWindowMessage(ActivationMessageName);
            _taskbarCreatedMessage = User32.RegisterWindowMessage("TaskbarCreated");

            if (!RegisterClass())
            {
                ready.Set();
                return;
            }

            _window = User32.CreateWindowExW(
                (uint)User32.WsExToolWindow,
                _windowClassName,
                "GlazeShell tray window",
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
                _icon = LoadApplicationIcon();
                added = Volatile.Read(ref _disposed) == 0 && AddIcon();
            }

            _registered = added;
            ready.Set();

            if (!added || Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            while (User32.GetMessage(out var message, 0, 0, 0) > 0)
            {
                User32.TranslateMessage(ref message);
                User32.DispatchMessage(ref message);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            _errorHandler?.Invoke(exception);
        }
        finally
        {
            ready.Set();
            _registered = false;
            if (_window != 0)
            {
                RemoveIcon();
                _ = User32.DestroyWindow(_window);
            }

            if (_ownsIcon && _icon != 0)
            {
                _ = User32.DestroyIcon(_icon);
            }

            _ownsIcon = false;
            _icon = 0;
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

        if (_activationMessage != 0 && message == _activationMessage)
        {
            Raise(OpenRequested);
            return 0;
        }

        if (_taskbarCreatedMessage != 0 && message == _taskbarCreatedMessage)
        {
            _ = AddIcon();
            return 0;
        }

        if (message != CallbackMessage)
        {
            return User32.DefWindowProc(hwnd, message, wParam, lParam);
        }

        var notification = (uint)(lParam.ToInt64() & 0xFFFF);

        if (notification == User32.WmLButtonDblClk)
        {
            Raise(OpenRequested);
        }
        else if (notification is User32.WmRButtonUp or User32.WmContextMenu)
        {
            ShowContextMenu(hwnd);
        }

        return 0;
    }

    private void ShowContextMenu(nint hwnd)
    {
        var menu = User32.CreatePopupMenu();

        if (menu == 0)
        {
            return;
        }

        try
        {
            _ = User32.AppendMenuW(menu, User32.MfString, OpenCommand, "Открыть Glaze Shell");
            _ = User32.AppendMenuW(menu, User32.MfString, SettingsCommand, "Настройки");
            _ = User32.AppendMenuW(menu, User32.MfSeparator, 0, null);
            _ = User32.AppendMenuW(menu, User32.MfString, ExitCommand, "Выход");
            _ = User32.GetCursorPos(out var position);

            // Перед TrackPopupMenu окно должно стать активным, иначе меню не закроется
            // при клике мимо; WM_NULL после закрытия сбрасывает «залипшее» состояние.
            _ = User32.SetForegroundWindow(hwnd);

            var command = User32.TrackPopupMenuEx(
                menu,
                User32.TpmRightButton | User32.TpmReturnCmd,
                position.X,
                position.Y,
                hwnd,
                0);

            _ = User32.PostMessage(hwnd, User32.WmNull, 0, 0);

            switch (command)
            {
                case OpenCommand:
                    Raise(OpenRequested);
                    break;
                case SettingsCommand:
                    Raise(SettingsRequested);
                    break;
                case ExitCommand:
                    Raise(ExitRequested);
                    break;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            _errorHandler?.Invoke(exception);
        }
        finally
        {
            _ = User32.DestroyMenu(menu);
        }
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

    private bool AddIcon()
    {
        var data = CreateData();
        return Shell32.Shell_NotifyIconW(Shell32.NimAdd, ref data);
    }

    private void RemoveIcon()
    {
        var data = CreateData();
        _ = Shell32.Shell_NotifyIconW(Shell32.NimDelete, ref data);
    }

    private Shell32.NotifyIconData CreateData() => new()
    {
        Size = (uint)Marshal.SizeOf<Shell32.NotifyIconData>(),
        Window = _window,
        Id = TrayIconId,
        Flags = Shell32.NifMessage | Shell32.NifIcon | Shell32.NifTip,
        CallbackMessage = CallbackMessage,
        Icon = _icon,
        Tip = "Glaze Shell",
        Info = string.Empty,
        InfoTitle = string.Empty,
    };

    /// <summary>
    /// Иконка приложения из исполняемого файла. Если извлечь её не удалось, берётся
    /// стандартная иконка Windows, чтобы значок трея не оказался пустым.
    /// </summary>
    private nint LoadApplicationIcon()
    {
        try
        {
            var path = Environment.ProcessPath;

            if (!string.IsNullOrEmpty(path))
            {
                _ = Shell32.ExtractIconExW(path, 0, out var large, out var small, 1);
                if (large != 0)
                {
                    _ = User32.DestroyIcon(large);
                }

                if (small != 0)
                {
                    _ownsIcon = true;
                    return small;
                }
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            _errorHandler?.Invoke(exception);
        }

        _ownsIcon = false;
        return User32.LoadIconW(0, User32.IdiApplication);
    }

    private void Raise(EventHandler? handler)
    {
        if (handler is null)
        {
            return;
        }

        try
        {
            handler(this, EventArgs.Empty);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            _errorHandler?.Invoke(exception);
        }
    }

    private void Stop()
    {
        if (_window != 0)
        {
            _ = User32.PostMessage(_window, User32.WmQuit, 0, 0);
        }
        else if (_threadId != 0)
        {
            _ = User32.PostThreadMessage(_threadId, User32.WmQuit, 0, 0);
        }

        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
        _window = 0;
        _threadId = 0;
    }
}
