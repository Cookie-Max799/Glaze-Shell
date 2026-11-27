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
    /// <summary>
    /// Выводит приложение на передний план.
    /// </summary>
    /// <param name="application">Приложение из снимка переключателя.</param>
    /// <param name="restoreMinimized">
    /// Развернуть свёрнутое окно. Если разворачивать не просили, но все окна приложения
    /// свёрнуты, последнее активное всё равно разворачивается: иначе выбранное приложение
    /// осталось бы за кадром и переключение выглядело бы как отказ.
    /// </param>
    /// <param name="preferredWindow">
    /// Конкретное окно (например, выбранное в раскрытом списке) или 0.
    /// </param>
    /// <remarks>
    /// Без явного окна поднимается последнее активное окно приложения
    /// (<see cref="SwitchableApplication.MostRecentWindow"/>), а не любое несвёрнутое:
    /// пользователь ждёт возврата к тому окну, в котором работал. Свёрнутое окно
    /// разворачивается только по Shift или настройке.
    /// </remarks>
    public static bool Activate(SwitchableApplication application, bool restoreMinimized, nint preferredWindow = 0)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (application.Windows.Count == 0)
        {
            return false;
        }

        var target = preferredWindow != 0
            ? application.Windows.FirstOrDefault(window => (nint)window.Handle == preferredWindow)
            : null;

        target ??= application.MostRecentWindow;

        if (target is null)
        {
            return false;
        }

        var hwnd = (nint)target.Handle;

        if (hwnd == 0 || !User32.IsWindow(hwnd))
        {
            // Кэш снимка мог устареть, если приложение закрылось между показом и выбором.
            return false;
        }

        var minimized = User32.IsIconic(hwnd);

        if (minimized && (restoreMinimized || preferredWindow != 0))
        {
            _ = User32.ShowWindow(hwnd, User32.SwRestore);
        }

        if (!OverlayWindow.ActivateForInput(hwnd))
        {
            // Ограничение foreground lock: панель задач обходит его тем же способом.
            User32.SwitchToThisWindow(hwnd, true);
            return User32.GetForegroundWindow() == hwnd;
        }

        return true;
    }

    /// <summary>
    /// Сворачивает все окна приложения (клавиша H). Приложение остаётся в снимке.
    /// </summary>
    /// <returns>Сколько окон удалось свернуть.</returns>
    public static int Minimize(SwitchableApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        var count = 0;

        foreach (var window in application.Windows)
        {
            var hwnd = (nint)window.Handle;

            if (hwnd != 0 && User32.IsWindow(hwnd) && User32.ShowWindow(hwnd, User32.SwMinimize))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Возвращает окна приложения на экран после временного H, не забирая фокус.
    /// </summary>
    /// <remarks>
    /// Скрытие клавишей H действует только на время переключения: при отпускании Alt
    /// приложение возвращается, иначе пользователь терял бы окна, которые не собирался
    /// закрывать. <c>SW_SHOWNOACTIVATE</c> разворачивает свёрнутое окно, не перебивая
    /// фокус у выбранного приложения.
    /// </remarks>
    /// <returns>Сколько окон было показано.</returns>
    public static int Restore(SwitchableApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        var count = 0;

        foreach (var window in application.Windows)
        {
            var hwnd = (nint)window.Handle;

            if (hwnd != 0 && User32.IsWindow(hwnd) && User32.IsIconic(hwnd) &&
                User32.ShowWindow(hwnd, User32.SwShowNoActivate))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Просит приложение закрыться (клавиша Q), отправляя <c>WM_CLOSE</c> всем окнам.
    /// Приложение может показать диалог сохранения или отказаться закрываться — переключатель
    /// этого не ждёт и не блокирует панель.
    /// </summary>
    /// <returns>Сколько окон получили запрос.</returns>
    public static int Close(SwitchableApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        var count = 0;

        foreach (var window in application.Windows)
        {
            var hwnd = (nint)window.Handle;

            if (hwnd != 0 && User32.IsWindow(hwnd) && User32.PostMessage(hwnd, User32.WmClose, 0, 0))
            {
                count++;
            }
        }

        return count;
    }

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
