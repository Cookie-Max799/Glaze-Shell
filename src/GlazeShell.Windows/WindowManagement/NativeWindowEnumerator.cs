using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using GlazeShell.Core.Models;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.WindowManagement;

internal static class NativeWindowEnumerator
{
    private const int MaxWindowText = 512;
    private const int MaxClassName = 256;

    internal static IReadOnlyList<WindowInfo> Enumerate()
    {
        var windows = new List<WindowInfo>(64);

        if (!User32.EnumWindows(Collect, 0))
        {
            return windows;
        }

        return windows;

        bool Collect(nint hwnd, nint _)
        {
            if (!IsListed(hwnd))
            {
                return true;
            }

            if (Read(hwnd, includeProcess: true) is { } window)
            {
                windows.Add(window);
            }

            return true;
        }
    }

    internal static WindowInfo? Read(nint hwnd, bool includeProcess)
    {
        if (hwnd == 0 || !User32.IsWindow(hwnd))
        {
            return null;
        }

        var processId = GetProcessId(hwnd);
        var title = GetText(hwnd);
        var className = GetClassName(hwnd);

        return new WindowInfo(
            id: CreateId(hwnd),
            title: string.IsNullOrWhiteSpace(title) ? className : title,
            processId: processId,
            processName: includeProcess ? GetProcessName(processId) : null,
            executablePath: includeProcess ? GetExecutablePath(processId) : null,
            type: WindowType.Application,
            state: ReadState(hwnd),
            isForeground: User32.GetForegroundWindow() == hwnd,
            ownerId: GetOwnerId(hwnd),
            canResize: (User32.GetWindowLongPtr(hwnd, User32.GwlStyle) & User32.WsThickFrame) != 0);
    }

    internal static string CreateId(nint hwnd) =>
        string.Create(CultureInfo.InvariantCulture, $"{hwnd:X}");

    internal static bool TryParseId(string windowId, out nint hwnd)
    {
        hwnd = 0;

        if (string.IsNullOrWhiteSpace(windowId) || !ulong.TryParse(windowId, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        hwnd = unchecked((nint)value);
        return hwnd != 0;
    }

    private static bool IsListed(nint hwnd)
    {
        if (!User32.IsWindowVisible(hwnd))
        {
            return false;
        }

        if (IsCloaked(hwnd))
        {
            return false;
        }

        if ((User32.GetWindowLongPtr(hwnd, User32.GwlExStyle) & User32.WsExToolWindow) != 0)
        {
            return false;
        }

        return true;
    }

    private static bool IsCloaked(nint hwnd)
    {
        if (Dwmapi.DwmGetWindowAttribute(hwnd, Dwmapi.DwmwaCloaked, out var value, sizeof(int)) < 0)
        {
            return false;
        }

        return value != 0;
    }

    private static int GetProcessId(nint hwnd)
    {
        _ = User32.GetWindowThreadProcessId(hwnd, out var processId);
        return unchecked((int)processId);
    }

    private static string GetText(nint hwnd)
    {
        var buffer = new char[MaxWindowText];
        var length = User32.GetWindowText(hwnd, buffer, buffer.Length);

        if (length <= 0)
        {
            return string.Empty;
        }

        return new string(buffer, 0, Math.Min(length, buffer.Length)).Trim();
    }

    private static string GetClassName(nint hwnd)
    {
        var buffer = new char[MaxClassName];
        var length = User32.GetClassName(hwnd, buffer, buffer.Length);

        if (length <= 0)
        {
            return "(безымянное окно)";
        }

        return new string(buffer, 0, Math.Min(length, buffer.Length)).Trim();
    }

    private static string? GetOwnerId(nint hwnd)
    {
        var owner = User32.GetWindow(hwnd, User32.GwOwner);
        return owner == 0 ? null : CreateId(owner);
    }

    private static WindowState ReadState(nint hwnd)
    {
        if (User32.IsIconic(hwnd))
        {
            return WindowState.Minimized;
        }

        if (User32.IsZoomed(hwnd))
        {
            return WindowState.Maximized;
        }

        return WindowState.Normal;
    }

    private static string? GetProcessName(int processId)
    {
        if (processId <= 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return null;
        }
    }

    private static string? GetExecutablePath(int processId)
    {
        if (processId <= 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            return process.MainModule?.FileName;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return null;
        }
    }
}