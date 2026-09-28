using GlazeShell.Core.Models;

namespace GlazeShell.Core.Discovery;

public sealed record ApplicationDiscoveryOptions
{
    public TimeSpan CacheDuration { get; init; } = TimeSpan.FromMinutes(5);

    public bool RequireExistingTarget { get; init; } = true;

    public int MaxStartMenuDepth { get; init; } = 8;

    public int MaxWarningsPerSource { get; init; } = 25;

    public bool IncludeSystemApplications { get; init; } = true;

    public static ApplicationDiscoveryOptions Default { get; } = new();

    public ApplicationDiscoveryOptions Validate()
    {
        ModelValidation.NonNegative(CacheDuration, nameof(CacheDuration));
        ModelValidation.Positive(MaxStartMenuDepth, nameof(MaxStartMenuDepth));
        ModelValidation.Positive(MaxWarningsPerSource, nameof(MaxWarningsPerSource));

        return this;
    }
}
