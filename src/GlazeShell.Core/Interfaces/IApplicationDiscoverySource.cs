using GlazeShell.Core.Discovery;

namespace GlazeShell.Core.Interfaces;

public interface IApplicationDiscoverySource
{
    string Name { get; }

    ApplicationDiscoveryResult Discover(CancellationToken cancellationToken = default);
}
