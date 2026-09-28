using GlazeShell.Core.Models;

namespace GlazeShell.Core.Discovery;

public sealed record ApplicationDiscoveryResult
{
    public ApplicationDiscoveryResult(
        IEnumerable<ApplicationCandidate> candidates,
        IEnumerable<string>? warnings = null,
        int skippedCount = 0)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        Candidates = ModelValidation.Copy(candidates, nameof(candidates));
        Warnings = ModelValidation.Copy(warnings, nameof(warnings));
        SkippedCount = skippedCount;
        ModelValidation.NonNegative(SkippedCount, nameof(skippedCount));
    }

    public IReadOnlyList<ApplicationCandidate> Candidates { get; }

    public IReadOnlyList<string> Warnings { get; }

    public int SkippedCount { get; }

    public static ApplicationDiscoveryResult Empty { get; } = new(Array.Empty<ApplicationCandidate>());
}
