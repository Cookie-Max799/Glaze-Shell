namespace GlazeShell.Core.Models;

/// <summary>
/// Режим оформления, выбранный пользователем: следовать системе либо явно светлая/тёмная тема.
/// </summary>
public enum ThemeMode
{
    System = 0,
    Light = 1,
    Dark = 2
}

/// <summary>
/// Строковые идентификаторы режима темы для пользовательских данных.
/// </summary>
public static class ThemeModeExtensions
{
    public const string SystemId = "system";

    public const string LightId = "light";

    public const string DarkId = "dark";

    public static string ToId(this ThemeMode mode) => mode switch
    {
        ThemeMode.Light => LightId,
        ThemeMode.Dark => DarkId,
        _ => SystemId
    };

    public static bool TryParse(string? value, out ThemeMode mode)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case SystemId:
                mode = ThemeMode.System;
                return true;
            case LightId:
                mode = ThemeMode.Light;
                return true;
            case DarkId:
                mode = ThemeMode.Dark;
                return true;
            default:
                mode = ThemeMode.System;
                return false;
        }
    }
}
