using System.Globalization;
using System.Runtime.InteropServices;
using GlazeShell.Core.Discovery;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Windows.Interop;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Shell;

public sealed class StartMenuShortcutSource : IApplicationDiscoverySource
{
    private const string ShortcutPattern = "*.lnk";
    private const string ExplorerExecutable = "explorer.exe";

    private static readonly HashSet<string> AllowedTargetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe",
        ".msi",
        ".appx",
        ".msix",
    };

    private static readonly Lazy<string> SystemRoot =
        new(() => Environment.GetFolderPath(Environment.SpecialFolder.Windows), LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly string[] _roots;
    private readonly ApplicationDiscoveryOptions _options;

    public StartMenuShortcutSource()
        : this(null)
    {
    }

    public StartMenuShortcutSource(ApplicationDiscoveryOptions? options)
        : this(ResolveRoots(), options)
    {
    }

    public StartMenuShortcutSource(IReadOnlyList<string> roots, ApplicationDiscoveryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(roots);

        _roots = roots.Where(static root => !string.IsNullOrWhiteSpace(root)).ToArray();
        _options = (options ?? ApplicationDiscoveryOptions.Default).Validate();
    }

    public string Name => "start-menu";

    public ApplicationDiscoveryResult Discover(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var warnings = new List<string>();
        var files = CollectShortcutFiles(warnings, cancellationToken);

        if (_roots.Length == 0)
        {
            warnings.Add("No Start Menu roots could be resolved.");
        }

        var resolved = ResolveAll(files, warnings, cancellationToken);

        return new ApplicationDiscoveryResult(resolved.Candidates, warnings, resolved.Skipped);
    }

    private List<string> CollectShortcutFiles(List<string> warnings, CancellationToken cancellationToken)
    {
        var files = new List<string>();

        foreach (var root in _roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CollectDirectory(root, 0, files, warnings, cancellationToken);
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    private void CollectDirectory(
        string directory,
        int depth,
        List<string> files,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        if (depth > _options.MaxStartMenuDepth)
        {
            AppendWarning(warnings, string.Create(CultureInfo.InvariantCulture, $"Depth limit {depth} reached at '{directory}'."));
            return;
        }

        foreach (var file in SafeEnumerate(
                     () => Directory.EnumerateFiles(directory, ShortcutPattern, SearchOption.TopDirectoryOnly),
                     warnings,
                     directory))
        {
            files.Add(file);
        }

        foreach (var subDirectory in SafeEnumerate(() => Directory.EnumerateDirectories(directory), warnings, directory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsReparsePoint(subDirectory))
            {
                continue;
            }

            CollectDirectory(subDirectory, depth + 1, files, warnings, cancellationToken);
        }
    }

    private ResolveSummary ResolveAll(List<string> files, List<string> warnings, CancellationToken cancellationToken)
    {
        var candidates = new List<ApplicationCandidate>(files.Count);
        var skipped = 0;
        var unresolved = 0;
        var maxDegree = Math.Clamp(Environment.ProcessorCount - 1, 1, 8);
        var gate = new object();

        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = maxDegree,
        };

        Parallel.ForEach(
            files,
            options,
            file =>
            {
                var outcome = ProcessShortcut(file);

                lock (gate)
                {
                    if (outcome.Candidate is not null)
                    {
                        candidates.Add(outcome.Candidate);
                    }
                    else
                    {
                        skipped++;

                        if (outcome.Unresolved && unresolved < _options.MaxWarningsPerSource)
                        {
                            unresolved++;
                        }
                    }
                }
            });

        candidates.Sort(static (left, right) =>
        {
            var byName = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            return byName != 0 ? byName : string.CompareOrdinal(left.ExecutablePath ?? left.ApplicationUserModelId, right.ExecutablePath ?? right.ApplicationUserModelId);
        });

        if (unresolved > 0)
        {
            AppendWarning(warnings, string.Create(
                CultureInfo.InvariantCulture,
                $"{unresolved} shortcut(s) expose no target path through any supported strategy."));
        }

        return new ResolveSummary(candidates, skipped);
    }

    private (ApplicationCandidate? Candidate, bool Unresolved) ProcessShortcut(string shortcutPath)
    {
        if (!ShellLinkResolver.TryResolve(shortcutPath, out var target, out _))
        {
            return (null, true);
        }

        if (target is null || string.IsNullOrWhiteSpace(target.Path))
        {
            return (null, true);
        }

        var extension = Path.GetExtension(target.Path);

        if (!AllowedTargetExtensions.Contains(extension))
        {
            return (null, false);
        }

        if (_options.RequireExistingTarget && !File.Exists(target.Path))
        {
            return (null, false);
        }

        if (IsShellNavigationTarget(target.Path))
        {
            return (null, false);
        }

        var name = Path.GetFileNameWithoutExtension(shortcutPath).Trim();

        if (name.Length == 0)
        {
            name = Path.GetFileNameWithoutExtension(target.Path).Trim();
        }

        if (name.Length == 0)
        {
            return (null, false);
        }

        var candidate = new ApplicationCandidate(
            name,
            Name,
            ApplicationType.Win32,
            target.Path,
            iconPath: target.IconPath,
            description: target.Description,
            isSystemApplication: IsSystemPath(target.Path),
            arguments: target.Arguments,
            workingDirectory: target.WorkingDirectory,
            iconIndex: target.IconIndex);

        return (candidate, false);
    }

    private static List<string> ResolveRoots()
    {
        var roots = new List<string>(2);

        try
        {
            foreach (var folderId in new[] { ShellIdentifiers.FolderIdPrograms, ShellIdentifiers.FolderIdCommonPrograms })
            {
                var result = Shell32.SHGetKnownFolderPath(in folderId, Shell32.KfFlagDefault, 0, out var path);

                if (result < 0 || path == 0)
                {
                    continue;
                }

                try
                {
                    var value = Marshal.PtrToStringUni(path);

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        roots.Add(value);
                    }
                }
                finally
                {
                    ComApartment.CoTaskMemFree(path);
                }
            }
        }
        catch (DllNotFoundException)
        {
            return roots;
        }
        catch (EntryPointNotFoundException)
        {
            return roots;
        }

        return roots;
    }

    private void AppendWarning(List<string> warnings, string warning)
    {
        if (warnings.Count < _options.MaxWarningsPerSource)
        {
            warnings.Add(warning);
        }
    }

    private static string[] SafeEnumerate(Func<IEnumerable<string>> factory, List<string> warnings, string directory)
    {
        try
        {
            return factory().ToArray();
        }
        catch (Exception exception) when (exception is DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            warnings.Add(string.Create(CultureInfo.InvariantCulture, $"'{directory}': {exception.Message}"));
            return Array.Empty<string>();
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static bool IsShellNavigationTarget(string path) =>
        string.Equals(Path.GetFileName(path), ExplorerExecutable, StringComparison.OrdinalIgnoreCase);

    private static bool IsSystemPath(string path)
    {
        var windows = SystemRoot.Value;
        return !string.IsNullOrEmpty(windows) && path.StartsWith(windows, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct ResolveSummary(IReadOnlyList<ApplicationCandidate> Candidates, int Skipped);
}
