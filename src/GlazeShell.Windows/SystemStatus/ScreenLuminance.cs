using GlazeShell.Core.Theming;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.SystemStatus;

/// <summary>
/// Средняя яркость того, что видно за стеклянной панелью. Панели Glaze Shell действительно
/// прозрачные, и подпись на них читается только тогда, когда её цвет противоположен фону.
/// Фоном считается не рабочий стол сам по себе, а то, что реально оказалось под панелью:
/// обои, окно приложения, браузер — что угодно.
/// </summary>
/// <remarks>
/// Пиксели читаются с экрана только по запросу и небольшим шагом: панель занимает узкую
/// полосу или небольшой popover, поэтому достаточно нескольких десятков отсчётов. Значение
/// не кэшируется: содержимое под панелью меняется при каждом переключении окна.
/// </remarks>
public static class ScreenLuminance
{
    /// <summary>
    /// Шаг сетки отсчётов в пикселях. Панель шириной в экран читается по ~40 точкам —
    /// этого достаточно для средней яркости и не стоит заметного времени.
    /// </summary>
    private const int SampleStep = 48;

    /// <summary>
    /// Относительная яркость (0–1) прямоугольника экрана или <see langword="null"/>,
    /// если прочитать пиксели не удалось.
    /// </summary>
    /// <param name="x">Левый край в физических пикселях.</param>
    /// <param name="y">Верхний край в физических пикселях.</param>
    /// <param name="width">Ширина в физических пикселях.</param>
    /// <param name="height">Высота в физических пикселях.</param>
    public static double? Read(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        nint screen = 0;
        nint memory = 0;
        nint bitmap = 0;
        nint previous = 0;

        try
        {
            // Размер копии ограничивается заранее: панель во всю ширину экрана при
            // 4K укладывается в разумный буфер, а случайно огромный прямоугольник
            // не приведёт к выделению сотен мегабайт.
            var sampleWidth = Math.Min(width, 4096);
            var sampleHeight = Math.Min(height, 4096);

            screen = Gdi32.GetDC(0);

            if (screen == 0)
            {
                return null;
            }

            memory = Gdi32.CreateCompatibleDC(screen);
            bitmap = Gdi32.CreateCompatibleBitmap(screen, sampleWidth, sampleHeight);

            if (memory == 0 || bitmap == 0)
            {
                return null;
            }

            previous = Gdi32.SelectObject(memory, bitmap);

            if (!Gdi32.BitBlt(memory, 0, 0, sampleWidth, sampleHeight, screen, x, y, Gdi32.SrcCopy))
            {
                return null;
            }

            var header = new Gdi32.BitmapInfoHeader
            {
                Size = Gdi32.BitmapInfoHeaderSize,
                Width = sampleWidth,

                // Отрицательная высота — строки сверху вниз, как в изображении.
                Height = -sampleHeight,
                Planes = 1,
                BitCount = 32,
                Compression = Gdi32.BiRgb,
            };

            var pixels = new byte[sampleWidth * sampleHeight * 4];

            if (Gdi32.GetDIBits(memory, bitmap, 0, (uint)sampleHeight, pixels, ref header, Gdi32.DibRgbColors) == 0)
            {
                return null;
            }

            return Average(pixels, sampleWidth, sampleHeight);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return null;
        }
        finally
        {
            if (memory != 0 && previous != 0)
            {
                _ = Gdi32.SelectObject(memory, previous);
            }

            if (bitmap != 0)
            {
                _ = Gdi32.DeleteObject(bitmap);
            }

            if (memory != 0)
            {
                _ = Gdi32.DeleteDC(memory);
            }

            if (screen != 0)
            {
                _ = Gdi32.ReleaseDC(0, screen);
            }
        }
    }

    /// <summary>
    /// Средняя яркость по сетке отсчётов: обход каждого пикселя копии стоил бы заметного
    /// времени на панели во всю ширину экрана, а для выбора светлых или тёмных подписей
    /// достаточно равномерной выборки.
    /// </summary>
    private static double Average(byte[] pixels, int width, int height)
    {
        var total = 0.0;
        var count = 0;

        for (var row = 0; row < height; row += SampleStep)
        {
            for (var column = 0; column < width; column += SampleStep)
            {
                var offset = ((row * width) + column) * 4;
                total += new GlazeColor(pixels[offset + 2], pixels[offset + 1], pixels[offset]).RelativeLuminance;
                count++;
            }
        }

        return count == 0 ? 0 : total / count;
    }
}
