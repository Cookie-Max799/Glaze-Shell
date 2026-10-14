using System.Diagnostics;
using GlazeShell.Core.Discovery;
using GlazeShell.Core.Interfaces;

namespace GlazeShell.Windows.Applications;

public sealed class ProcessInspector : IProcessInspector
{
    public IReadOnlyList<int> FindProcessesByPath(string executablePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var normalized = NormalizePath(executablePath);
        var matches = new List<int>();

        Collect(matches, path =>
            string.Equals(path, normalized, StringComparison.OrdinalIgnoreCase), cancellationToken);

        return matches;
    }

    public IReadOnlyList<int> FindProcessesByDirectory(string directory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var prefix = TrimSeparator(directory);
        var matches = new List<int>();

        Collect(matches, path => IsUnderDirectory(path, prefix), cancellationToken);

        return matches;
    }

    public IReadOnlyList<int> FindProcessesByImageInDirectory(
        string directory,
        IReadOnlyCollection<string> imageNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(imageNames);

        if (imageNames.Count == 0)
        {
            return Array.Empty<int>();
        }

        var prefix = TrimSeparator(directory);
        var matches = new List<int>();

        Collect(matches, path =>
            IsUnderDirectory(path, prefix) && HasImageName(path, imageNames), cancellationToken);

        return matches;
    }

    public IReadOnlyList<int> FindProcessesByPackage(string packageFamilyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageFamilyName);

        var matches = new List<int>();

        Collect(matches, path => PackageIdentity.IsPathOfPackage(path, packageFamilyName), cancellationToken);

        return matches;
    }

    private static void Collect(List<int> matches, Func<string, bool> predicate, CancellationToken cancellationToken)
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (predicate(SafePath(process)))
                {
                    matches.Add(process.Id);
                }
            }
        }
    }

    private static bool IsUnderDirectory(string path, string prefix)
    {
        if (path.Length <= prefix.Length || !path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path[prefix.Length] == Path.DirectorySeparatorChar || path[prefix.Length] == Path.AltDirectorySeparatorChar;
    }

    private static bool HasImageName(string path, IReadOnlyCollection<string> imageNames)
    {
        var image = Path.GetFileName(path);

        if (image.Length == 0)
        {
            return false;
        }

        foreach (var name in imageNames)
        {
            if (string.Equals(image, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }


    private static string SafePath(Process process)
    {
        try
        {
            return NormalizePath(process.MainModule?.FileName ?? string.Empty);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return string.Empty;
        }
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    private static string TrimSeparator(string value) =>
        value.Length > 3 ? value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : value;
}
