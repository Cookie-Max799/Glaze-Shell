using System.Runtime.InteropServices;

namespace GlazeShell.Windows.Interop;

[ComImport]
[Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IApplicationActivationManager
{
    [PreserveSig]
    int ActivateApplication(
        [MarshalAs(UnmanagedType.LPWStr)] string applicationUserModelId,
        [MarshalAs(UnmanagedType.LPWStr)] string? arguments,
        uint options,
        out uint processId);

    [PreserveSig]
    int ActivateForFile(
        [MarshalAs(UnmanagedType.LPWStr)] string applicationUserModelId,
        nint itemArray,
        [MarshalAs(UnmanagedType.LPWStr)] string? verb,
        out uint processId);

    [PreserveSig]
    int ActivateForProtocol(
        [MarshalAs(UnmanagedType.LPWStr)] string applicationUserModelId,
        nint itemArray,
        out uint processId);

    [PreserveSig]
    int GetApplicationUserModelId(nint processHandle, out nint applicationUserModelId);

    [PreserveSig]
    int GetApplicationUserModelIdFromProcessId(int processId, out nint applicationUserModelId);

    [PreserveSig]
    int GetApplicationUserModelIdFromShortcut(
        [MarshalAs(UnmanagedType.LPWStr)] string shortcutPath,
        out nint applicationUserModelId);
}
