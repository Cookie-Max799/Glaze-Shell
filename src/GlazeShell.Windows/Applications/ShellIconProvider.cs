using System.Runtime.InteropServices;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Applications;

/// <summary>
/// Пиксельная иконка приложения для плиток dock (функция A1).
/// </summary>
/// <param name="Width">Ширина изображения в пикселях.</param>
/// <param name="Height">Высота изображения в пикселях.</param>
/// <param name="Bgra">Пиксели в формате BGRA, по строкам сверху вниз.</param>
public sealed record IconPixels(int Width, int Height, byte[] Bgra);

/// <summary>
/// Достаёт иконку элемента оболочки (приложения, файла или папки) через официальный
/// shell-интерфейс <c>IShellItemImageFactory</c> и разворачивает её в управляемый буфер BGRA.
/// Провайдер — best-effort: если иконку получить не удалось, возвращается
/// <c>null</c>, а вызывающий показывает запасную плитку с буквой.
/// </summary>
public static class ShellIconProvider
{
    private const int BytesPerPixel = 4;

    public static IconPixels? TryGetIcon(string? path, int size = 64)
    {
        if (size <= 0 || string.IsNullOrWhiteSpace(path) || !(File.Exists(path) || Directory.Exists(path)))
        {
            return null;
        }

        nint itemPointer = 0;
        nint bitmap = 0;
        nint dc = 0;
        Shell32.IShellItemImageFactory? factory = null;

        try
        {
            var interfaceId = new Guid(Shell32.ShellItemImageFactoryId);
            var result = Shell32.SHCreateItemFromParsingName(path, 0, in interfaceId, out itemPointer);

            if (result != 0 || itemPointer == 0)
            {
                return null;
            }

            factory = Marshal.GetObjectForIUnknown(itemPointer) as Shell32.IShellItemImageFactory;

            if (factory is null)
            {
                return null;
            }

            var nativeSize = new Shell32.NativeSize { Width = size, Height = size };
            result = factory.GetImage(nativeSize, Shell32.SiigbfIconOnly | Shell32.SiigbfResizeToFit, out bitmap);

            if (result != 0 || bitmap == 0)
            {
                return null;
            }

            return ReadPixels(bitmap);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return null;
        }
        finally
        {
            if (factory is not null)
            {
                Marshal.ReleaseComObject(factory);
            }

            if (itemPointer != 0)
            {
                Marshal.Release(itemPointer);
            }

            if (bitmap != 0)
            {
                _ = Gdi32.DeleteObject(bitmap);
            }

            if (dc != 0)
            {
                _ = Gdi32.DeleteDC(dc);
            }
        }

        IconPixels? ReadPixels(nint hbitmap)
        {
            var bitmap = default(Gdi32.Bitmap);

            if (Gdi32.GetObject(hbitmap, Marshal.SizeOf<Gdi32.Bitmap>(), ref bitmap) == 0 ||
                bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                return null;
            }

            dc = Gdi32.CreateDC("DISPLAY", null, null, 0);

            if (dc == 0)
            {
                return null;
            }

            var width = bitmap.Width;
            var height = Math.Abs(bitmap.Height);

            if (width > 1024 || height > 1024)
            {
                return null;
            }

            var header = new Gdi32.BitmapInfoHeader
            {
                Size = Gdi32.BitmapInfoHeaderSize,
                Width = width,
                Height = bitmap.Height,
                Planes = 1,
                BitCount = 32,
                Compression = Gdi32.BiRgb,
            };

            var pixels = new byte[checked(width * height * BytesPerPixel)];

            if (Gdi32.GetDIBits(dc, hbitmap, 0, (uint)height, pixels, ref header, Gdi32.DibRgbColors) == 0)
            {
                return null;
            }

            // GDI отдаёт DIB снизу вверх — строки разворачиваются для WinUI, который ждёт сверху вниз.
            if (header.Height > 0)
            {
                FlipVertically(pixels, width, height);
            }

            return new IconPixels(width, height, pixels);
        }
    }

    private static void FlipVertically(byte[] pixels, int width, int height)
    {
        var rowLength = width * BytesPerPixel;
        var buffer = new byte[rowLength];

        for (var row = 0; row < height / 2; row++)
        {
            var top = row * rowLength;
            var bottom = (height - 1 - row) * rowLength;

            Buffer.BlockCopy(pixels, top, buffer, 0, rowLength);
            Buffer.BlockCopy(pixels, bottom, pixels, top, rowLength);
            Buffer.BlockCopy(buffer, 0, pixels, bottom, rowLength);
        }
    }
}
