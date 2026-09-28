using System.Runtime.InteropServices;
using GlazeShell.Windows.Interop;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Shell;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct PropertyKey
{
    internal PropertyKey(Guid formatId, uint propertyId)
    {
        FormatId = formatId;
        PropertyId = propertyId;
    }

    internal Guid FormatId { get; }

    internal uint PropertyId { get; }
}

internal enum Sigdn : uint
{
    NormalDisplay = 0x00000000,
    ParentRelativeParsing = 0x80018001,
    DesktopAbsoluteParsing = 0x80028000,
    FileSystemPath = 0x80058000,
    Url = 0x80068000,
}

internal enum Sichintf : uint
{
    CanOrder = 0x00000001,
    OrderByDate = 0x00000002,
}

internal enum Slgp : uint
{
    Unused = 0x00000000,
    RawPath = 0x00000004,
}

internal enum Shgdn : uint
{
    NormalDisplay = 0x00000000,
    FileSysPath = 0x00080000,
}

internal static class ShellIdentifiers
{
    internal static readonly Guid ClassShellLink = new("00021401-0000-0000-C000-000000000046");
    internal static readonly Guid InterfaceIShellLinkW = new("000214F9-0000-0000-C000-000000000046");
    internal static readonly Guid InterfaceIPersistFile = new("0000010B-0000-0000-C000-000000000046");
    internal static readonly Guid InterfaceIShellItem = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");
    internal static readonly Guid InterfaceIShellItem2 = new("B63EA76D-1F85-456F-A19C-47959E30A9A9");
    internal static readonly Guid InterfaceIShellItemArray = new("56FDF344-FD6D-11D0-958A-006097C9A090");
    internal static readonly Guid InterfaceIShellFolder = new("000214E6-0000-0000-C000-000000000046");
    internal static readonly Guid InterfaceIEnumIdList = new("000214F2-0000-0000-C000-000000000046");
    internal static readonly Guid BhidEnumItems = new("94F60519-2850-4927-AA24-5F0B2AAFD3E3");
    internal static readonly Guid BhidSfObject = new("3981E224-F559-11D3-8E3A-00C04F6837D5");
    internal static readonly Guid ClassApplicationActivationManager = new("45BA127D-10A8-46EA-8AB7-56EA9078943C");
    internal static readonly Guid InterfaceIApplicationActivationManager = new("2E941141-7F97-4756-BA1D-9DECDE894A3D");

    internal static readonly Guid FolderIdPrograms = new("A77F5D77-2E2B-44C3-A6A2-ABA601054A51");
    internal static readonly Guid FolderIdCommonPrograms = new("0139D44E-6AFE-49F2-8690-3DAFCAE6FFB8");
    internal static readonly Guid FolderIdAppsFolder = new("A3918781-E5F2-4890-B3D9-A7E54332328C");

    internal static readonly PropertyKey AppUserModelId = new(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);

    private static readonly Guid LinkPropertySet = new("B64428C4-E1D2-101A-999E-08002B2CF9AE");

    internal static readonly PropertyKey LinkTargetParsingPath = new(LinkPropertySet, 2);
    internal static readonly PropertyKey LinkArguments = new(LinkPropertySet, 3);
    internal static readonly PropertyKey LinkWorkingDirectory = new(LinkPropertySet, 4);
    internal static readonly PropertyKey LinkIconPath = new(LinkPropertySet, 5);
    internal static readonly PropertyKey LinkDescription = new(LinkPropertySet, 6);
    internal static readonly PropertyKey LinkTargetPath = new(LinkPropertySet, 1);
    internal static readonly PropertyKey LinkIconIndex = new(LinkPropertySet, 7);

    internal const uint StgmRead = 0x00000000;
    internal const uint StgmWrite = 0x00000001;

    internal static bool Failed(int hresult) => hresult < 0;

    internal static T Query<T>(nint instance, in Guid interfaceId)
        where T : class
    {
        var result = Marshal.QueryInterface(instance, in interfaceId, out var resolved);

        if (result < 0 || resolved == 0)
        {
            throw CreateException(result);
        }

        try
        {
            return (T)Marshal.GetObjectForIUnknown(resolved)!;
        }
        finally
        {
            Marshal.Release(resolved);
        }
    }

    internal static T? TryQuery<T>(nint instance, in Guid interfaceId)
        where T : class
    {
        var result = Marshal.QueryInterface(instance, in interfaceId, out var resolved);

        if (result < 0 || resolved == 0)
        {
            return null;
        }

        try
        {
            return (T)Marshal.GetObjectForIUnknown(resolved)!;
        }
        finally
        {
            Marshal.Release(resolved);
        }
    }

    internal static nint CreateInstance(in Guid classId, in Guid interfaceId, uint context)
    {
        var result = Ole32.CoCreateInstance(in classId, 0, context, in interfaceId, out var instance);

        if (result < 0 || instance == 0)
        {
            throw CreateException(result);
        }

        return instance;
    }

    private static Exception CreateException(int hresult) =>
        Marshal.GetExceptionForHR(hresult) ?? new InvalidOperationException(ComApartment.Describe(hresult));

    internal static T CreateInstance<T>(in Guid classId, in Guid interfaceId, uint context)
        where T : class
    {
        var instance = CreateInstance(in classId, in interfaceId, context);

        try
        {
            return (T)Marshal.GetObjectForIUnknown(instance)!;
        }
        finally
        {
            Marshal.Release(instance);
        }
    }
}
