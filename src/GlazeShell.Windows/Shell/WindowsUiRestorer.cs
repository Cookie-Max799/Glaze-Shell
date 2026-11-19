using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Shell;

/// <summary>
/// Результат восстановления интерфейса Windows (функция I2).
/// </summary>
/// <param name="TaskbarRestored">Возвращена ли системная панель задач на место.</param>
/// <param name="DesktopIconsRestored">Возвращены ли иконки рабочего стола.</param>
/// <param name="Notes">Что именно было сделано — для журнала.</param>
public sealed record UiRestoreResult(bool TaskbarRestored, bool DesktopIconsRestored, IReadOnlyList<string> Notes)
{
    /// <summary>Ничего не потребовалось менять.</summary>
    public static UiRestoreResult Unchanged { get; } = new(false, false, []);

    /// <summary>Были ли внесены изменения.</summary>
    public bool HasChanges => TaskbarRestored || DesktopIconsRestored;
}

/// <summary>
/// Возвращает интерфейс Windows к штатному виду (функция I2).
/// </summary>
/// <remarks>
/// <para>
/// Восстановление нужно после падения оболочки: скрытая системная панель задач и
/// исчезнувшие иконки рабочего стола оставляют пользователя без рабочего стола, пока
/// приложение не перезапустят. Здесь выполняется только возврат системных окон на место:
/// настройки системы при этом не меняются.
/// </para>
/// <para>
/// Окно рабочего стола ищется по классу <c>SHELLDLL_DefView</c> внутри <c>Progman</c> или
/// <c>WorkerW</c>: это тот самый список значков, который прячет Explorer.
/// </para>
/// </remarks>
public static class WindowsUiRestorer
{
    private const string ProgmanClassName = "Progman";
    private const string WorkerWClassName = "WorkerW";
    private const string DesktopListClassName = "SHELLDLL_DefView";

    /// <summary>
    /// Возвращает системную панель задач и иконки рабочего стола на место.
    /// </summary>
    public static UiRestoreResult Restore()
    {
        var notes = new List<string>();

        var taskbarRestored = TaskbarHider.RestoreIfOffScreen();
        if (taskbarRestored)
        {
            notes.Add("The taskbar was moved back on screen.");
        }

        var desktopIconsRestored = RestoreDesktopIcons();
        if (desktopIconsRestored)
        {
            notes.Add("The desktop icons were shown again.");
        }

        return new UiRestoreResult(taskbarRestored, desktopIconsRestored, notes);
    }

    /// <summary>
    /// Показывает окно со значками рабочего стола, если оно скрыто, и возвращает его на
    /// экран, если оно оказалось целиком за границей монитора.
    /// </summary>
    private static bool RestoreDesktopIcons()
    {
        // Список значков мог быть скрыт слоем Glaze, а не всем окном рабочего стола:
        // ShowDesktopIcons возвращает видимость списку внутри видимого Progman/WorkerW.
        var restored = ShowDesktopIcons();

        var host = FindDesktopIconHost();

        if (host == 0)
        {
            return restored;
        }

        if (!User32.IsWindowVisible(host))
        {
            User32.ShowWindow(host, User32.SwShow);
            restored = true;
        }

        if (User32.GetWindowRect(host, out var bounds) &&
            TryGetMonitorBounds(host, out var monitor) &&
            IsOutsideMonitor(bounds, monitor))
        {
            User32.SetWindowPos(
                host,
                0,
                monitor.Left,
                monitor.Top,
                0,
                0,
                User32.SwpNoSize | User32.SwpNoZOrder | User32.SwpNoActivate);

            restored = true;
        }

        return restored;
    }

