namespace GlazeShell.Core.Configuration;

public sealed record GlazeShellConfiguration
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string ApplicationName { get; init; } = "Glaze Shell";

    public string DataDirectoryName { get; init; } = "GlazeShell";

    public static GlazeShellConfiguration CreateDefault() => new();
}
