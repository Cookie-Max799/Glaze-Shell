using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using GlazeShell.Core.Models;

namespace GlazeShell.App.Presentation;

public sealed class MonitorListItemViewModel : INotifyPropertyChanged
{
    private MonitorInfo _monitor;
    private string _defaultName;
    private string? _customName;

    public MonitorListItemViewModel(MonitorInfo monitor)
    {
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _defaultName = monitor.DeviceName;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MonitorInfo Monitor => _monitor;

    public ObservableCollection<WindowListItemViewModel> Windows { get; } = new();

    public string Id => _monitor.Id;

    /// <summary>Аппаратное имя устройства (например, «\\.\DISPLAY1») — уходит в подсказку и в сводку.</summary>
    public string DeviceName => _monitor.DeviceName;

    /// <summary>
    /// Отображаемое имя дисплея: пользовательское, а при его отсутствии — имя по умолчанию
    /// («MAIN DISPLAY» для основного, «DISPLAY N» для остальных).
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(_customName) ? _defaultName : _customName;

    public bool HasCustomName => !string.IsNullOrEmpty(_customName);

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

    /// <summary>
    /// Задаёт имя по умолчанию и пользовательское имя. Имя по умолчанию пересчитывается
    /// при изменении состава мониторов, пользовательское — приходит из настроек.
    /// </summary>
    public void SetNames(string defaultName, string? customName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultName);

        var changed = false;

        if (!string.Equals(_defaultName, defaultName, StringComparison.Ordinal))
        {
            _defaultName = defaultName;
            changed = true;
        }

        var normalizedCustom = string.IsNullOrWhiteSpace(customName) ? null : customName.Trim();

        if (!string.Equals(_customName, normalizedCustom, StringComparison.Ordinal))
        {
            _customName = normalizedCustom;
            changed = true;
        }

        if (changed)
        {
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(HasCustomName));
        }
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
