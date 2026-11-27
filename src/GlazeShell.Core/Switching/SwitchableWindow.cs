namespace GlazeShell.Core.Switching;

public sealed record SwitchableWindow(
    long Handle,
    string Title,
    bool IsMinimized,
    string? ExecutablePath = null);
