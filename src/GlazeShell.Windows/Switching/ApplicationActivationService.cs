using GlazeShell.Core.Switching;
using GlazeShell.Windows.Win32;
using GlazeShell.Windows.WindowManagement;

namespace GlazeShell.Windows.Switching;

/// <summary>
/// Действия переключателя приложений над чужими окнами: активация, сворачивание,
/// закрытие и циклический переход по окнам одного приложения (Alt+`).
/// </summary>
/// <remarks>
/// Активация идёт через <see cref="OverlayWindow.ActivateForInput"/>: панель переключателя
/// показана без активации, и обычный <c>SetForegroundWindow</c> из фонового процесса
/// система отклоняет. Если окно принадлежит приложению с повышенными правами, фокус
/// может не передаться — метод честно возвращает <see langword="false"/>, чтобы
/// вызывающий не считал переключение состоявшимся.
/// </remarks>
public static class ApplicationActivationService
{
    public static bool Activate(SwitchableWindow window, bool restoreMinimized)
    {
        var hwnd = (nint)window.Handle;
        if (!User32.IsWindow(hwnd))
        {
            return false;
        }

        if (restoreMinimized && User32.IsIconic(hwnd))
        {
            _ = User32.ShowWindow(hwnd, User32.SwRestore);
        }

        if (OverlayWindow.ActivateForInput(hwnd))
        {
            return true;
        }

        User32.SwitchToThisWindow(hwnd, true);
        return User32.GetForegroundWindow() == hwnd;
    }

    public static bool Minimize(SwitchableWindow window)
    {
        var hwnd = (nint)window.Handle;
        if (!User32.IsWindow(hwnd) || User32.IsIconic(hwnd))
        {
            return false;
        }

        _ = User32.ShowWindow(hwnd, User32.SwMinimize);
        return User32.IsIconic(hwnd);
    }

    public static void Restore(SwitchableWindow window)
    {
        var hwnd = (nint)window.Handle;
        if (User32.IsWindow(hwnd) && User32.IsIconic(hwnd))
        {
            _ = User32.ShowWindow(hwnd, User32.SwShowNoActivate);
        }
    }

    public static bool Close(SwitchableWindow window) =>
        User32.PostMessage((nint)window.Handle, User32.WmClose, 0, 0);

    /// <summary>
    /// Переключает окна приложения, которому принадлежит активное окно (Alt+`).
    /// Окна берутся в порядке Z-порядка; переход циклический.
    /// </summary>
    /// <returns><see langword="true"/>, если фокус перешёл к другому окну.</returns>
    public static bool CycleForegroundWindow()
    {
        var foreground = User32.GetForegroundWindow();

        if (foreground == 0)
        {
            return false;
        }

        var windows = SwitchableApplicationProvider.GetGroupWindows(foreground);

        if (windows.Count < 2)
        {
            return false;
        }

        var index = -1;

        for (var i = 0; i < windows.Count; i++)
        {
            if ((nint)windows[i].Handle == foreground)
            {
                index = i;
                break;
            }
        }

        var target = windows[(index + 1) % windows.Count];
        var hwnd = (nint)target.Handle;

        if (hwnd == 0 || !User32.IsWindow(hwnd))
        {
            return false;
        }

        if (User32.IsIconic(hwnd))
        {
            _ = User32.ShowWindow(hwnd, User32.SwRestore);
        }

        return OverlayWindow.ActivateForInput(hwnd);
    }
}
