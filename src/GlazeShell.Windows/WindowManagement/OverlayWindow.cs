using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.WindowManagement;

/// <summary>Прямоугольник в физических пикселях окна для региональной маски.</summary>
/// <param name="X">Левый край относительно окна.</param>
/// <param name="Y">Верхний край относительно окна.</param>
/// <param name="Width">Ширина.</param>
/// <param name="Height">Высота.</param>
public readonly record struct PixelRect(int X, int Y, int Width, int Height);

/// <summary>
/// Скруглённый прямоугольник региональной маски с собственным радиусом: у капсул-плиток
/// радиус равен половине высоты, у popover он больше (DESIGN §4).
/// </summary>
/// <param name="Bounds">Границы в физических пикселях окна.</param>
/// <param name="CornerRadius">Радиус скругления углов.</param>
public readonly record struct PixelRegion(PixelRect Bounds, int CornerRadius);

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
        if (regions is null || regions.Count == 0)
        {
            return false;
        }

        var regionsWithRadius = new List<PixelRegion>(regions.Count);

        foreach (var rect in regions)
        {
            regionsWithRadius.Add(new PixelRegion(rect, cornerRadius));
        }

        return ApplyRoundedRegions(hwnd, regionsWithRadius);
    }

    /// <summary>
    /// Придаёт окну форму прямоугольника со скруглёнными <b>только верхними</b> углами:
    /// нижний край остаётся прямым, поэтому панель не парит над краем экрана, а
    /// «растворяется» в нём. Нужная форма получается объединением капсулы с нижней
    /// полосой прямоугольника высотой в радиус. Возвращает <c>false</c>, если маску
    /// задать не удалось: тогда окно остаётся прямоугольным.
    /// </summary>
    public static bool ApplyTopRoundedRegion(nint hwnd, PixelRect rect, int cornerRadius)
    {
        if (hwnd == 0 || rect.Width <= 0 || rect.Height <= 0)
        {
            return false;
        }

        var radius = Math.Clamp(cornerRadius, 0, Math.Min(rect.Width, rect.Height) / 2);
        var diameter = radius * 2;

        var shape = Gdi32.CreateRoundRectRgn(
            rect.X,
            rect.Y,
            rect.X + rect.Width + 1,
            rect.Y + rect.Height + 1,
            diameter,
            diameter);

        if (shape == 0)
        {
            return false;
        }

        if (radius > 0)
        {
            // Полоса начинается там, где у капсулы заканчиваются дуги нижних углов:
            // объединение закрывает только углы, верхнее скругление не меняется.
            var band = Gdi32.CreateRectRgn(
                rect.X,
                rect.Y + rect.Height - radius,
                rect.X + rect.Width + 1,
                rect.Y + rect.Height + 1);

            if (band != 0)
            {
                _ = Gdi32.CombineRgn(shape, shape, band, Gdi32.RgnOr);
                _ = Gdi32.DeleteObject(band);
            }
        }

        // При успехе владельцем области становится система: повторно удалять её нельзя.
        if (User32.SetWindowRgn(hwnd, shape, true) == 0)
        {
            _ = Gdi32.DeleteObject(shape);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Придаёт окну форму набора скруглённых прямоугольников, каждый со своим радиусом.
    /// Возвращает <c>false</c>, если маску задать не удалось: тогда окно остаётся прямоугольным.
    /// </summary>
    public static bool ApplyRoundedRegions(nint hwnd, IReadOnlyList<PixelRegion> regions)
    {
        if (hwnd == 0 || regions is null || regions.Count == 0)
        {
            return false;
        }

        nint combined = 0;

        foreach (var region in regions)
        {
            var rect = region.Bounds;

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                continue;
            }

            var diameter = Math.Max(0, region.CornerRadius) * 2;

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
