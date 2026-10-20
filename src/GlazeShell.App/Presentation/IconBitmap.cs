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
                stream.Write(pixels.Bgra, 0, pixels.Bgra.Length);
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
}
