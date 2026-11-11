using System.Runtime.InteropServices;

namespace GlazeShell.Windows.Win32;

internal static class Shell32
{
    internal const uint KfFlagDefault = 0x00000000;

    /// <summary>Идентификатор <see cref="IShellItemImageFactory"/> (IID).</summary>
    internal const string ShellItemImageFactoryId = "bcc18b79-ba16-442f-80c4-8a59c30c463b";

    /// <summary>
    /// Флаги <see cref="IShellItemImageFactory.GetImage"/>: запрашивать только иконку,
    /// вписав её в заданный размер.
    /// </summary>
    internal const uint SiigbfIconOnly = 0x0000_0004;
    internal const uint SiigbfResizeToFit = 0x0000_0000;

    // Работа с иконкой в области уведомлений (трей).
    internal const uint NifMessage = 0x0000_0001;
    internal const uint NifIcon = 0x0000_0002;
    internal const uint NifTip = 0x0000_0004;
    internal const uint NimAdd = 0x0000_0000;
    internal const uint NimModify = 0x0000_0001;
    internal const uint NimDelete = 0x0000_0002;

    /// <summary>
    /// Данные иконки области уведомлений. Поля идут в порядке <c>NOTIFYICONDATA</c>;
    /// размер структуры передаётся в <see cref="Size"/> и считается маршалером, поэтому
    /// структура одинаково верна для x64 и x86.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NotifyIconData
    {
        public uint Size;
        public nint Window;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public nint Icon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Tip;

        public uint State;
        public uint StateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Info;

        public uint TimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string InfoTitle;

        public uint InfoFlags;
        public Guid ItemGuid;
        public nint BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeSize
    {
        public int Width;
        public int Height;
    }

    [ComImport]
    [Guid(ShellItemImageFactoryId)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(NativeSize size, uint flags, out nint bitmap);
    }

    [DllImport("shell32.dll", ExactSpelling = true)]
    internal static extern int SHGetKnownFolderPath(
        in Guid folderId,
        uint flags,
        nint token,
        out nint path);

    [DllImport("shell32.dll", ExactSpelling = true)]
    internal static extern int SHGetKnownFolderItem(
        in Guid folderId,
        uint flags,
        nint token,
        in Guid interfaceId,
        out nint item);

    [DllImport("shell32.dll", ExactSpelling = true)]
    internal static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string path,
        nint bindingContext,
        in Guid interfaceId,
        out nint item);

    [DllImport("shell32.dll", ExactSpelling = true)]
    internal static extern int SHCreateItemFromIDList(
        nint idList,
        in Guid interfaceId,
        out nint item);

    /// <summary>Добавляет, изменяет или удаляет иконку в области уведомлений.</summary>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Shell_NotifyIconW(uint message, ref NotifyIconData data);

    /// <summary>Достаёт большую и малую иконки из файла; индекс 0 — иконка приложения.</summary>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern uint ExtractIconExW(
        string file,
        int iconIndex,
        out nint largeIcon,
        out nint smallIcon,
        uint iconCount);
}
