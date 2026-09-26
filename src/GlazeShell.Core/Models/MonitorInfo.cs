namespace GlazeShell.Core.Models;

public sealed record MonitorInfo
{
    public MonitorInfo(
        string id,
        string deviceName,
        MonitorBounds bounds,
        MonitorBounds? workingArea = null,
        double scaleFactor = 1.0,
        bool isPrimary = false,
        bool isConnected = true,
        int? refreshRateHz = null,
        MonitorOrientation orientation = MonitorOrientation.None)
    {
        Id = ModelValidation.Required(id, nameof(id));
        DeviceName = ModelValidation.Required(deviceName, nameof(deviceName));
        ArgumentNullException.ThrowIfNull(bounds);
        ModelValidation.Positive(scaleFactor, nameof(scaleFactor));

        if (refreshRateHz is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(refreshRateHz), refreshRateHz, "The refresh rate must be greater than zero.");
        }

        Bounds = bounds;
        WorkingArea = workingArea;
        ScaleFactor = scaleFactor;
        IsPrimary = isPrimary;
        IsConnected = isConnected;
        RefreshRateHz = refreshRateHz;
        Orientation = orientation;
    }

    public string Id { get; }

    public string DeviceName { get; }

    public MonitorBounds Bounds { get; }

    public MonitorBounds? WorkingArea { get; }

    public double ScaleFactor { get; }

    public bool IsPrimary { get; }

    public bool IsConnected { get; }

    public int? RefreshRateHz { get; }

    public MonitorOrientation Orientation { get; }
}