    /// <summary>
    /// Скрывает значки рабочего стола Explorer, пока работает слой Glaze. Скрывается только
    /// список значков <c>SHELLDLL_DefView</c>: фон рабочего стола и обои рисует родительское
    /// окно, поэтому картинка остаётся на месте.
    /// </summary>
    /// <returns><see langword="true"/>, если окно значков найдено и скрыто.</returns>
    public static bool HideDesktopIcons()
    {
        var list = FindDesktopIconList();

        if (list == 0 || !User32.IsWindowVisible(list))
        {
            return false;
        }

        User32.ShowWindow(list, User32.SwHide);
        return true;
    }

    /// <summary>
    /// Показывает значки рабочего стола Explorer. Безопасно вызывать повторно: если окно
    /// значков уже видно или не найдено, ничего не происходит.
    /// </summary>
    /// <returns><see langword="true"/>, если окно значков было скрыто и показано снова.</returns>
    public static bool ShowDesktopIcons()
    {
        var list = FindDesktopIconList();

        if (list == 0 || User32.IsWindowVisible(list))
        {
            return false;
        }

        User32.ShowWindow(list, User32.SwShow);
        return true;
    }

    /// <summary>
    /// Находит список значков рабочего стола (<c>SHELLDLL_DefView</c>) внутри <c>Progman</c>
    /// или <c>WorkerW</c>. Возвращает 0, если окно не найдено.
    /// </summary>
    private static nint FindDesktopIconList()
    {
        var progman = User32.FindWindow(ProgmanClassName, null);

        if (progman != 0)
        {
            var list = User32.FindWindowEx(progman, 0, DesktopListClassName, null);

            if (list != 0)
            {
                return list;
            }
        }

        nint found = 0;

        User32.EnumWindows((hwnd, _) =>
        {
            if (!IsClass(hwnd, WorkerWClassName))
            {
                return true;
            }

            var list = User32.FindWindowEx(hwnd, 0, DesktopListClassName, null);

            if (list == 0)
            {
                return true;
            }

            found = list;
            return false;
        }, 0);

        return found;
    }

    /// <summary>
    /// Находит окно, в котором живут значки рабочего стола. В разных версиях Windows
    /// список значков находится либо прямо в <c>Progman</c>, либо в отдельном <c>WorkerW</c>.
    /// </summary>
    private static nint FindDesktopIconHost()
    {
        var progman = User32.FindWindow(ProgmanClassName, null);

        if (progman != 0 && User32.FindWindowEx(progman, 0, DesktopListClassName, null) != 0)
        {
            return progman;
        }

        nint host = 0;

        User32.EnumWindows((hwnd, _) =>
        {
            if (!IsClass(hwnd, WorkerWClassName))
            {
                return true;
            }

            if (User32.FindWindowEx(hwnd, 0, DesktopListClassName, null) == 0)
            {
                return true;
            }

            host = hwnd;
            return false;
        }, 0);

        return host;
    }

    private static bool IsClass(nint hwnd, string className)
    {
        var buffer = new char[64];
        var length = User32.GetClassName(hwnd, buffer, buffer.Length);

        if (length <= 0)
        {
            return false;
        }

        return string.Equals(new string(buffer, 0, length), className, StringComparison.Ordinal);
    }

    /// <summary>
    /// Проверяет, что окно целиком находится за пределами монитора. Внутренний метод,
    /// чтобы правило можно было проверить тестом без реального рабочего стола.
    /// </summary>
    internal static bool IsOutsideMonitor(Rect bounds, Rect monitor) =>
        bounds.Left >= monitor.Right ||
        bounds.Top >= monitor.Bottom ||
        bounds.Right <= monitor.Left ||
        bounds.Bottom <= monitor.Top;

    private static bool TryGetMonitorBounds(nint window, out Rect monitor)
    {
        monitor = default;

        var handle = User32.MonitorFromWindow(window, User32.MonitorDefaultToNearest);

        if (handle == 0)
        {
            return false;
        }

        var info = new MonitorInfoEx { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MonitorInfoEx>() };

        if (!User32.GetMonitorInfoW(handle, ref info))
        {
            return false;
        }

        monitor = info.Monitor;
        return true;
    }
}
