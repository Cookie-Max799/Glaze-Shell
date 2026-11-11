using System.Diagnostics;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Shell;

/// <summary>
/// Действия оболочки Windows, доступные с верхней панели: свернуть все окна и открыть
/// список виртуальных рабочих столов.
/// </summary>
/// <remarks>
/// Обе операции выполняет сама Windows: приложение только просит оболочку. Это дешевле
/// и надёжнее, чем воспроизводить поведение через перебор окон.
/// </remarks>
public static class DesktopActions
{
    /// <summary>
    /// Команда «Показать рабочий стол» панели задач. Значение 419 — штатная команда
    /// <c>Shell_TrayWnd</c>, которую Windows использует для того же действия.
    /// </summary>
    private const int ShowDesktopCommand = 419;

    /// <summary>Идентификатор виртуальных рабочих столов в оболочке Windows.</summary>
    private const string TaskViewTarget = "shell:::{3080F90E-D7AD-11D9-BD98-0000947B0257}";

    /// <summary>Сворачивает все окна и показывает рабочий стол.</summary>
    public static bool ShowDesktop()
    {
        var taskbar = User32.FindWindow("Shell_TrayWnd", null);

        return taskbar != 0 &&
               User32.PostMessage(taskbar, User32.WmCommand, ShowDesktopCommand, 0);
    }

    /// <summary>Открывает представление задач (виртуальные рабочие столы Windows).</summary>
    public static bool ShowTaskView()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = TaskViewTarget,
                UseShellExecute = true,
            });

            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return false;
        }
    }
}
