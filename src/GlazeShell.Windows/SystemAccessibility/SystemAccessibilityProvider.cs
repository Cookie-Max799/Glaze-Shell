using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.SystemAccessibility;

/// <summary>
/// Цвет системной палитры Windows (Rgba). Нужен, чтобы в режиме высокой контрастности
/// интерфейс брал цвета у пользователя, а не придумывал свои.
/// </summary>
/// <param name="R">Красный канал.</param>
/// <param name="G">Зелёный канал.</param>
/// <param name="B">Синий канал.</param>
/// <param name="A">Прозрачность.</param>
public readonly record struct SystemColor(byte R, byte G, byte B, byte A);

/// <summary>
/// Системная палитра для режима высокой контрастности: фон, текст, выделение и
/// приглушённый текст.
/// </summary>
/// <param name="Background">Цвет фона окна.</param>
/// <param name="Foreground">Цвет основного текста.</param>
/// <param name="Highlight">Цвет выделения.</param>
/// <param name="HighlightText">Цвет текста на выделении.</param>
/// <param name="Surface">Цвет поверхности (панели, поля ввода).</param>
/// <param name="SurfaceText">Цвет текста на поверхности.</param>
/// <param name="Muted">Цвет приглушённого текста.</param>
public readonly record struct SystemAccessibilityPalette(
    SystemColor Background,
    SystemColor Foreground,
    SystemColor Highlight,
    SystemColor HighlightText,
    SystemColor Surface,
    SystemColor SurfaceText,
    SystemColor Muted);

/// <summary>
/// Снимок системных настроек доступности (функция I5).
/// </summary>
/// <param name="HighContrast">Включена ли в Windows тема высокой контрастности.</param>
/// <param name="AnimationsEnabled">Разрешены ли в Windows анимации.</param>
/// <param name="Palette">Системная палитра для контрастного режима.</param>
public readonly record struct SystemAccessibilityInfo(
    bool HighContrast,
    bool AnimationsEnabled,
    SystemAccessibilityPalette Palette);

/// <summary>
/// Читает системные настройки доступности Windows (функция I5): тему высокой контрастности
/// и разрешение анимаций. Обе настройки меняются пользователем в Панели управления, поэтому
/// значения читаются по требованию, а не кэшируются навсегда.
/// </summary>
/// <remarks>
/// Провайдер best-effort: если системный вызов не удался, считается, что контрастность
/// выключена, анимации разрешены, а палитра остаётся обычной. Ошибка не должна мешать
/// запуску оболочки.
/// </remarks>
public static class SystemAccessibilityProvider
{
    /// <summary>
    /// Читает текущее состояние системных настроек доступности.
    /// </summary>
    public static SystemAccessibilityInfo GetInfo()
    {
        var highContrast = IsHighContrastEnabled();
        var animationsEnabled = AreAnimationsEnabled();

        return new SystemAccessibilityInfo(
            highContrast,
            animationsEnabled,
            ReadPalette());
    }

    /// <summary>
    /// Включена ли тема высокой контрастности (<c>SPI_GETHIGHCONTRAST</c>).
    /// </summary>
    public static bool IsHighContrastEnabled()
    {
        try
        {
            var highContrast = new HighContrast
            {
                Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<HighContrast>()
            };

            if (!User32.SystemParametersInfoW(User32.SpiGetHighContrast, highContrast.Size, ref highContrast, 0))
            {
                return false;
            }

            return (highContrast.Flags & User32.HcfHighContrastOn) != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Разрешены ли анимации (<c>SPI_GETCLIENTAREAANIMATION</c>). Это системная настройка
    /// «Показывать анимации в Windows»: выключенная в системе анимация должна исчезнуть и
    /// в оболочке.
    /// </summary>
    public static bool AreAnimationsEnabled()
    {
        try
        {
            uint enabled = 1;

            if (!User32.SystemParametersInfoW(User32.SpiGetClientAreaAnimation, 0, ref enabled, 0))
            {
                return true;
            }

            return enabled != 0;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>
    /// Читает системную палитру. Используется только в контрастном режиме: обычные цвета
    /// оболочка задаёт сама, а пользовательские цвета Windows нужны именно там, где
    /// приглушённые оттенки нечитаемы.
    /// </summary>
    private static SystemAccessibilityPalette ReadPalette() => new(
        ReadColor(User32.ColorWindow),
        ReadColor(User32.ColorWindowText),
        ReadColor(User32.ColorHighlight),
        ReadColor(User32.ColorHighlightText),
        ReadColor(User32.ColorButtonFace),
        ReadColor(User32.ColorButtonText),
        ReadColor(User32.ColorGrayText));

    /// <summary>
    /// Читает системный цвет. Значение приходит в формате COLORREF (0x00BBGGRR), поэтому
    /// каналы меняются местами.
    /// </summary>
    private static SystemColor ReadColor(int index)
    {
        try
        {
            var color = User32.GetSysColor(index);

            return new SystemColor(
                (byte)(color & 0xFF),
                (byte)((color >> 8) & 0xFF),
                (byte)((color >> 16) & 0xFF),
                255);
        }
        catch (Exception)
        {
            return new SystemColor(0, 0, 0, 255);
        }
    }
}
