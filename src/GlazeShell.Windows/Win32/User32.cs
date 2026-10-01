using System.Runtime.InteropServices;

namespace GlazeShell.Windows.Win32;

[StructLayout(LayoutKind.Sequential)]
internal struct Point
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Rect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Message
{
    public nint Hwnd;
    public uint Value;
    public nint WParam;
    public nint LParam;
    public uint Time;
    public Point Point;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct MonitorInfoEx
{
    public uint Size;
    public Rect Monitor;
    public Rect WorkArea;
    public uint Flags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DeviceName;
}

[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
internal struct DevModeUnion
{
    [FieldOffset(0)]
    public short Orientation;

    [FieldOffset(2)]
    public short PaperSize;

    [FieldOffset(4)]
    public short PaperLength;

    [FieldOffset(6)]
    public short Scale;

    [FieldOffset(8)]
    public short Copies;

    [FieldOffset(10)]
    public short DefaultSource;

    [FieldOffset(12)]
    public short PrintQuality;

    [FieldOffset(0)]
    public Point Position;

    [FieldOffset(8)]
    public uint DisplayOrientation;

    [FieldOffset(12)]
    public uint DisplayFixedOutput;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DevMode
{
    private const int CchDeviceName = 32;
    private const int CchFormName = 32;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchDeviceName)]
    public string DeviceName;
    public ushort SpecVersion;
    public ushort DriverVersion;
    public ushort Size;
    public ushort DriverExtra;
    public uint Fields;
    public DevModeUnion Union;
    public short Color;
    public short Duplex;
    public short YResolution;
    public short TTOption;
    public short Collate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchFormName)]
    public string FormName;
    public ushort LogPixels;
    public uint BitsPerPixel;
    public uint PelsWidth;
    public uint PelsHeight;
    public uint DisplayFlags;
    public uint DisplayFrequency;
    public uint ICMethod;
    public uint ICIntent;
    public uint MediaType;
    public uint DitherType;
    public uint Reserved1;
    public uint Reserved2;
    public uint PanningWidth;
    public uint PanningHeight;
}

[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate bool MonitorEnumProc(nint monitor, nint deviceContext, ref Rect rect, nint parameter);

[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate nint WindowProc(nint hwnd, uint message, nint wParam, nint lParam);

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WndClass
{
    public uint Style;
    public WindowProc Proc;
    public int ClassExtraBytes;
    public int WindowExtraBytes;
    public nint Instance;
    public nint Icon;
    public nint Cursor;
    public nint Background;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string? MenuName;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string ClassName;
}

[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate bool EnumWindowsProc(nint hwnd, nint parameter);

[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate void WinEventProc(nint hook, uint winEvent, nint hwnd, uint objectId, uint childId, uint eventThread, uint eventTime);

internal static class User32
{
    internal const int GwOwner = 4;

    internal const long GwlStyle = -16;
    internal const long GwlExStyle = -20;

    internal const long WsThickFrame = 0x0004_0000;
    internal const long WsExToolWindow = 0x0000_0080;

    internal const int SwShow = 5;
    internal const int SwMinimize = 6;
    internal const int SwRestore = 9;
    internal const int SwMaximize = 3;

    internal const uint WmClose = 0x0010;
    internal const uint WmQuit = 0x0012;
    internal const uint WmSettingChange = 0x001A;
    internal const uint WmDeviceChange = 0x0219;
    internal const uint WmDpiChanged = 0x02E0;

    internal const uint SpiSetWorkArea = 0x005B;
    internal const uint SpiSetLogicalDpiOverride = 0x009F;

    internal const uint DevNodesChanged = 0x0007;

    internal const uint SwpNoSize = 0x0001;
    internal const uint SwpNoMove = 0x0002;
    internal const uint SwpShowWindow = 0x0040;

    internal const uint EventSystemForeground = 0x0003;
    internal const uint EventObjectCreate = 0x8000;
    internal const uint EventObjectDestroy = 0x8001;
    internal const uint EventObjectShow = 0x8002;
    internal const uint EventObjectHide = 0x8003;
    internal const uint EventSystemMinimizestart = 0x0016;
    internal const uint EventSystemMinimizeend = 0x0017;
    internal const uint EventObjectCloaked = 0x8016;
    internal const uint EventObjectUncloaked = 0x8017;

    internal const uint WineventOutOfContext = 0x0000;
    internal const uint WineventSkipOwnProcess = 0x0002;

    internal const uint WmDisplayChange = 0x007E;

    internal const uint MonitorInfoFPrimary = 0x0000_0001;

    internal const uint EnumCurrentSettings = unchecked((uint)-1);

    internal const uint DmDisplayOrientation = 0x0000_0080;

    internal const uint DisplayOrientationDefault = 0x0000_0000;
    internal const uint DisplayOrientation90 = 0x0000_0001;
    internal const uint DisplayOrientation180 = 0x0000_0002;
    internal const uint DisplayOrientation270 = 0x0000_0003;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(nint hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsZoomed(nint hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindowAsync(nint hwnd, int command);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(nint hwnd, char[] buffer, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(nint hwnd, char[] buffer, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint hwnd, out Rect rect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern long GetWindowLongPtr(nint hwnd, long index);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint GetWindow(nint hwnd, int command);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(nint hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(nint hwnd, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AttachThreadInput(uint attachTo, uint attachFrom, [MarshalAs(UnmanagedType.Bool)] bool attach);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint module, WinEventProc callback, uint processId, uint threadId, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWinEvent(nint hook);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetMessage(out Message message, nint hwnd, uint filterMin, uint filterMax);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TranslateMessage(ref Message message);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint DispatchMessage(ref Message message);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostThreadMessage(uint threadId, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern void PostQuitMessage(int exitCode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(nint deviceContext, nint clip, MonitorEnumProc callback, nint parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfoEx info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint MonitorFromWindow(nint hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint MonitorFromPoint(Point point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplaySettingsW(string deviceName, uint modeIndex, ref DevMode mode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetDpiForSystem();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern uint GetDpiForWindow(nint hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern ushort RegisterClassW(ref WndClass wndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern nint CreateWindowExW(
        uint exStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint DefWindowProc(nint hwnd, uint message, nint wParam, nint lParam);
}