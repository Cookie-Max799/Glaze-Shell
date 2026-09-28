using GlazeShell.Core.Models;

namespace GlazeShell.Core.Interfaces;

public interface IApplicationManager
{
    Task<IReadOnlyList<Application>> DiscoverAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Application>> RefreshAsync(CancellationToken cancellationToken = default);

    Task<Application?> GetAsync(string applicationId, CancellationToken cancellationToken = default);
}
