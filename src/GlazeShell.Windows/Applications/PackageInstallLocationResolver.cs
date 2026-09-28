using Microsoft.Win32;
using GlazeShell.Core.Interfaces;

namespace GlazeShell.Windows.Applications;

public sealed class PackageInstallLocationResolver : IPackageLocationResolver
{
    private const string RepositoryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Applications";

    private static readonly Lazy<Dictionary<string, string>> Index = new(
        BuildIndex,
        LazyThreadSafetyMode.ExecutionAndPublication);

    public string? ResolveInstallLocation(string packageFamilyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageFamilyName);

        cancellationToken.ThrowIfCancellationRequested();

        var index = Index.Value;

        if (!index.TryGetValue(Normalize(packageFamilyName), out var location))
        {
            return null;
        }

        return DirectoryExists(location) ? location : null;
    }

    private static Dictionary<string, string> BuildIndex()
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using var repository = TryOpenSubKey(hive, RepositoryPath);

            if (repository is null)
            {
                continue;
            }

            foreach (var name in SafeSubKeyNames(repository))
            {
                var separator = name.IndexOf('!', StringComparison.Ordinal);

                if (separator <= 0)
                {
                    continue;
                }

                using var packageKey = TryOpenSubKey(repository, name + @"\Package");

                if (packageKey?.GetValue("Path") is string location && location.Length > 0)
                {
                    index.TryAdd(name[..separator], location);
                }
            }
        }

        return index;
    }

    private static string[] SafeSubKeyNames(RegistryKey key)
    {
        try
        {
            return key.GetSubKeyNames();
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return Array.Empty<string>();
        }
    }

    private static RegistryKey? TryOpenSubKey(RegistryKey root, string name)
    {
        try
        {
            return root.OpenSubKey(name);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static bool DirectoryExists(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }
}
