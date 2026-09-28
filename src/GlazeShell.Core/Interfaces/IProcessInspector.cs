namespace GlazeShell.Core.Interfaces;

public interface IProcessInspector
{
    IReadOnlyList<int> FindProcessesByPath(string executablePath, CancellationToken cancellationToken = default);

    IReadOnlyList<int> FindProcessesByDirectory(string directory, CancellationToken cancellationToken = default);
}

public interface IPackageLocationResolver
{
    string? ResolveInstallLocation(string packageFamilyName, CancellationToken cancellationToken = default);
}
