using System.Runtime.InteropServices;

namespace GlazeShell.Windows.Shell;

[ComImport]
[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    [PreserveSig]
    int BindToHandler(nint bindContext, in Guid behaviorId, in Guid interfaceId, out nint result);

    [PreserveSig]
    int GetParent(out nint parent);

    [PreserveSig]
    int GetDisplayName(Sigdn form, out nint name);

    [PreserveSig]
    int GetAttributes(Sfgao mask, out Sfgao attributes);

    [PreserveSig]
    int Compare(nint other, Sichintf hint, out int order);
}

[ComImport]
[Guid("B63EA76D-1F85-456F-A19C-47959E30A9A9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem2 : IShellItem
{
    [PreserveSig]
    int GetProperty(in PropertyKey key, out nint value);

    [PreserveSig]
    int GetString(in PropertyKey key, [MarshalAs(UnmanagedType.LPWStr)] out string value);

    [PreserveSig]
    int GetUInt32(in PropertyKey key, out uint value);

    [PreserveSig]
    int GetInt64(in PropertyKey key, out long value);

    [PreserveSig]
    int GetUInt64(in PropertyKey key, out ulong value);

    [PreserveSig]
    int GetDouble(in PropertyKey key, out double value);

    [PreserveSig]
    int GetGuid(in PropertyKey key, out Guid value);

    [PreserveSig]
    int GetCanonicalName(out nint name);

    [PreserveSig]
    int GetPropertyStore(uint access, out nint propertyStore);

    [PreserveSig]
    int GetExtendedPropertyStore(uint access, out nint propertyStore);
}
