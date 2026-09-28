using GlazeShell.Core.Discovery;

namespace GlazeShell.Core.Interfaces;

public interface IApplicationLauncher
{
    Task<ApplicationLaunchResult> LaunchAsync(string applicationId, CancellationToken cancellationToken = default);

    Task<bool> IsRunningAsync(string applicationId, CancellationToken cancellationToken = default);

    Task<bool> CloseAsync(string applicationId, CancellationToken cancellationToken = default);

    Task<ApplicationLaunchResult> RestartAsync(string applicationId, CancellationToken cancellationToken = default);
}
