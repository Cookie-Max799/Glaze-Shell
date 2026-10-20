using GlazeShell.Core.Models;

namespace GlazeShell.Core.Services;

/// <summary>
/// Преобразует выбранный пользователем <see cref="ThemeMode"/> в конкретную тему
/// (светлую или тёмную), разрешая <see cref="ThemeMode.System"/> через системную настройку.
/// </summary>
public static class ThemeModeResolver
{
    /// <param name="mode">Режим, выбранный пользователем.</param>
    /// <param name="systemUsesLightTheme">
    /// Признак того, что система сейчас использует светлую тему (например, значение
    /// <c>AppsUseLightTheme</c> в реестре Windows).
    /// </param>
    public static ThemeMode Resolve(ThemeMode mode, bool systemUsesLightTheme) => mode switch
    {
        ThemeMode.Light => ThemeMode.Light,
        ThemeMode.Dark => ThemeMode.Dark,
        _ => systemUsesLightTheme ? ThemeMode.Light : ThemeMode.Dark
    };
}
