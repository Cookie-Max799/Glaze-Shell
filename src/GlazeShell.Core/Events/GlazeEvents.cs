using GlazeShell.Core.Models;

namespace GlazeShell.Core.Events;

public abstract record GlazeEvent;

public sealed record WindowOpened : GlazeEvent
{
    public WindowOpened(WindowInfo window)
    {
        Window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public WindowInfo Window { get; }
}

public sealed record WindowClosed : GlazeEvent
{
    public WindowClosed(WindowInfo window)
    {
        Window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public WindowInfo Window { get; }
}

public sealed record ForegroundWindowChanged : GlazeEvent
{
    public ForegroundWindowChanged(WindowInfo? window)
    {
        Window = window;
    }

    public WindowInfo? Window { get; }
}

public sealed record WindowStateChanged : GlazeEvent
{
    public WindowStateChanged(WindowInfo window)
    {
        Window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public WindowInfo Window { get; }
}

public sealed record ProcessStarted : GlazeEvent
{
    public ProcessStarted(int processId, string processName, string? executablePath = null)
    {
        ModelValidation.NonNegative(processId, nameof(processId));
        ProcessId = processId;
        ProcessName = ModelValidation.Required(processName, nameof(processName));
        ExecutablePath = ModelValidation.Optional(executablePath, nameof(executablePath));
    }

    public int ProcessId { get; }

    public string ProcessName { get; }

    public string? ExecutablePath { get; }
}

public sealed record ProcessExited : GlazeEvent
{
    public ProcessExited(int processId, string? processName = null)
    {
        ModelValidation.NonNegative(processId, nameof(processId));
        ProcessId = processId;
        ProcessName = ModelValidation.Optional(processName, nameof(processName));
    }

    public int ProcessId { get; }

    public string? ProcessName { get; }
}

public sealed record DisplayChanged : GlazeEvent
{
    public DisplayChanged(IEnumerable<MonitorInfo> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        Monitors = ModelValidation.Copy(monitors, nameof(monitors));
    }

    public IReadOnlyList<MonitorInfo> Monitors { get; }
}

public sealed record DesktopChanged : GlazeEvent
{
    public DesktopChanged(DesktopLayout layout)
    {
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
    }

    public DesktopLayout Layout { get; }
}

public sealed record SettingsChanged : GlazeEvent
{
    public SettingsChanged(UserSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public UserSettings Settings { get; }
}

public sealed record ApplicationChanged : GlazeEvent
{
    public ApplicationChanged(IEnumerable<Application> applications)
    {
        ArgumentNullException.ThrowIfNull(applications);
        Applications = ModelValidation.Copy(applications, nameof(applications));
    }

    public IReadOnlyList<Application> Applications { get; }
}
