using System.Runtime.InteropServices;

namespace GlazeShell.Windows.Win32;

internal static class Dwmapi
{
    internal const uint DwmwaCloaked = 14;

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    internal static extern int DwmGetWindowAttribute(nint hwnd, uint attribute, out int value, int size);
}