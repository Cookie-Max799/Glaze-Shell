using System.Runtime.InteropServices;
using System.Text;
using GlazeShell.Windows.Interop;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Shell;

public enum ShellLinkResolutionStrategy
{
    ManagedLinkInfo,
    ManagedRelativePath,
    ShellPropertyStore,
    ShellLinkCom,
    Unresolved,
}

public sealed record ShellLinkTarget(
    string Path,
    string? Arguments,
    string? WorkingDirectory,
    string? Description,
    string? IconPath,
    int IconIndex,
    ShellLinkResolutionStrategy Strategy)
{
    public string? Name { get; init; }
}

public static class ShellLinkResolver
{
    public const int BufferLength = 1024;

    public static bool TryResolve(string shortcutPath, out ShellLinkTarget? target, out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shortcutPath);

        error = null;

        if (TryResolveManaged(shortcutPath, out var managed, out var managedError))
        {
            target = managed;
            return true;
        }

        if (TryResolveViaPropertyStore(shortcutPath, out var property, out var propertyError))
        {
            target = property;
            return true;
        }

        if (TryResolveViaShellLink(shortcutPath, out var com, out var comError))
        {
            target = com;
            return true;
        }

        target = null;
        error = string.Join(
            "; ",
            new[] { managedError, propertyError, comError }.Where(static part => !string.IsNullOrWhiteSpace(part)));
        return false;
    }

    public static ShellLinkTarget? TryResolveManaged(string shortcutPath)
    {
        ArgumentNullException.ThrowIfNull(shortcutPath);
        TryResolveManaged(shortcutPath, out var target, out _);
        return target;
    }

    private static bool TryResolveManaged(string shortcutPath, out ShellLinkTarget? target, out string? error)
    {
        target = null;
        error = null;

        ShellLinkData? data;

        try
        {
            data = ShellLinkData.TryRead(shortcutPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            error = $"managed: {exception.Message}";
            return false;
        }

        if (data is null)
        {
            error = "managed: not a readable shell link";
            return false;
        }

        var path = data.ResolveTargetPath(shortcutPath);

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "managed: no target path in header";
            return false;
        }

        var strategy = data.LocalBasePath is not null
            ? ShellLinkResolutionStrategy.ManagedLinkInfo
            : ShellLinkResolutionStrategy.ManagedRelativePath;

        target = new ShellLinkTarget(
            path,
            data.Arguments,
            data.WorkingDirectory,
            data.Name,
            data.IconLocation,
            data.IconIndex,
            strategy)
        {
            Name = data.Name,
        };

        return true;
    }

    private static bool TryResolveViaPropertyStore(string shortcutPath, out ShellLinkTarget? target, out string? error)
    {
        target = null;
        error = null;

        try
        {
            target = ComApartment.Run<ShellLinkTarget?>(() =>
            {
                var created = Shell32.SHCreateItemFromParsingName(
                    shortcutPath,
                    0,
                    in ShellIdentifiers.InterfaceIShellItem,
                    out var raw);

                if (created < 0 || raw == 0)
                {
                    return null;
                }

                try
                {
                    var item = ShellIdentifiers.TryQuery<IShellItem2>(raw, in ShellIdentifiers.InterfaceIShellItem2);

                    if (item is null)
                    {
                        return null;
                    }

                    var path = ReadString(item, in ShellIdentifiers.LinkTargetParsingPath)
                        ?? ReadString(item, in ShellIdentifiers.LinkTargetPath);

                    if (string.IsNullOrWhiteSpace(path))
                    {
                        return null;
                    }

                    return new ShellLinkTarget(
                        path,
                        ReadString(item, in ShellIdentifiers.LinkArguments),
                        ReadString(item, in ShellIdentifiers.LinkWorkingDirectory),
                        ReadString(item, in ShellIdentifiers.LinkDescription),
                        ReadString(item, in ShellIdentifiers.LinkIconPath),
                        ReadInt32(item, in ShellIdentifiers.LinkIconIndex),
                        ShellLinkResolutionStrategy.ShellPropertyStore)
                    {
                        Name = Path.GetFileNameWithoutExtension(shortcutPath),
                    };
                }
                finally
                {
                    Marshal.Release(raw);
                }
            });

            if (target is not null)
            {
                return true;
            }

            error = "property store: no target property";
            return false;
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException or NotSupportedException or ArgumentException)
        {
            error = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"property store: {exception.GetType().Name}");
            return false;
        }
    }

    private static bool TryResolveViaShellLink(string shortcutPath, out ShellLinkTarget? target, out string? error)
    {
        target = null;
        error = null;

        try
        {
            target = ComApartment.Run<ShellLinkTarget?>(() =>
            {
                var instance = ShellIdentifiers.CreateInstance(
                    in ShellIdentifiers.ClassShellLink,
                    in ShellIdentifiers.InterfaceIShellLinkW,
                    Ole32.ClsctxInProcServer);

                try
                {
                    var link = (IShellLinkW)Marshal.GetObjectForIUnknown(instance);
                    var load = link.Load(shortcutPath, ShellIdentifiers.StgmRead);

                    if (load != 0)
                    {
                        return null;
                    }

                    var path = ReadPath(link, Slgp.Unused) ?? ReadPath(link, Slgp.RawPath);

                    if (string.IsNullOrWhiteSpace(path))
                    {
                        return null;
                    }

                    return new ShellLinkTarget(
                        path,
                        ReadString((b, n) => { _ = link.GetArguments(b, n); }),
                        ReadString((b, n) => { _ = link.GetWorkingDirectory(b, n); }),
                        ReadString((b, n) => { _ = link.GetDescription(b, n); }),
                        ReadIconPath(link, out var iconIndex),
                        iconIndex,
                        ShellLinkResolutionStrategy.ShellLinkCom)
                    {
                        Name = ReadString((b, n) => { _ = link.GetDescription(b, n); }),
                    };
                }
                finally
                {
                    Marshal.Release(instance);
                }
            });

            if (target is not null)
            {
                return true;
            }

            error = "shell link COM: Load returned no target";
            return false;
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException or NotSupportedException or ArgumentException)
        {
            error = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"shell link COM: {exception.GetType().Name}");
            return false;
        }
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

    private static int ReadInt32(IShellItem2 item, in PropertyKey key)
    {
        try
        {
            return item.GetUInt32(in key, out var value) < 0 ? 0 : unchecked((int)value);
        }
        catch (COMException)
        {
            return 0;
        }
    }

    private static string? ReadPath(IShellLinkW link, Slgp flags)
    {
        var builder = new StringBuilder(BufferLength);

        if (link.GetPath(builder, BufferLength, 0, flags) < 0)
        {
            return null;
        }

        return Normalize(builder);
    }

    private static string? ReadString(Action<StringBuilder, int> reader)
    {
        var builder = new StringBuilder(BufferLength);
        reader(builder, BufferLength);
        return Normalize(builder);
    }

    private static string? ReadIconPath(IShellLinkW link, out int iconIndex)
    {
        var builder = new StringBuilder(BufferLength);
        iconIndex = 0;

        if (link.GetIconLocation(builder, BufferLength, out iconIndex) < 0)
        {
            return null;
        }

        return Normalize(builder);
    }

    private static string? Normalize(StringBuilder builder)
    {
        if (builder.Length == 0)
        {
            return null;
        }

        var value = builder.ToString().Trim();
        return value.Length == 0 ? null : value;
    }
}
