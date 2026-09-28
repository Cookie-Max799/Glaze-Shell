using System.Runtime.InteropServices;

namespace GlazeShell.Windows.Shell;

[ComImport]
[Guid("56FDF344-FD6D-11D0-958A-006097C9A090")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemArray
{
    [PreserveSig]
    int BindToHandler(nint bindContext, in Guid behaviorId, in Guid interfaceId, out nint result);

    [PreserveSig]
    int GetPropertyStore(uint access, out nint propertyStore);

    [PreserveSig]
    int GetPropertyDescriptionList(in PropertyKey key, out nint descriptionList);

    [PreserveSig]
    int GetAttributes(Sfgao mask, out Sfgao attributes);

    [PreserveSig]
    int GetCount(out uint count);

    [PreserveSig]
    int GetItemAt(uint index, out nint item);

    [PreserveSig]
    int EnumItems(out nint enumerator);
}
