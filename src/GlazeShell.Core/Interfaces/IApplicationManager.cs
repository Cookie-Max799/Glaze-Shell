using GlazeShell.Core.Models;

namespace GlazeShell.Core.Interfaces;

public interface IApplicationManager
{
    Task<IReadOnlyList<Application>> DiscoverAsync(CancellationToken cancellationToken = default);

    Task<Application?> GetAsync(string applicationId, CancellationToken cancellationToken = default);

    Task<bool> IsRunningAsync(string applicationId, CancellationToken cancellationToken = default);
}
