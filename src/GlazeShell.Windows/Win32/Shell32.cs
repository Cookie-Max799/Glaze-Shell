using System.Runtime.InteropServices;

namespace GlazeShell.Windows.Win32;

internal static class Shell32
{
    internal const uint KfFlagDefault = 0x00000000;

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
