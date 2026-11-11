using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using GlazeShell.Windows.Shell;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Lifecycle;

/// <summary>
/// Владение named mutex первого экземпляра приложения. Lease обязан быть освобождён
/// на том же потоке, на котором был получен, и только после того, как остальные
/// службы уже остановлены: освобождение мутекса делает возможным запуск нового
/// экземпляра, поэтому мутекс освобождается последним при завершении.
/// </summary>
public sealed class SingleInstanceLease : IDisposable
{
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private Mutex? _mutex;

    internal SingleInstanceLease(Mutex mutex) => _mutex = mutex;

    public void Dispose()
    {
        var mutex = Interlocked.Exchange(ref _mutex, null);
        if (mutex is null)
        {
            return;
        }

        if (Environment.CurrentManagedThreadId == _ownerThreadId)
        {
            mutex.ReleaseMutex();
        }

        mutex.Dispose();
    }
}

/// <summary>
/// Единая точка single-instance: первый экземпляр держит named mutex весь срок жизни,
/// повторный запуск активирует существующее окно и не создаёт второго экземпляра.
/// </summary>
public static class SingleInstanceHelper
{
    private const string DefaultMutexName = "GlazeShell.SingleInstance.v1";

    public static SingleInstanceLease? Acquire(string mutexName = DefaultMutexName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutexName);

        var mutex = new Mutex(initiallyOwned: false, mutexName, out _);

        bool acquired;
        try
        {
            acquired = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            // Предыдущий владелец завершился без освобождения: мутекс переходит к нам,
            // и запуск считается первым — приложение стартует, а не молча выходит.
            acquired = true;
        }

        if (!acquired)
        {
            mutex.Dispose();
            return null;
        }

        return new SingleInstanceLease(mutex);
    }

    /// <summary>
    /// Находит видимое top-level окно собственного приложения — по владеющему процессу,
    /// который запущен из того же исполняемого файла, — и поднимает его: восстанавливает
    /// из свёрнутого состояния и пытается вывести на передний план. Если видимого окна
    /// нет (главное свёрнуто в трей), работающему экземпляру отправляется широковещательное
    /// сообщение: он сам покажет главное окно.
    /// </summary>
    public static void ActivateExistingInstance()
    {
        RequestShowMainWindow();
        var hwnd = FindOwnVisibleWindow();
        if (hwnd == 0)
        {
            RequestShowMainWindow();
            return;
        }

        BringToForeground(hwnd);
    }

    /// <summary>
    /// Восстанавливает окно из свёрнутого состояния и пытается вывести его на передний
    /// план. Фокус — best-effort: при foreground lock система может отклонить перевод,
    /// и это не ошибка. Повторный запуск и значок трея используют один и тот же путь.
    /// </summary>
    public static void BringToForeground(nint hwnd)
    {
        if (hwnd == 0)
        {
            return;
        }

        if (User32.IsIconic(hwnd))
        {
            _ = User32.ShowWindowAsync(hwnd, User32.SwRestore);
        }

        // Второй процесс не может просто вызвать SetForegroundWindow: пока активно
        // чужое окно, система отклоняет перевод, и приложение выглядит «не открывшимся»
        // (а Windows при этом издаёт звук ошибки). Сначала разрешаем смену активного
        // окна всем процессам, затем присоединяем очередь ввода к потоку активного
        // окна — тот же приём, что у панелей при активации поиска.
        _ = User32.AllowSetForegroundWindow(User32.AsfwAny);

        // Поднимаем окно в верх обычного слоя: даже если активация не удалась,
        // главное окно окажется поверх остальных и его можно будет выбрать.
        _ = User32.SetWindowPos(
            hwnd,
            User32.HwndTop,
            0,
            0,
            0,
            0,
            User32.SwpNoMove | User32.SwpNoSize | User32.SwpNoActivate | User32.SwpShowWindow);

        var foreground = User32.GetForegroundWindow();
        var currentThread = Kernel32.GetCurrentThreadId();
        var foregroundThread = foreground == 0
            ? 0
            : User32.GetWindowThreadProcessId(foreground, out _);

        var attached = foregroundThread != 0 &&
                       foregroundThread != currentThread &&
                       User32.AttachThreadInput(currentThread, foregroundThread, true);

        try
        {
            _ = User32.SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached)
            {
                _ = User32.AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }

    /// <summary>
    /// Просит работающий экземпляр показать главное окно. Сообщение широковещательное,
    /// поэтому его получает скрытое окно значка трея: обычные окна приложения в этот
    /// момент могут быть невидимы.
    /// </summary>
    private static void RequestShowMainWindow()
    {
        var message = User32.RegisterWindowMessage(TrayIcon.ActivationMessageName);

        if (message != 0)
        {
            _ = User32.PostMessage(User32.HwndBroadcast, message, 0, 0);
        }
    }

    internal static nint FindOwnVisibleWindow()
    {
        var ownPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(ownPath))
        {
            return 0;
        }

        nint found = 0;
        long largestArea = 0;

        User32.EnumWindows((hwnd, _) =>
        {
            if (!User32.IsWindowVisible(hwnd))
            {
                return true;
            }

            // Панели Glaze Shell — topmost tool-окна. При повторном запуске нельзя
            // поднимать их вместо главного окна: иначе приложение «не открывается»,
            // хотя панели на экране.
            var exStyle = (nint)User32.GetWindowLongPtr(hwnd, User32.GwlExStyle);
            if ((exStyle & (nint)User32.WsExToolWindow) != 0)
            {
                return true;
            }

            var threadId = User32.GetWindowThreadProcessId(hwnd, out uint processId);
            if (threadId == 0 || processId == 0 || !IsOwnProcess(processId, ownPath))
            {
                return true;
            }

            // Среди обычных окон процесса (главное, проводник, настройки, предпросмотр)
            // берём самое большое: это главное окно, а не случайная карточка предпросмотра.
            if (User32.GetWindowRect(hwnd, out var bounds))
            {
                var area = (long)(bounds.Right - bounds.Left) * (bounds.Bottom - bounds.Top);
                if (area >= largestArea)
                {
                    largestArea = area;
                    found = hwnd;
                }
            }

            return true;
        }, 0);

        return found;
    }

    private static bool IsOwnProcess(uint processId, string ownPath)
    {
        if (processId == (uint)Environment.ProcessId)
        {
            return true;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return string.Equals(process.MainModule?.FileName, ownPath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }
}
