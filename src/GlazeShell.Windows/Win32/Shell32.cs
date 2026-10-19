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
}
