using System.Runtime.InteropServices;

namespace GlazeShell.Windows.Win32;

internal static class Kernel32
{
    [DllImport("kernel32.dll", ExactSpelling = true)]
    internal static extern uint GetCurrentThreadId();
}