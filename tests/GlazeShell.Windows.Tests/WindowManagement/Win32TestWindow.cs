using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using GlazeShell.Windows.WindowManagement;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Tests.WindowManagement;

internal sealed class Win32TestWindow : IDisposable
{
    private const uint WsOverlappedWindow = 0x00CF_0000;
    private const uint WsVisible = 0x1000_0000;
    private const uint WmClose = 0x0010;
    private const uint WmNcDestroy = 0x0082;

    private static readonly object RegisterSync = new();
    private static readonly WindowsMessageProc WindowProcedure = OnWindowMessage;
    private static bool _registered;

    private readonly Thread _thread;
    private readonly TaskCompletionSource<nint> _handle = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private uint _threadId;
    private int _disposed;

    internal Win32TestWindow(string title)
    {
        ArgumentNullException.ThrowIfNull(title);

        _thread = new Thread(() => ThreadMain(title))
        {
            IsBackground = true,
            Name = "GlazeShell.TestWindow",
        };
        _thread.Start();

        if (!_handle.Task.Wait(TimeSpan.FromSeconds(5)) || Handle == 0)
        {
            throw new InvalidOperationException("The test window could not be created.");
        }
    }

    internal nint Handle => _handle.Task.IsCompletedSuccessfully ? _handle.Task.Result : 0;

    internal string Id => NativeWindowEnumerator.CreateId(Handle);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (Handle != 0)
        {
            User32.PostMessage(Handle, WmClose, 0, 0);

            var deadline = Stopwatch.StartNew();
            while (User32.IsWindow(Handle) && deadline.ElapsedMilliseconds < 1500)
            {
                Thread.Sleep(15);
            }
        }

        if (_threadId != 0)
        {
            User32.PostThreadMessage(_threadId, User32.WmQuit, 0, 0);
        }

        _thread.Join(TimeSpan.FromSeconds(3));
    }

    internal static void EnsureRegistered()
    {
        if (_registered)
        {
            return;
        }

        lock (RegisterSync)
        {
            if (_registered)
            {
                return;
            }

            var className = "GlazeShellTestWindow" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            var wndClass = new WndClass
            {
                Style = 0,
                LpfnWndProc = WindowProcedure,
                CbClsExtra = 0,
                CbWndExtra = 0,
                HInstance = GetModuleHandle(null),
                HIcon = 0,
                HCursor = 0,
                HbrBackground = 0,
                LpszMenuName = null,
                LpszClassName = className,
            };

            if (RegisterClassW(ref wndClass) == 0)
            {
                throw new InvalidOperationException("RegisterClassW could not register the test window class.");
            }

            _className = className;
            _registered = true;
        }
    }

    private static string? _className;

    private void ThreadMain(string title)
    {
        EnsureRegistered();

        _threadId = Kernel32.GetCurrentThreadId();
        var handle = CreateWindowExW(
            0,
            _className!,
            title,
            WsOverlappedWindow | WsVisible,
            unchecked((int)0x8000_0000),
            unchecked((int)0x8000_0000),
            320,
            200,
            0,
            0,
            GetModuleHandle(null),
            0);

        _handle.TrySetResult(handle);

        if (handle == 0)
        {
            return;
        }

        while (User32.GetMessage(out var message, 0, 0, 0) > 0)
        {
            User32.TranslateMessage(ref message);
            User32.DispatchMessage(ref message);
        }
    }

    private static nint OnWindowMessage(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (message == WmNcDestroy)
        {
            User32.PostQuitMessage(0);
            return 0;
        }

        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClass
    {
        public uint Style;
        public WindowsMessageProc LpfnWndProc;
        public int CbClsExtra;
        public int CbWndExtra;
        public nint HInstance;
        public nint HIcon;
        public nint HCursor;
        public nint HbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? LpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string LpszClassName;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowsMessageProc(nint hwnd, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern ushort RegisterClassW(ref WndClass wndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern nint CreateWindowExW(
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

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = false)]
    private static extern nint DefWindowProc(nint hwnd, uint message, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = false)]
    private static extern nint GetModuleHandle(string? moduleName);
}