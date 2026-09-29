using System.Runtime.InteropServices;

namespace GlazeShell.Windows.Win32;

internal static class Shcore
{
    internal const int ProcessPerMonitorDpiAware = 2;
    internal const int MdTEffectiveDpi = 0;

    internal static double ToScaleFactor(int dpi) => dpi / 96.0;

    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);
}