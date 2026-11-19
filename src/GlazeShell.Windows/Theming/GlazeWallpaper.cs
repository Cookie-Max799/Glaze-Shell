using GlazeShell.Core.Theming;
using GlazeShell.Windows.Win32;
using Microsoft.Win32;

namespace GlazeShell.Windows.Theming;

/// <summary>
/// Обои рабочего стола в оформлении Glaze: плавный градиент из основного цвета и
/// акцента. Картинка собирается один раз на смену темы, сохраняется в файл и
/// ставится системным вызовом, без сторонних библиотек рисования.
/// </summary>
/// <remarks>
/// <para>
/// Формат — несжатый BMP: его принимает <c>SystemParametersInfo(SPI_SETDESKWALLPAPER)</c>
/// на любой версии Windows, а запись 24-битных строк стоит считанные миллисекунды.
/// </para>
/// <para>
/// Пользовательские обои не теряются: путь и режим растяжения запоминаются до первой
/// подмены и возвращаются, когда оформление выключают или оболочка завершается.
/// </para>
/// </remarks>
public sealed class GlazeWallpaper
{
    private const int DefaultWidth = 1920;
    private const int DefaultHeight = 1080;
    private const int MaxWidth = 3840;
    private const int MaxHeight = 2400;

    private const string DesktopKey = @"Control Panel\Desktop";

    private readonly string _directory;
    private readonly string _snapshotPath;

