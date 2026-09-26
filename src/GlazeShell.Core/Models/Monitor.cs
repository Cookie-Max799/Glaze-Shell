namespace GlazeShell.Core.Models;

public enum MonitorOrientation
{
    None = 0,
    Landscape,
    Portrait,
    LandscapeFlipped,
    PortraitFlipped
}

public sealed record MonitorBounds
{
    public MonitorBounds(int x, int y, int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "The width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "The height must be greater than zero.");
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }
}
