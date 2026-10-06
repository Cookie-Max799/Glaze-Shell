using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace GlazeShell.App.SingleInstance;

internal static class SingleInstanceHelper
{
    private const string MutexName = "GlazeShell.SingleInstance.v1";

    internal static bool IsFirstInstance()
    {
        using var mutex = new Mutex(true, MutexName, out bool created);
        return created;
    }

    internal static void ActivateExistingInstance()
    {
        var hwnd = FindWindowByClassName("GlazeShell.MainWindow");
        if (hwnd == 0)
        {
            return;
        }

        if (!IsIconic(hwnd))
        {
            SetForegroundWindow(hwnd);
            return;
        }

        ShowWindow(hwnd, 9);
        SetForegroundWindow(hwnd);
    }

    private static nint FindWindowByClassName(string className)
    {
        nint found = 0;
        EnumWindows((hwnd, lParam) =>
        {
            var buffer = new char[256];
            var length = GetClassName(hwnd, buffer, buffer.Length);
            if (length > 0)
            {
                var name = new string(buffer, 0, length);
                if (string.Equals(name, className, StringComparison.OrdinalIgnoreCase))
                {
                    found = hwnd;
                    return false;
                }
            }
            return true;
        }, 0);
        return found;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hwnd, char[] buffer, int maxCount);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hwnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hwnd, int command);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint hwnd);

    private delegate bool EnumWindowsProc(nint hwnd, nint parameter);
}
