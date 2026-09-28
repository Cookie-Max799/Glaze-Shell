using System.Globalization;
using System.Runtime.InteropServices;
using GlazeShell.Core.Discovery;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Windows.Interop;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Shell;

public sealed class AppsFolderSource : IApplicationDiscoverySource
{
    private readonly ApplicationDiscoveryOptions _options;

    public AppsFolderSource()
        : this(null)
    {
    }

    public AppsFolderSource(ApplicationDiscoveryOptions? options)
    {
        _options = (options ?? ApplicationDiscoveryOptions.Default).Validate();
    }

    public string Name => "apps-folder";

    public ApplicationDiscoveryResult Discover(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var candidates = new List<ApplicationCandidate>();
        var warnings = new List<string>();
        var skipped = 0;
        var failures = 0;

        try
        {
            var items = ComApartment.Run(() => ReadAppsFolder(warnings));

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (item.ApplicationUserModelId is null || item.DisplayName is null)
                {
                    skipped++;
                    continue;
                }

                candidates.Add(new ApplicationCandidate(
                    item.DisplayName,
                    Name,
                    ApplicationType.Msix,
                    packageFamilyName: item.PackageFamilyName,
                    applicationUserModelId: item.ApplicationUserModelId,
                    description: item.Description,
                    iconPath: item.IconPath,
                    publisher: item.Publisher,
                    version: item.Version,
                    isSystemApplication: false));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException or NotSupportedException or ArgumentException)
        {
            warnings.Add(string.Create(CultureInfo.InvariantCulture, $"{exception.GetType().Name}: {exception.Message}"));
            failures++;
        }

        if (failures > 0)
        {
            warnings.Add(string.Create(CultureInfo.InvariantCulture, $"{failures} shell item(s) could not be read."));
        }

        return new ApplicationDiscoveryResult(candidates, warnings, skipped);
    }

    private static List<AppsFolderEntry> ReadAppsFolder(List<string> warnings)
    {
        var results = new List<AppsFolderEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var created = Shell32.SHGetKnownFolderItem(
            in ShellIdentifiers.FolderIdAppsFolder,
            Shell32.KfFlagDefault,
            0,
            in ShellIdentifiers.InterfaceIShellItem,
            out var rootRaw);

        if (created < 0 || rootRaw == 0)
        {
            warnings.Add(string.Create(CultureInfo.InvariantCulture, $"SHGetKnownFolderItem returned {ComApartment.Describe(created)}."));
            return results;
        }

try
        {
            var root = (IShellItem)Marshal.GetObjectForIUnknown(rootRaw);

            if (!CanEnumerateRoot(root, warnings))
            {
                return results;
            }

            var bind = root.BindToHandler(
                nint.Zero,
                in ShellIdentifiers.BhidEnumItems,
                in ShellIdentifiers.InterfaceIShellItemArray,
                out var arrayRaw);

            if (bind < 0 || arrayRaw == 0)
            {
                warnings.Add(string.Create(CultureInfo.InvariantCulture, $"IShellItemArray bind returned {ComApartment.Describe(bind)}."));
                return results;
            }

            try
            {
                var array = (IShellItemArray)Marshal.GetObjectForIUnknown(arrayRaw);

                if (array.GetCount(out var count) < 0)
                {
                    return results;
                }

                for (var index = 0u; index < count; index++)
                {
                    if (array.GetItemAt(index, out var itemRaw) < 0 || itemRaw == 0)
                    {
                        continue;
                    }

                    try
                    {
                        var entry = ReadItem(itemRaw);

                        if (entry is not null && seen.Add(entry.ApplicationUserModelId!))
                        {
                            results.Add(entry);
                        }
                    }
                    finally
                    {
                        Marshal.Release(itemRaw);
                    }
                }
            }
            finally
            {
                Marshal.Release(arrayRaw);
            }
        }
        finally
        {
            Marshal.Release(rootRaw);
        }

        return results;
    }

    private static bool CanEnumerateRoot(IShellItem root, List<string> warnings)
    {
        var result = root.BindToHandler(
            nint.Zero,
            in ShellIdentifiers.BhidSfObject,
            in ShellIdentifiers.InterfaceIShellFolder,
            out var folderRaw);

        if (result >= 0 && folderRaw != 0)
        {
            var folder = Marshal.GetObjectForIUnknown(folderRaw);

            try
            {
                Marshal.ReleaseComObject(folder);
            }
            finally
            {
                Marshal.Release(folderRaw);
            }

            return true;
        }

        warnings.Add(string.Create(
            CultureInfo.InvariantCulture,
            $"AppsFolder enumeration is unavailable (0x{unchecked((uint)result):X8}); skipping MSIX source."));

        return false;
    }

    private static AppsFolderEntry? ReadItem(nint itemRaw)
    {
        var item2 = ShellIdentifiers.TryQuery<IShellItem2>(itemRaw, in ShellIdentifiers.InterfaceIShellItem2);

        if (item2 is null)
        {
            return null;
        }

        var aumid = ReadString(item2, in ShellIdentifiers.AppUserModelId)
            ?? TryReadAumidFromParsingName(item2);

        if (string.IsNullOrWhiteSpace(aumid) || !aumid.Contains('!', StringComparison.Ordinal))
        {
            return null;
        }

        var displayName = ReadDisplayName(item2, Sigdn.NormalDisplay);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var separator = aumid.IndexOf('!', StringComparison.Ordinal);

        return new AppsFolderEntry(
            displayName,
            aumid,
            separator > 0 ? aumid[..separator] : null,
            ReadDisplayName(item2, Sigdn.FileSystemPath),
            null,
            null,
            null);
    }

    private static string? TryReadAumidFromParsingName(IShellItem item)
    {
        var parsingName = ReadDisplayName(item, Sigdn.DesktopAbsoluteParsing);

        if (string.IsNullOrWhiteSpace(parsingName) || !parsingName.Contains('!', StringComparison.Ordinal))
        {
            return null;
        }

        return parsingName;
    }

    private static string? ReadString(IShellItem2 item, in PropertyKey key)
    {
        try
        {
            var result = item.GetString(in key, out var value);

            if (result < 0 || string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static string? ReadDisplayName(IShellItem item, Sigdn form)
    {
        try
        {
            if (item.GetDisplayName(form, out var pointer) < 0 || pointer == 0)
            {
                return null;
            }

            try
            {
                var value = Marshal.PtrToStringUni(pointer);
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }
            finally
            {
                Marshal.FreeCoTaskMem(pointer);
            }
        }
        catch (COMException)
        {
            return null;
        }
    }

    private sealed record AppsFolderEntry(
        string DisplayName,
        string ApplicationUserModelId,
        string? PackageFamilyName,
        string? IconPath,
        string? Description,
        string? Publisher,
        string? Version);
}
