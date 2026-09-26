namespace GlazeShell.Core.Models;

public enum WindowType
{
    None = 0,
    Application,
    Dialog,
    System,
    Splash,
    Unknown
}

public enum WindowState
{
    None = 0,
    Normal,
    Minimized,
    Maximized,
    Restored
}
