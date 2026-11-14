using GlazeShell.Core.Theming;
using GlazeShell.Windows.Applications;
using GlazeShell.Windows.Win32;
using Microsoft.Win32;

namespace GlazeShell.Windows.SystemStatus;

/// <summary>
/// Средняя светлота обоев рабочего стола. Панели Glaze Shell действительно прозрачные
/// и наследуют яркость рабочего стола, поэтому на светлых обоях светлые подписи не
/// читаются. По этому значению тема выбирает, каким цветом писать текст на стекле.
/// </summary>
/// <remarks>
/// Измерение не читает экран: берётся путь обоев из системной настройки и его
/// уменьшенная миниатюра через оболочку. Результат кэшируется по пути и времени
/// изменения файла, поэтому повторные применения темы ничего не стоят.
/// </remarks>
public static class DesktopWallpaper
{
    private const string ColorsKey = @"HKEY_CURRENT_USER\Control Panel\Colors";

    private static string? _cachedKey;
    private static double? _cachedLuminance;

    /// <summary>Относительная яркость обоев (0–1) или <c>null</c>, если измерить не удалось.</summary>
    public static double? Luminance
    {
        get
        {
            var wallpaper = ReadWallpaperPath();
            var key = BuildCacheKey(wallpaper);

            if (!string.Equals(key, _cachedKey, StringComparison.Ordinal))
            {
                _cachedLuminance = Measure(wallpaper);
                _cachedKey = key;
            }

            return _cachedLuminance;
        }
    }

    private static double? Measure(string? wallpaper)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(wallpaper) && File.Exists(wallpaper))
            {
                var thumbnail = ShellIconProvider.TryGetThumbnail(wallpaper, 16);

                if (thumbnail is { Width: > 0, Height: > 0 })
                {
                    return AverageLuminance(thumbnail);
                }
            }

            return ReadSolidColor();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return null;
        }
    }

    private static string BuildCacheKey(string? wallpaper)
    {
        if (!string.IsNullOrWhiteSpace(wallpaper))
        {
            try
            {
                return $"{wallpaper}|{File.GetLastWriteTimeUtc(wallpaper).Ticks}";
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return wallpaper;
            }
        }

        return Registry.GetValue(ColorsKey, "Background", string.Empty)?.ToString() ?? string.Empty;
    }

    private static string? ReadWallpaperPath()
    {
        var buffer = new char[520];

        if (!User32.SystemParametersInfoW(User32.SpiGetDesktopWallpaper, (uint)buffer.Length, buffer, 0))
        {
            return null;
        }

        var end = Array.IndexOf(buffer, '\0');

        return end <= 0 ? null : new string(buffer, 0, end);
    }

    private static double? ReadSolidColor()
    {
        if (Registry.GetValue(ColorsKey, "Background", null) is not string value)
        {
            return null;
        }

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 3
            || !byte.TryParse(parts[0], out var red)
            || !byte.TryParse(parts[1], out var green)
            || !byte.TryParse(parts[2], out var blue))
        {
            return null;
        }

        return new GlazeColor(red, green, blue).RelativeLuminance;
    }

    private static double AverageLuminance(IconPixels pixels)
    {
        var total = 0.0;
        var count = 0;

        for (var offset = 0; offset + 3 < pixels.Bgra.Length; offset += 4)
        {
            total += new GlazeColor(pixels.Bgra[offset + 2], pixels.Bgra[offset + 1], pixels.Bgra[offset])
                .RelativeLuminance;
            count++;
        }

        return count == 0 ? 0 : total / count;
    }
}
