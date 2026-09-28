using System.Runtime.InteropServices;

namespace GlazeShell.Windows.Win32;

internal static class Ole32
{
    internal const uint ClsctxInProcServer = 0x00000001;
    internal const uint ClsctxLocalServer = 0x00000004;

    [DllImport("ole32.dll", ExactSpelling = true)]
    internal static extern int CoCreateInstance(
        in Guid classId,
        nint outer,
        uint context,
        in Guid interfaceId,
        out nint instance);

    [DllImport("ole32.dll", ExactSpelling = true)]
    internal static extern int CoInitializeEx(nint reserved, uint flags);

    [DllImport("ole32.dll", ExactSpelling = true)]
    internal static extern void CoUninitialize();

    [DllImport("ole32.dll", ExactSpelling = true)]
    internal static extern void CoTaskMemFree(nint pointer);
}
