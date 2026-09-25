namespace GlazeShell.Infrastructure.Logging;

public sealed record LogEntry(
    DateTimeOffset Timestamp,
    GlazeLogLevel Level,
    string Component,
    string Message,
    string? Exception);
