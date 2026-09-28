using System.Runtime.InteropServices;

namespace GlazeShell.Windows.Shell;

[Flags]
internal enum Sfgao : uint
{
    None = 0x00000000,
    HasSubFolder = 0x00000001,
    HasStorage = 0x00000004,
    HasProperty = 0x00000008,
    IsFolder = 0x00000010,
    IsLink = 0x00000080,
    IsShared = 0x00000100,
    IsAlias = 0x00000800,
    IsFileSystem = 0x10000000,
}

[ComImport]
[Guid("000214E6-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellFolder
{
    [PreserveSig]
    int BindToHandler(nint bindContext, in Guid behaviorId, in Guid interfaceId, out nint result);

    [PreserveSig]
    int GetDisplayName(Shgdn format, out nint name);

    [PreserveSig]
    int GetAttributes(Sfgao mask, out Sfgao attributes);

    [PreserveSig]
    int Compare(nint other, Sichintf hint, out int order);
}

[ComImport]
[Guid("000214F2-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IEnumIdList
{
    [PreserveSig]
    int Next(uint requested, out nint item, out uint fetched);

    [PreserveSig]
    int Skip(uint count);

    [PreserveSig]
    int Reset();

    [PreserveSig]
    int Clone(out nint enumerator);
}
