using System.Globalization;

namespace GlazeShell.Core.Discovery;

public sealed record ApplicationLaunchResult
{
    private ApplicationLaunchResult(bool started, int? processId, string? error)
    {
        Started = started;
        ProcessId = processId;
        Error = error;
    }

    public bool Started { get; }

    public int? ProcessId { get; }

    public string? Error { get; }

    public static ApplicationLaunchResult Success(int? processId) => new(true, processId, null);

    public static ApplicationLaunchResult Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new(false, null, error);
    }

    public override string ToString() =>
        Started
            ? string.Create(CultureInfo.InvariantCulture, $"Started (pid {ProcessId?.ToString(CultureInfo.InvariantCulture) ?? "unknown"})")
            : string.Create(CultureInfo.InvariantCulture, $"Failed: {Error}");
}
