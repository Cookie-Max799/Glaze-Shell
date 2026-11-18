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

    private static readonly HashSet<string> SearchProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "SearchHost",
        "SearchUI",
        "SearchApp",
        "Cortana",
    };

    /// <summary>
    /// Процессы панели Widgets. Жест «три пальца» на части машин открывает именно её
    /// (<c>Win+W</c>), и она выглядит как отдельное окно поверх оболочки: Glaze Shell
    /// закрывает её так же, как поиск.
    /// </summary>
    private static readonly HashSet<string> WidgetsProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Widgets",
        "WidgetService",
    };

    /// <summary>
    /// Активное окно — поиск Windows. Такое окно Glaze Shell закрывает: жест «три пальца»
    /// по тачпаду на части машин открывает поиск в обход клавиатурного хука, и вместо
    /// него должен появиться интерфейс оболочки.
    /// </summary>
    /// <remarks>
    /// Имена различаются по версиям: в Windows 11 поиск живёт в <c>SearchHost.exe</c>,
    /// в Windows 10 — в <c>SearchUI.exe</c> (Cortana). Проверяются оба, иначе на десятке
    /// поиск оставался на экране поверх панелей.
    /// </remarks>
    public static bool IsSystemSearchUi(uint processId)
    {
        var name = GetProcessName(processId);
        return name is not null && SearchProcessNames.Contains(name);
    }

    /// <summary>
    /// Активное окно — панель Widgets. Жест «три пальца» открывает её как <c>Win+W</c>,
    /// и она появляется поверх оболочки отдельным окном, поэтому её нужно закрыть.
    /// </summary>
    public static bool IsWidgetsUi(uint processId)
    {
        var name = GetProcessName(processId);
        return name is not null && WidgetsProcessNames.Contains(name);
    }

    /// <summary>
    /// Системная поверхность, которую Glaze Shell закрывает по жесту «три пальца»:
    /// поиск Windows и панель Widgets.
    /// </summary>
    public static bool IsGestureSystemUi(uint processId)
    {
        var name = GetProcessName(processId);
        return name is not null && (SearchProcessNames.Contains(name) || WidgetsProcessNames.Contains(name) ||
            string.Equals(name, "StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Активное окно — поверхность рабочего стола (<c>Progman</c>/<c>WorkerW</c>).
    /// Панель задач и меню «Пуск» сюда не входят: это не «пустой» рабочий стол.
    /// </summary>
    public static bool IsDesktop(nint hwnd, uint processId)
    {
        var windowClass = User32.GetWindowClassName(hwnd);

        if (windowClass is not ("Progman" or "WorkerW"))
        {
            return false;
        }

        return string.Equals(GetProcessName(processId), "explorer", StringComparison.OrdinalIgnoreCase);
    }

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
