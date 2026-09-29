using System.Globalization;
using GlazeShell.Core.Models;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.DisplayManagement;

internal static class NativeMonitorEnumerator
{
    private const int MonitorInfoExSize = 104;
    private const uint MonitorDefaultToNull = 0;

    internal static IReadOnlyList<MonitorInfo> Enumerate()
    {
        var monitors = new List<MonitorInfo>();

        bool Collect(nint hmonitor, nint _, ref Rect rect, nint __)
        {
            if (Read(hmonitor) is { } monitor)
            {
                monitors.Add(monitor);
            }

            return true;
        }

        User32.EnumDisplayMonitors(0, 0, Collect, 0);
        return monitors;
    }

    internal static MonitorInfo? Read(nint hmonitor)
    {
        if (hmonitor == 0)
        {
            return null;
        }

        var info = new MonitorInfoEx
        {
            Size = (uint)MonitorInfoExSize,
        };

        if (!User32.GetMonitorInfoW(hmonitor, ref info))
        {
            return null;
        }

        var deviceName = string.IsNullOrWhiteSpace(info.DeviceName)
            ? CreateId(hmonitor)
            : info.DeviceName;

        var dpi = ReadDpi(hmonitor);
        var mode = ReadCurrentMode(info.DeviceName);
        var isPrimary = (info.Flags & User32.MonitorInfoFPrimary) != 0;

        return new MonitorInfo(
            id: CreateId(hmonitor),
            deviceName: deviceName,
            bounds: ToBounds(info.Monitor),
            workingArea: ToBoundsOrNull(info.WorkArea),
            scaleFactor: dpi is { } dpiValue ? Shcore.ToScaleFactor(dpiValue) : 1.0,
            isPrimary: isPrimary,
            isConnected: true,
            refreshRateHz: mode?.DisplayFrequency is > 0 ? (int)mode.Value.DisplayFrequency : null,
            orientation: mode is null ? MonitorOrientation.None : ToOrientation(mode.Value.Union.DisplayOrientation));
    }

    internal static MonitorInfo? GetMonitorForWindow(string windowId)
    {
        if (!TryParseId(windowId, out var hwnd))
        {
            return null;
        }

        var hmonitor = User32.MonitorFromWindow(hwnd, MonitorDefaultToNull);
        return hmonitor == 0 ? null : Read(hmonitor);
    }

    internal static MonitorInfo? GetPrimaryMonitor()
    {
        var primary = Enumerate().FirstOrDefault(monitor => monitor.IsPrimary);
        return primary;
    }

    internal static MonitorInfo? GetMonitorForPoint(int x, int y)
    {
        var hmonitor = User32.MonitorFromPoint(new Point { X = x, Y = y }, MonitorDefaultToNull);
        return hmonitor == 0 ? null : Read(hmonitor);
    }

    internal static string CreateId(nint hmonitor) =>
        string.Create(CultureInfo.InvariantCulture, $"{hmonitor:X}");

    internal static bool TryParseId(string monitorId, out nint hmonitor)
    {
        hmonitor = 0;

        if (string.IsNullOrWhiteSpace(monitorId) || !ulong.TryParse(monitorId, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        hmonitor = unchecked((nint)value);
        return hmonitor != 0;
    }

    private static int? ReadDpi(nint hmonitor)
    {
        try
        {
            if (Shcore.GetDpiForMonitor(hmonitor, Shcore.MdTEffectiveDpi, out var dpiX, out _) == 0 && dpiX > 0)
            {
                return (int)dpiX;
            }
        }
        catch (DllNotFoundException)
        {
            return GetSystemDpi();
        }
        catch (EntryPointNotFoundException)
        {
            return GetSystemDpi();
        }

        return GetSystemDpi();
    }

    private static int GetSystemDpi() => User32.GetDpiForSystem();

    private static DevMode? ReadCurrentMode(string deviceName)
    {
        var mode = new DevMode
        {
            Size = (ushort)System.Runtime.InteropServices.Marshal.SizeOf<DevMode>(),
        };

        if (!User32.EnumDisplaySettingsW(deviceName, User32.EnumCurrentSettings, ref mode))
        {
            return null;
        }

        return mode;
    }

    private static MonitorBounds ToBounds(Rect rect)
    {
        var width = Math.Max(1, rect.Right - rect.Left);
        var height = Math.Max(1, rect.Bottom - rect.Top);
        return new MonitorBounds(rect.Left, rect.Top, width, height);
    }

    private static MonitorBounds? ToBoundsOrNull(Rect rect)
    {
        if (rect.Right <= rect.Left || rect.Bottom <= rect.Top)
        {
            return null;
        }

        return ToBounds(rect);
    }

    private static MonitorOrientation ToOrientation(uint orientation) => orientation switch
    {
        User32.DisplayOrientationDefault => MonitorOrientation.Landscape,
        User32.DisplayOrientation90 => MonitorOrientation.Portrait,
        User32.DisplayOrientation180 => MonitorOrientation.LandscapeFlipped,
        User32.DisplayOrientation270 => MonitorOrientation.PortraitFlipped,
        _ => MonitorOrientation.None,
    };
}