using System.Runtime.InteropServices;

namespace GlazeShell.Windows.Win32;

internal static class Kernel32
{
    [DllImport("kernel32.dll", ExactSpelling = true)]
    internal static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint GetModuleHandle(string? moduleName);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern int MultiByteToWideChar(
        uint codePage,
        uint dwFlags,
        byte[] lpMultiByteStr,
        int cbMultiByte,
        [Out] char[]? lpWideCharStr,
        int cchWideChar);

    /// <summary>
    /// Декодирует байты в системной ANSI-кодировке (CP_ACP). На современных системах
    /// с включённым UTF-8 это UTF-8, на старых — локальная кодовая страница, поэтому
    /// используется Win32, а не Encoding.Default фиксированной UTF-8.
    /// </summary>
    internal static string DecodeAnsi(byte[] data)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        const uint Acp = 0;

        var length = MultiByteToWideChar(Acp, 0, data, data.Length, null, 0);

        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new char[length];
        var written = MultiByteToWideChar(Acp, 0, data, data.Length, buffer, length);

        return written <= 0 ? string.Empty : new string(buffer, 0, written);
    }
}