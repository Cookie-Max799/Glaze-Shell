using System.Runtime.InteropServices;

namespace GlazeShell.Windows.Win32;

internal static class Dwmapi
{
    internal const uint DwmwaCloaked = 14;
    internal const uint DwmwaWindowCornerPreference = 33;

    internal const int DwmcpDefault = 0;
    internal const int DwmcpDoNotRound = 1;
    internal const int DwmcpRound = 2;
    internal const int DwmcpRoundSmall = 3;

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    internal static extern int DwmGetWindowAttribute(nint hwnd, uint attribute, out int value, int size);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    internal static extern int DwmSetWindowAttribute(nint hwnd, uint attribute, ref int value, int size);
}