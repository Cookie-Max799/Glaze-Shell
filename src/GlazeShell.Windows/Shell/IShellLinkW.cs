using System.Runtime.InteropServices;
using System.Text;

namespace GlazeShell.Windows.Shell;

[ComImport]
[Guid("0000010C-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPersist
{
    [PreserveSig]
    int GetClassID(out Guid classId);
}

[ComImport]
[Guid("0000010B-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPersistFile : IPersist
{
    [PreserveSig]
    int IsDirty();

    [PreserveSig]
    int Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);

    [PreserveSig]
    int Save([MarshalAs(UnmanagedType.LPWStr)] string? fileName, [MarshalAs(UnmanagedType.Bool)] bool remember);

    [PreserveSig]
    int SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);

    [PreserveSig]
    int GetCurFile(out nint fileName);
}

[ComImport]
[Guid("000214F9-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellLinkW : IPersistFile
{
    [PreserveSig]
    int GetPath([MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maximumLength, nint findData, Slgp flags);

    [PreserveSig]
    int GetIDList(out nint idList);

    [PreserveSig]
    int SetIDList(nint idList);

    [PreserveSig]
    int GetDescription([MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maximumLength);

    [PreserveSig]
    int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);

    [PreserveSig]
    int GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int maximumLength);

    [PreserveSig]
    int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);

    [PreserveSig]
    int GetArguments([MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int maximumLength);

    [PreserveSig]
    int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);

    [PreserveSig]
    int GetHotkey(out short hotkey);

    [PreserveSig]
    int SetHotkey(short hotkey);

    [PreserveSig]
    int GetShowCmd(out int showCmd);

    [PreserveSig]
    int SetShowCmd(int showCmd);

    [PreserveSig]
    int GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int maximumLength, out int iconIndex);

    [PreserveSig]
    int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);

    [PreserveSig]
    int SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, uint reserved);

    [PreserveSig]
    int Resolve(nint window, uint flags);

    [PreserveSig]
    int SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
}
