using System.Globalization;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.SystemStatus;

public static class KeyboardLayoutProvider
{
    public static CultureInfo? GetForegroundCulture()
    {
        var hwnd = User32.GetForegroundWindow();
        if (hwnd == 0)
        {
            return null;
        }

        var thread = User32.GetWindowThreadProcessId(hwnd, out _);
        if (thread == 0)
        {
            return null;
        }

        var layout = User32.GetKeyboardLayout(thread);
        try
        {
            return layout == 0 ? null : CultureInfo.GetCultureInfo((int)(layout.ToInt64() & 0xFFFF));
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
