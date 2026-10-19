using System.Runtime.InteropServices;

namespace GlazeShell.Windows.Win32;

/// <summary>
/// GDI-функции, нужные для получения пикселей иконки приложения (dock, функция A1):
/// <c>GetDIBits</c> копирует содержимое HBITMAP в управляемый буфер в формате BGRA.
/// </summary>
internal static class Gdi32
{
    internal const uint DibRgbColors = 0;
    internal const uint BiRgb = 0;

    /// <summary>Размер <see cref="BitmapInfoHeader"/> (BITMAPINFOHEADER).</summary>
    internal const uint BitmapInfoHeaderSize = 40;

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint ImageSize;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    /// <summary>BITMAP: размеры и формат HBITMAP (заполняется <see cref="GetObject"/>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Bitmap
    {
        public int Type;
        public int Width;
        public int Height;
        public int WidthBytes;
        public ushort Planes;
        public ushort BitCount;
        public nint Bits;
    }

    [DllImport("gdi32.dll", EntryPoint = "GetObjectW", ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal static extern int GetObject(nint handle, int size, ref Bitmap bitmap);

    [DllImport("gdi32.dll", EntryPoint = "CreateDCW", ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal static extern nint CreateDC(string? driverName, string? deviceName, string? output, nint initData);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    internal static extern bool DeleteDC(nint hdc);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    internal static extern bool DeleteObject(nint handle);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    internal static extern int GetDIBits(
        nint hdc,
        nint bitmap,
        uint startScan,
        uint scanLines,
        byte[]? bits,
        ref BitmapInfoHeader bitmapInfoHeader,
        uint usage);
}
