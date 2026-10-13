using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using GlazeShell.Core.Models;

namespace GlazeShell.App.Presentation;

public sealed class MonitorListItemViewModel : INotifyPropertyChanged
{
    private MonitorInfo _monitor;
    private double _mapLeft;
    private double _mapTop;
    private double _mapWidth;
    private double _mapHeight;

    public MonitorListItemViewModel(MonitorInfo monitor)
    {
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MonitorInfo Monitor => _monitor;

    public ObservableCollection<WindowListItemViewModel> Windows { get; } = new();

    public string Id => _monitor.Id;

    public string DeviceName => _monitor.DeviceName;

    public bool IsPrimary => _monitor.IsPrimary;

    public string Label => BuildLabel(_monitor);

    public string Summary => BuildSummary(_monitor);

    public string Badge => _monitor.IsPrimary ? "Основной" : "Дополнительный";

    public bool HasWindows => Windows.Count > 0;

    public string WindowCountText => Windows.Count switch
    {
        0 => "нет окон",
        1 => "1 окно",
        _ => $"{Windows.Count} окон",
    };

    /// <summary>Позиция монитора на схематичной карте (единицы холста).</summary>
    public double MapLeft => _mapLeft;

    public double MapTop => _mapTop;

    public double MapWidth => _mapWidth;

    public double MapHeight => _mapHeight;

    public void Update(MonitorInfo monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        _monitor = monitor;

        OnPropertyChanged(nameof(Monitor));
        OnPropertyChanged(nameof(DeviceName));
        OnPropertyChanged(nameof(IsPrimary));
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(Badge));
    }

    public void SetWindows(IReadOnlyList<WindowListItemViewModel> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);

        Windows.Clear();

        foreach (var window in windows)
        {
            Windows.Add(window);
        }

        OnPropertyChanged(nameof(HasWindows));
        OnPropertyChanged(nameof(WindowCountText));
    }

    public void SetPlacement(double left, double top, double width, double height)
    {
        _mapLeft = left;
        _mapTop = top;
        _mapWidth = width;
        _mapHeight = height;

        OnPropertyChanged(nameof(MapLeft));
        OnPropertyChanged(nameof(MapTop));
        OnPropertyChanged(nameof(MapWidth));
        OnPropertyChanged(nameof(MapHeight));
    }

    private static string BuildLabel(MonitorInfo monitor)
    {
        var scale = monitor.ScaleFactor == 1.0
            ? "100%"
            : $"{Math.Round(monitor.ScaleFactor * 100)}%";

        var label = new StringBuilder(monitor.DeviceName);

        if (monitor.RefreshRateHz is { } refreshRateHz)
        {
            label.Append(" · ").Append(refreshRateHz).Append(" Гц");
        }

        label.Append(" · ").Append(scale);
        return label.ToString();
    }

    private static string BuildSummary(MonitorInfo monitor)
    {
        var summary = $"{monitor.Bounds.Width}×{monitor.Bounds.Height}";

        if (monitor.WorkingArea is { } workingArea)
        {
            summary += $" (рабочая {workingArea.Width}×{workingArea.Height})";
        }

        if (monitor.Orientation != MonitorOrientation.None)
        {
            summary += $" · {OrientationText(monitor.Orientation)}";
        }

        return summary;
    }

    private static string OrientationText(MonitorOrientation orientation) => orientation switch
    {
        MonitorOrientation.Portrait => "книжная",
        MonitorOrientation.LandscapeFlipped => "перевернутая альбомная",
        MonitorOrientation.PortraitFlipped => "перевернутая книжная",
        _ => "альбомная",
    };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}