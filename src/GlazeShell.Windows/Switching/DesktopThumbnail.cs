using System.Runtime.InteropServices;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Switching;

public sealed class DesktopThumbnail : IDisposable
{
    private nint _handle;

    private DesktopThumbnail(nint handle) => _handle = handle;

    public static DesktopThumbnail? TryCreate(nint destination, nint source, int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0 || DwmRegisterThumbnail(destination, source, out var handle) < 0)
        {
            return null;
        }

        var thumbnail = new DesktopThumbnail(handle);

        if (DwmQueryThumbnailSourceSize(handle, out var size) >= 0 && size.Width > 0 && size.Height > 0)
        {
            var scale = Math.Min((double)width / size.Width, (double)height / size.Height);
            var fittedWidth = Math.Max(1, (int)(size.Width * scale));
            var fittedHeight = Math.Max(1, (int)(size.Height * scale));
            x += (width - fittedWidth) / 2;
            y += (height - fittedHeight) / 2;
            width = fittedWidth;
            height = fittedHeight;
        }

        var properties = new ThumbnailProperties
        {
            Flags = 0x1 | 0x8,
            Destination = new Rect { Left = x, Top = y, Right = x + width, Bottom = y + height },
            Visible = true,
        };

        if (DwmUpdateThumbnailProperties(handle, in properties) >= 0)
        {
            return thumbnail;
        }

        thumbnail.Dispose();
        return null;
    }

    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);

        if (handle != 0)
        {
            _ = DwmUnregisterThumbnail(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ThumbnailProperties
    {
        public uint Flags;
        public Rect Destination;
        public Rect Source;
        public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool SourceClientAreaOnly;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Size
    {
        public int Width;
        public int Height;
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmQueryThumbnailSourceSize(nint thumbnail, out Size size);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmUpdateThumbnailProperties(nint thumbnail, in ThumbnailProperties properties);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmUnregisterThumbnail(nint thumbnail);
}
