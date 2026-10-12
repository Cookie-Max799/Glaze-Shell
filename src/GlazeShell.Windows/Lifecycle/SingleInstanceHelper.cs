using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
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
    /// из свёрнутого состояния и пытается вывести на передний план. Фокус — best-effort:
    /// при foreground lock система может отклонить перевод на передний план, и это не ошибка.
    /// </summary>
    public static void ActivateExistingInstance()
    {
        var hwnd = FindOwnVisibleWindow();
        if (hwnd == 0)
        {
            return;
        }

        if (User32.IsIconic(hwnd))
        {
            _ = User32.ShowWindowAsync(hwnd, User32.SwRestore);
        }

        _ = User32.SetForegroundWindow(hwnd);
    }

    internal static nint FindOwnVisibleWindow()
    {
        var ownPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(ownPath))
        {
            return 0;
        }

        nint found = 0;

        User32.EnumWindows((hwnd, _) =>
        {
            if (!User32.IsWindowVisible(hwnd))
            {
                return true;
            }

            var threadId = User32.GetWindowThreadProcessId(hwnd, out uint processId);
            if (threadId == 0 || processId == 0 || !IsOwnProcess(processId, ownPath))
            {
                return true;
            }

            found = hwnd;
            return false;
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