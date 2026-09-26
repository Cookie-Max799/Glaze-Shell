namespace GlazeShell.Core.Models;

public sealed record WindowInfo
{
    public WindowInfo(
        string id,
        string title,
        int processId,
        string? processName = null,
        string? executablePath = null,
        WindowType type = WindowType.Application,
        WindowState state = WindowState.None,
        bool isForeground = false,
        string? ownerId = null,
        bool canResize = true)
    {
        Id = ModelValidation.Required(id, nameof(id));
        ArgumentNullException.ThrowIfNull(title);
        ModelValidation.NonNegative(processId, nameof(processId));
        Title = title.Trim();
        ProcessId = processId;
        ProcessName = ModelValidation.Optional(processName, nameof(processName));
        ExecutablePath = ModelValidation.Optional(executablePath, nameof(executablePath));
        Type = type;
        State = state;
        IsForeground = isForeground;
        OwnerId = ModelValidation.Optional(ownerId, nameof(ownerId));
        CanResize = canResize;
    }

    public string Id { get; }

    public string Title { get; }

    public int ProcessId { get; }

    public string? ProcessName { get; }

    public string? ExecutablePath { get; }

    public WindowType Type { get; }

    public WindowState State { get; }

    public bool IsForeground { get; }

    public string? OwnerId { get; }

    public bool CanResize { get; }
}
