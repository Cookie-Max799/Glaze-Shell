using GlazeShell.Core.Discovery;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;

namespace GlazeShell.Windows.Tests;

internal sealed class FakeApplicationManager : IApplicationManager
{
    private readonly Dictionary<string, Application> _applications = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<Application> Applications => _applications.Values.ToArray();

    public FakeApplicationManager Add(Application application)
    {
        _applications[application.Id] = application;
        return this;
    }

    public Task<IReadOnlyList<Application>> DiscoverAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Application>>(Applications);

    public Task<IReadOnlyList<Application>> RefreshAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Application>>(Applications);

    public Task<Application?> GetAsync(string applicationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_applications.TryGetValue(applicationId, out var application) ? application : null);
}

internal sealed class StubProcessInspector : IProcessInspector
{
    internal IReadOnlyList<int> ByPath { get; set; } = Array.Empty<int>();

    internal IReadOnlyList<int> ByDirectory { get; set; } = Array.Empty<int>();

    internal IReadOnlyList<int> ByPackage { get; set; } = Array.Empty<int>();

    public IReadOnlyList<int> FindProcessesByPath(string executablePath, CancellationToken cancellationToken = default) => ByPath;

    public IReadOnlyList<int> FindProcessesByDirectory(string directory, CancellationToken cancellationToken = default) => ByDirectory;

    public IReadOnlyList<int> FindProcessesByPackage(string packageFamilyName, CancellationToken cancellationToken = default) => ByPackage;
}
