using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.WindowManagement;

/// <summary>
/// Настройка окна-оверлея (верхняя панель): поверх всех окон, без записи в панели задач,
/// со скруглёнными углами и возможностью показать окно без перехвата фокуса.
/// </summary>
public static class OverlayWindow
{
    /// <summary>
    /// Помечает окно как topmost tool-window: оно не появляется в панели задач и в Alt+Tab,
    /// всегда остаётся поверх остальных и не перекрашивается при переключении.
    /// </summary>
    public static void MakeTopMostToolWindow(nint hwnd)
    {
        if (hwnd == 0)
        {
            return;
        }

        var style = (nint)User32.GetWindowLongPtr(hwnd, User32.GwlExStyle);
        style |= (nint)(User32.WsExToolWindow | User32.WsExTopmost);
        User32.SetWindowLongPtr(hwnd, User32.GwlExStyle, style);

        User32.SetWindowPos(
            hwnd,
            User32.HwndTopmost,
            0,
            0,
            0,
            0,
            User32.SwpNoMove | User32.SwpNoSize | User32.SwpNoActivate);
    }

    /// <summary>
    /// Включает или выключает стиль <c>WS_EX_NOACTIVATE</c>. Пока он включён, клик по окну
    /// не активирует его и не забирает фокус у активного приложения.
    /// </summary>
    public static void SetNonActivating(nint hwnd, bool nonActivating)
    {
        if (hwnd == 0)
        {
            return;
        }

        var style = (nint)User32.GetWindowLongPtr(hwnd, User32.GwlExStyle);

        style = nonActivating
            ? style | (nint)User32.WsExNoActivate
            : style & (nint)~User32.WsExNoActivate;

        User32.SetWindowLongPtr(hwnd, User32.GwlExStyle, style);
    }

    /// <summary>
    /// Включает системное скругление углов окна (Windows 11). На Windows 10 атрибут
    /// игнорируется, и окно остаётся прямоугольным.
    /// </summary>
    public static void UseRoundedCorners(nint hwnd)
    {
        if (hwnd == 0)
        {
            return;
        }

        var preference = Dwmapi.DwmcpRound;
        _ = Dwmapi.DwmSetWindowAttribute(hwnd, Dwmapi.DwmwaWindowCornerPreference, ref preference, sizeof(int));
    }

    /// <summary>
    /// Показывает окно, не переводя на него фокус ввода (SW_SHOWNOACTIVATE).
    /// </summary>
    public static void ShowWithoutActivation(nint hwnd)
    {
        if (hwnd == 0)
        {
            return;
        }

        User32.ShowWindow(hwnd, User32.SwShowNoActivate);
    }

    /// <summary>
    /// Масштаб монитора окна (1.0 при 96 DPI). Окно верхней панели задаётся в физических
    /// пикселях, поэтому его размеры умножаются на масштаб, чтобы совпасть с XAML.
    /// </summary>
    public static double GetScaleFactor(nint hwnd)
    {
        if (hwnd == 0)
        {
            return 1.0;
        }

        var dpi = User32.GetDpiForWindow(hwnd);
        return dpi == 0 ? 1.0 : dpi / 96.0;
    }
}
