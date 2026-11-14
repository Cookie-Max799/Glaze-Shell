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

/// <summary>
/// Параметры темы высокой контрастности, возвращаемые <c>SPI_GETHIGHCONTRAST</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct HighContrast
{
    public uint Size;
    public uint Flags;
    public nint DefaultScheme;
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
    internal const long WsCaption = 0x00C0_0000;
    internal const long WsExToolWindow = 0x0000_0080;
    internal const long WsExTopmost = 0x0000_0008;
    internal const long WsExNoActivate = 0x0800_0000;
    internal const long WsExWindowEdge = 0x0000_0100;
    internal const long WsExDlgModalFrame = 0x0000_0001;
    internal const long WsExNoRedirectionBitmap = 0x0020_0000;

    internal const nint HwndTop = 0;
    internal const nint HwndTopmost = -1;
    internal const nint HwndNotTopmost = -2;
    internal const uint AsfwAny = 0xFFFF_FFFF;

    internal const int SwHide = 0;
    internal const int SwShow = 5;
    internal const int SwShowNoActivate = 4;
    internal const int SwMinimize = 6;
    internal const int SwRestore = 9;
    internal const int SwMaximize = 3;

    internal const uint WmNull = 0x0000;
    internal const uint WmClose = 0x0010;
    internal const uint WmQuit = 0x0012;
    internal const uint WmSettingChange = 0x001A;
    internal const uint WmContextMenu = 0x007B;
    internal const uint WmCommand = 0x0111;
    internal const uint WmDeviceChange = 0x0219;
    internal const uint WmDpiChanged = 0x02E0;
    internal const uint WmHotkey = 0x0312;
    internal const nint WmKeydown = 0x0100;
    internal const nint WmSyskeydown = 0x0104;
    internal const nint WmKeyup = 0x0101;
    internal const nint WmSyskeyup = 0x0105;
    internal const uint WmLButtonUp = 0x0202;
    internal const uint WmLButtonDblClk = 0x0203;
    internal const uint WmRButtonUp = 0x0205;
    internal const uint WmApp = 0x8000;

    /// <summary>Окно-получатель широковещательного сообщения.</summary>
    internal const nint HwndBroadcast = 0xFFFF;

    /// <summary>Идентификатор стандартной иконки приложения (<c>IDI_APPLICATION</c>).</summary>
    internal const nint IdiApplication = 32512;

    /// <summary>Всплывающее меню открывается по правой кнопке и возвращает команду числом.</summary>
    internal const uint TpmRightButton = 0x0002;
    internal const uint TpmBottomAlign = 0x0020;
    internal const uint TpmReturnCmd = 0x0100;

    internal const uint MfString = 0x0000;
    internal const uint MfSeparator = 0x0800;

    internal const uint ModAlt = 0x0001;
    internal const uint ModControl = 0x0002;
    internal const uint ModShift = 0x0004;
    internal const uint ModWin = 0x0008;

    /// <summary>Не повторять WM_HOTKEY, пока клавиши удерживаются.</summary>
    internal const uint ModNoRepeat = 0x4000;

    internal const uint SpiSetWorkArea = 0x005B;
    internal const uint SpiSetLogicalDpiOverride = 0x009F;

    /// <summary>Чтение состояния анимаций клиентской области.</summary>
    internal const uint SpiGetClientAreaAnimation = 0x1042;

    /// <summary>Чтение параметров темы высокой контрастности.</summary>
    internal const uint SpiGetHighContrast = 0x0042;

    /// <summary>Путь к файлу обоев рабочего стола.</summary>
    internal const uint SpiGetDesktopWallpaper = 0x0073;

    /// <summary>Признак включённой темы высокой контрастности.</summary>
    internal const uint HcfHighContrastOn = 0x0000_0001;

    internal const int ColorHighlight = 0;
    internal const int ColorHighlightText = 1;
    internal const int ColorWindow = 5;
    internal const int ColorWindowText = 8;
    internal const int ColorButtonFace = 15;
    internal const int ColorGrayText = 17;
    internal const int ColorButtonText = 18;

    internal const uint DevNodesChanged = 0x0007;

    internal const uint SwpNoSize = 0x0001;
    internal const uint SwpNoMove = 0x0002;
    internal const uint SwpNoZOrder = 0x0004;
    internal const uint SwpShowWindow = 0x0040;
    internal const uint SwpHideWindow = 0x0080;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpFrameChanged = 0x0020;

    internal const uint EventSystemForeground = 0x0003;

    /// <summary>Кнопка «ОК» в сообщении.</summary>
    internal const uint MbOk = 0x0000;

    /// <summary>Значок информации в сообщении.</summary>
    internal const uint MbIconInformation = 0x0040;

    /// <summary>Сообщение выводится поверх других окон.</summary>
    internal const uint MbSetForeground = 0x10000;
    internal const uint EventObjectCreate = 0x8000;
    internal const uint EventObjectDestroy = 0x8001;
    internal const uint EventObjectShow = 0x8002;
    internal const uint EventObjectHide = 0x8003;
    internal const uint EventSystemMinimizestart = 0x0016;
    internal const uint EventSystemMinimizeend = 0x0017;
    internal const uint EventObjectCloaked = 0x8017;
    internal const uint EventObjectUncloaked = 0x8018;

    internal const uint WineventOutOfContext = 0x0000;
    internal const uint WineventSkipOwnProcess = 0x0002;

    internal const uint WmDisplayChange = 0x007E;

    internal const uint MonitorInfoFPrimary = 0x0000_0001;

    internal const uint MonitorDefaultToNearest = 0x0000_0002;

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
    internal static extern nint FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint FindWindowEx(nint parent, nint childAfter, string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern int MessageBoxW(nint hwnd, string text, string caption, uint type);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern uint RegisterWindowMessage(string message);

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

    /// <summary>Имя класса окна; пустая строка, если окно уже уничтожено.</summary>
    internal static string GetWindowClassName(nint hwnd)
    {
        var buffer = new char[256];
        var length = GetClassName(hwnd, buffer, buffer.Length);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint hwnd, out Rect rect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClientRect(nint hwnd, out Rect rect);

    /// <summary>Позиция курсора в физических пикселях экрана.</summary>
    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern long GetWindowLongPtr(nint hwnd, long index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", CharSet = CharSet.Unicode)]
    internal static extern nint SetWindowLongPtr(nint hwnd, long index, nint value);

    /// <summary>
    /// Задаёт региональную маску окна: за её пределами система не рисует содержимое.
    /// При успехе владельцем области становится система, поэтому повторно удалять её нельзя.
    /// Возвращает 0 при ошибке.
    /// </summary>
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern int SetWindowRgn(nint hwnd, nint region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    /// <summary>
    /// Регистрирует системную горячую клавишу для окна: WM_HOTKEY приходит в его процедуру,
    /// даже когда приложение не в фокусе. Возвращает <see langword="false"/>, если комбинацию
    /// уже занял другой процесс.
    /// </summary>
    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(nint hwnd, int id);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyWindow(nint hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint hwnd, int command);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint GetWindow(nint hwnd, int command);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern nint GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(nint hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AllowSetForegroundWindow(uint processId);

    /// <summary>
    /// Принудительно выводит окно на передний план, даже когда система отклонила
    /// <see cref="SetForegroundWindow"/> из-за foreground lock. Этим же путём пользуется
    /// панель задач; звонка «не удалось активировать» при отказе не бывает.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern void SwitchToThisWindow(nint hwnd, [MarshalAs(UnmanagedType.Bool)] bool altTab);

    /// <summary>Передаёт фокус ввода окну; действует в пределах присоединённой очереди ввода.</summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint SetFocus(nint hwnd);

    /// <summary>
    /// Состояние клавиши: старший бит означает «нажата сейчас». Используется вместо
    /// клавиатурного хука для клавиши Windows — хук перехватывал бы её у системы.
    /// </summary>
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern short GetAsyncKeyState(int virtualKey);

    internal const int VkLeftWindows = 0x5B;
    internal const int VkRightWindows = 0x5C;
    internal const int VkTab = 0x09;
    internal const int VkS = 0x53;
    internal const int VkLeftButton = 0x01;

    /// <summary>
    /// Метка синтетического нажатия Win, которое отправляет сам Glaze Shell. По ней хук
    /// отличает своё событие от чужого ввода: жесты тачпада и средства автоматизации тоже
    /// помечены <c>LLKHF_INJECTED</c>, но их перехватывать нужно.
    /// </summary>
    internal const nint SyntheticMarker = 0x474C415A;

    /// <summary>Ввод клавиатуры для <see cref="SendInput"/>.</summary>
    internal const uint InputKeyboard = 1;

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeybdInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeybdInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint count, [In] Input[] inputs, int size);

    /// <summary>Поднимает клавишу, если она ещё не нажата, и оставляет её нажатой.</summary>
    internal static void PressVirtualKey(ushort virtualKey)
    {
        var inputs = new[]
        {
            new Input
            {
                Type = InputKeyboard,
                Union = new InputUnion
                {
                    Keyboard = new KeybdInput { Vk = virtualKey, Scan = 0, Flags = 0, Time = 0, ExtraInfo = SyntheticMarker }
                }
            }
        };

        _ = SendInput(1, inputs, Marshal.SizeOf<Input>());
    }

    /// <summary>Вид хука: низкоуровневая клавиатура.</summary>
    internal const int WhKeyboardLl = 13;

    /// <summary>Код возврата низкоуровневого хука: заблокировать клавишу.</summary>
    internal const nint LlhcBlocked = 1;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint LowLevelKeyboardProc(int code, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint SetWindowsHookExW(int idHook, LowLevelKeyboardProc procedure, nint module, uint threadId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

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

    /// <summary>
    /// Читает параметры темы высокой контрастности. Параметры только читаются, поэтому
    /// перезапись не нужна.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SystemParametersInfoW(uint action, uint param, ref HighContrast value, uint winIni);

    /// <summary>Читает состояние анимаций клиентской области (системная настройка анимаций).</summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SystemParametersInfoW(uint action, uint param, ref uint value, uint winIni);

    /// <summary>Читает строковый параметр системы, например путь к файлу обоев.</summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SystemParametersInfoW(uint action, uint param, [Out] char[] value, uint winIni);

    /// <summary>Возвращает текущий системный цвет по индексу (например, <c>COLOR_WINDOW</c>).</summary>
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern uint GetSysColor(int index);

    /// <summary>Создаёт пустое всплывающее меню (контекстное меню трея).</summary>
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern nint CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "AppendMenuW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AppendMenuW(nint menu, uint flags, nint itemId, string? text);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hwnd, nint parameters);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyMenu(nint menu);

    /// <summary>Загружает стандартную иконку системы: используется как запасная для трея.</summary>
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern nint LoadIconW(nint instance, nint iconName);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(nint icon);
}
