using System.Diagnostics;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Shell;

/// <summary>
/// Классификация активного окна для автоскрытия панелей: рабочий стол и системные
/// поверхности считаются «домом», остальные окна — чужими.
/// </summary>
/// <remarks>
/// Одного имени процесса недостаточно: <c>explorer.exe</c> владеет и рабочим столом
/// (<c>Progman</c>/<c>WorkerW</c>), и представлением задач, которое открывается свайпом
/// тремя пальцами вверх. Если считать весь explorer «своим», панели остаются висеть
/// поверх списка задач. Поэтому для explorer дополнительно проверяется класс окна:
/// домашними считаются только поверхности рабочего стола и панели задач.
/// </remarks>
public static class ShellForeground
{
    private static readonly HashSet<string> ShellProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "StartMenuExperienceHost",
        "SearchHost",
        "ShellExperienceHost",
        "TextInputHost",
        "LockApp",
    };

    private static readonly HashSet<string> DesktopWindowClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow",
        "TaskListThumbnailWnd",
    };

    /// <summary>
    /// Активное окно — рабочий стол или системная поверхность (меню «Пуск», поиск,
    /// уведомления). Представление задач и переключатель окон сюда не входят: панели
    /// должны убираться, чтобы не закрывать список.
    /// </summary>
    public static bool IsDesktopOrShellUi(nint hwnd, uint processId)
    {
        var windowClass = User32.GetWindowClassName(hwnd);
        if (windowClass is "MultitaskingViewFrame" or "TaskSwitcherWnd" or "XamlExplorerHostIslandWindow")
        {
            return false;
        }

        var name = GetProcessName(processId);

        if (name is null)
        {
            return false;
        }

        if (string.Equals(name, "explorer", StringComparison.OrdinalIgnoreCase))
        {
            return DesktopWindowClasses.Contains(windowClass);
        }

        return ShellProcessNames.Contains(name);
    }

    private static string? GetProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
