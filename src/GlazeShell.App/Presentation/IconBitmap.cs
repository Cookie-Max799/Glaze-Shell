using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using GlazeShell.Windows.Applications;
using Microsoft.UI.Xaml.Media.Imaging;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Превращает пиксели иконки оболочки в источник изображения WinUI. Общий помощник dock (A1)
/// и Explorer (E1): извлечение иконки идёт в фоне, а <c>WriteableBitmap</c> обязан создаваться
/// на потоке интерфейса, поэтому создание вынесено в отдельный метод.
/// </summary>
internal static class IconBitmap
{
    internal static WriteableBitmap? Create(IconPixels pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);

        try
        {
            var bitmap = new WriteableBitmap(pixels.Width, pixels.Height);

            using (var stream = bitmap.PixelBuffer.AsStream())
            {
                stream.Write(Premultiply(pixels), 0, pixels.Bgra.Length);
            }

            bitmap.Invalidate();
            return bitmap;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.IconBitmap: an icon image could not be created. {exception}");
            return null;
        }
    }

    /// <summary>
    /// Переводит пиксели из straight alpha в premultiplied: <c>WriteableBitmap</c> в WinUI
    /// ожидает BGRA с уже умноженным на альфу цветом, а <c>IShellItemImageFactory</c>
    /// отдаёт цвет без умножения. Без этого полупрозрачные края значков светятся
    /// (светлые «звёзды» и рваная кромка поверх тёмной панели).
    /// </summary>
    private static byte[] Premultiply(IconPixels pixels)
    {
        var result = (byte[])pixels.Bgra.Clone();
        var radius = Math.Min(pixels.Width, pixels.Height) * 0.22;

        for (var i = 0; i < result.Length; i += 4)
        {
            var x = (i / 4) % pixels.Width + 0.5;
            var y = (i / 4) / pixels.Width + 0.5;
            var dx = Math.Max(Math.Max(radius - x, x - (pixels.Width - radius)), 0);
            var dy = Math.Max(Math.Max(radius - y, y - (pixels.Height - radius)), 0);
            var coverage = Math.Clamp(radius + 0.5 - Math.Sqrt(dx * dx + dy * dy), 0, 1);
            var alpha = (byte)Math.Round(result[i + 3] * coverage);
            result[i + 3] = alpha;

            if (alpha == 255)
            {
                continue;
            }

            if (alpha == 0)
            {
                result[i] = 0;
                result[i + 1] = 0;
                result[i + 2] = 0;
                continue;
            }

            result[i] = (byte)((result[i] * alpha + 127) / 255);
            result[i + 1] = (byte)((result[i + 1] * alpha + 127) / 255);
            result[i + 2] = (byte)((result[i + 2] * alpha + 127) / 255);
        }

        return result;
    }
}