    public GlazeWallpaper(string directory, string snapshotPath)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("A wallpaper directory is required.", nameof(directory));
        }

        if (string.IsNullOrWhiteSpace(snapshotPath))
        {
            throw new ArgumentException("A snapshot path is required.", nameof(snapshotPath));
        }

        _directory = directory;
        _snapshotPath = snapshotPath;
    }

    /// <summary>Путь к текущим обоям Glaze или <c>null</c>, если они ещё не созданы.</summary>
    public string? CurrentPath { get; private set; }

    /// <summary>
    /// Собирает и ставит обои по теме. Возвращает путь к файлу или <c>null</c>, если
    /// собрать не удалось. Повторный вызов с теми же цветами переиспользует файл.
    /// </summary>
    public string? Apply(GlazeColor baseColor, GlazeColor accent)
    {
        try
        {
            EnsureSnapshot();

            var (width, height) = ScreenSize();
            var path = Path.Combine(_directory, $"wallpaper-{BuildKey(baseColor, accent, width, height)}.bmp");

            if (!File.Exists(path))
            {
                Directory.CreateDirectory(_directory);
                WriteGradient(path, width, height, baseColor, accent);
            }

            using (var desktop = Registry.CurrentUser.CreateSubKey(DesktopKey, writable: true))
            {
                // Fill: картинка масштабируется под экран без полей. Плитка и растяжение
                // оставили бы полосы на мониторах другой пропорции.
                desktop?.SetValue("WallpaperStyle", "10", RegistryValueKind.String);
                desktop?.SetValue("TileWallpaper", "0", RegistryValueKind.String);
            }

            if (!User32.SystemParametersInfoW(
                User32.SpiSetDesktopWallpaper,
                0,
                path,
                User32.SpifUpdateIniFile | User32.SpifSendChange))
            {
                return null;
            }

            CurrentPath = path;
            return path;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// Возвращает исходные обои и режим растяжения. Безопасно вызывать повторно и когда
    /// снимка нет. После успешного восстановления снимок удаляется.
    /// </summary>
    public void Restore()
    {
        try
        {
            var snapshot = LoadSnapshot();

            if (snapshot is null)
            {
                return;
            }

            if (snapshot.Path is not null)
            {
                // Пустая строка — штатный способ сказать Windows «файла обоев нет»:
                // тогда показывается исходный сплошной цвет рабочего стола. Без этого
                // градиент Glaze оставался бы на экране после выключения.
                _ = User32.SystemParametersInfoW(
                    User32.SpiSetDesktopWallpaper,
                    0,
                    snapshot.Path,
                    User32.SpifUpdateIniFile | User32.SpifSendChange);
            }

            using (var desktop = Registry.CurrentUser.CreateSubKey(DesktopKey, writable: true))
            {
                if (snapshot.Style is not null)
                {
                    desktop?.SetValue("WallpaperStyle", snapshot.Style, RegistryValueKind.String);
                }

                if (snapshot.Tile is not null)
                {
                    desktop?.SetValue("TileWallpaper", snapshot.Tile, RegistryValueKind.String);
                }
            }

            CurrentPath = null;
            DeleteSnapshot();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            // Не удалось вернуть обои: файл снимка остаётся, повторная попытка — при
            // следующем запуске или выключении оформления.
        }
    }

    /// <summary>Снимок исходных обоев и режима растяжения.</summary>
    private sealed record WallpaperSnapshot(string? Path, string? Style, string? Tile);

    private void EnsureSnapshot()
    {
        if (File.Exists(_snapshotPath))
        {
            return;
        }

        var path = ReadWallpaperPath();

        string? style = null;
        string? tile = null;

        using (var desktop = Registry.CurrentUser.OpenSubKey(DesktopKey))
        {
            style = desktop?.GetValue("WallpaperStyle")?.ToString();
            tile = desktop?.GetValue("TileWallpaper")?.ToString();
        }

        SaveSnapshot(new WallpaperSnapshot(path, style, tile));
    }

    private static string? ReadWallpaperPath()
    {
        var buffer = new char[520];

        if (!User32.SystemParametersInfoW(User32.SpiGetDesktopWallpaper, (uint)buffer.Length, buffer, 0))
        {
            // Не удалось прочитать: путь неизвестен, обои при восстановлении лучше не трогать.
            return null;
        }

        var end = Array.IndexOf(buffer, '\0');

        // Пустая строка — это не сбой, а сплошной цвет: Windows сообщает, что файла нет.
        // Такой снимок восстановит исходный цвет, если снять обои Glaze.
        return end <= 0 ? string.Empty : new string(buffer, 0, end);
    }

    private WallpaperSnapshot? LoadSnapshot()
    {
        try
        {
            return File.Exists(_snapshotPath)
                ? System.Text.Json.JsonSerializer.Deserialize<WallpaperSnapshot>(File.ReadAllText(_snapshotPath))
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private void SaveSnapshot(WallpaperSnapshot snapshot)
    {
        try
        {
            var directory = Path.GetDirectoryName(_snapshotPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_snapshotPath, System.Text.Json.JsonSerializer.Serialize(snapshot));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Снимок не сохранён: обои применятся, но вернуть исходные будет нельзя.
        }
    }

    private void DeleteSnapshot()
    {
        try
        {
            if (File.Exists(_snapshotPath))
            {
                File.Delete(_snapshotPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Файл останется: следующий запуск прочитает его и повторит восстановление.
        }
    }

    /// <summary>Удаляет созданные файлы обоев, кроме текущего (его может держать система).</summary>
    public void Cleanup()
    {
        try
        {
            if (!Directory.Exists(_directory))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(_directory, "wallpaper-*.bmp"))
            {
                if (string.Equals(file, CurrentPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Delete(file);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Оставшиеся файлы не мешают: следующий запуск перезапишет или переиспользует их.
        }
    }

    private static (int Width, int Height) ScreenSize()
    {
        var width = User32.GetSystemMetrics(0);
        var height = User32.GetSystemMetrics(1);

        if (width <= 0 || height <= 0)
        {
            return (DefaultWidth, DefaultHeight);
        }

        return (Math.Min(width, MaxWidth), Math.Min(height, MaxHeight));
    }

    /// <summary>
    /// Ключ файла: цвета и размер экрана. Пока они те же, картинка не пересобирается,
    /// поэтому переключение настроек не создаёт файл заново.
    /// </summary>
    private static string BuildKey(GlazeColor baseColor, GlazeColor accent, int width, int height) =>
        $"{baseColor.R:X2}{baseColor.G:X2}{baseColor.B:X2}-{accent.R:X2}{accent.G:X2}{accent.B:X2}-{width}x{height}";

    /// <summary>
    /// Рисует вертикальный градиент «основной цвет сверху — акцент снизу» с мягким
    /// диагональным бликом. Блик даёт объём и не мешает значкам рабочего стола.
    /// </summary>
    private static void WriteGradient(string path, int width, int height, GlazeColor top, GlazeColor bottom)
    {
        // Строка BMP кратна четырём байтам: иначе изображение поедет по диагонали.
        var rowBytes = ((width * 3) + 3) & ~3;
        var pixelBytes = rowBytes * height;
        var fileSize = 54 + pixelBytes;

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16);
        using var writer = new BinaryWriter(stream);

        // Заголовок файла.
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write(0);
        writer.Write(54);

        // Заголовок изображения (BITMAPINFOHEADER).
        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write((short)24);
        writer.Write(0);
        writer.Write(pixelBytes);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);

        var row = new byte[rowBytes];

        // BMP хранит строки снизу вверх: нижняя строка файла — верх изображения.
        for (var y = height - 1; y >= 0; y--)
        {
            var vertical = 1.0 - ((double)y / Math.Max(1, height - 1));
            var color = GlazePalette.Mix(top, bottom, vertical);
            var glow = Math.Max(0, 1 - Math.Abs(vertical - 0.35) * 3) * 0.12;

            for (var x = 0; x < width; x++)
            {
                var offset = x * 3;
                var lighten = glow * (1 - ((double)x / Math.Max(1, width - 1)));
                row[offset] = MixChannel(color.B, lighten);
                row[offset + 1] = MixChannel(color.G, lighten);
                row[offset + 2] = MixChannel(color.R, lighten);
            }

            writer.Write(row, 0, rowBytes);
        }
    }

    private static byte MixChannel(byte channel, double amount) =>
        (byte)Math.Clamp(channel + ((255 - channel) * amount), 0, 255);
}
