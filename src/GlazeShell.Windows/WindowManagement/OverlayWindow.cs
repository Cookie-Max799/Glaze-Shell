using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.WindowManagement;

/// <summary>Прямоугольник в физических пикселях окна для региональной маски.</summary>
/// <param name="X">Левый край относительно окна.</param>
/// <param name="Y">Верхний край относительно окна.</param>
/// <param name="Width">Ширина.</param>
/// <param name="Height">Высота.</param>
public readonly record struct PixelRect(int X, int Y, int Width, int Height);

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

    /// <summary>Размер клиентской области окна в физических пикселях.</summary>
    public static bool TryGetClientSize(nint hwnd, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (hwnd == 0 || !User32.GetClientRect(hwnd, out var rect))
        {
            return false;
        }

        width = rect.Right - rect.Left;
        height = rect.Bottom - rect.Top;
        return width > 0 && height > 0;
    }

    /// <summary>
    /// Придаёт окну форму набора скруглённых прямоугольников (регион окна) в физических
    /// пикселях. Так «стеклянный» backdrop рисуется только внутри pill, а промежутки между
    /// ними пропускают рабочий стол. Возвращает <c>false</c>, если маску задать не удалось:
    /// тогда окно остаётся прямоугольным, как раньше.
    /// </summary>
    public static bool ApplyRoundedRegions(nint hwnd, IReadOnlyList<PixelRect> regions, int cornerRadius)
    {
        if (hwnd == 0 || regions is null || regions.Count == 0)
        {
            return false;
        }

        var diameter = Math.Max(0, cornerRadius) * 2;
        nint combined = 0;

        foreach (var rect in regions)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                continue;
            }

            var part = Gdi32.CreateRoundRectRgn(
                rect.X,
                rect.Y,
                rect.X + rect.Width + 1,
                rect.Y + rect.Height + 1,
                diameter,
                diameter);

            if (part == 0)
            {
                continue;
            }

            if (combined == 0)
            {
                combined = part;
                continue;
            }

            _ = Gdi32.CombineRgn(combined, combined, part, Gdi32.RgnOr);
            _ = Gdi32.DeleteObject(part);
        }

        if (combined == 0)
        {
            return false;
        }

        // При успехе владельцем области становится система: повторно удалять её нельзя.
        if (User32.SetWindowRgn(hwnd, combined, true) == 0)
        {
            _ = Gdi32.DeleteObject(combined);
            return false;
        }

        return true;
    }
}
