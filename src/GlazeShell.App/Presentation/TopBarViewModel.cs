using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Windows.Applications;
using GlazeShell.Windows.SystemStatus;
using Microsoft.UI.Dispatching;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Состояние верхней панели (A6): имя активного приложения, часы и индикаторы питания
/// и сети. Данные приложения приходят из события смены активного окна, часы и статус
/// обновляются раз в секунду.
/// </summary>
public sealed class TopBarViewModel : INotifyPropertyChanged, IDisposable
{
    /// <summary>Интервал обновления часов и индикаторов — 1 секунда (требование A6).</summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    private const string DefaultAppName = "Glaze Shell";

    private readonly IWindowManager _windows;
    private readonly DispatcherQueue _dispatcher;
    private readonly IDisposable _foregroundChanged;
    private readonly DispatcherQueueTimer _timer;
    private readonly int _ownProcessId = Environment.ProcessId;

    private string _appName = DefaultAppName;
    private string _appLocation = string.Empty;
    private string _clockTime = string.Empty;
    private string _clockDate = string.Empty;
    private string _batteryText = string.Empty;
    private string _batteryGlyph = "\uE83F";
    private string _networkGlyph = "\uE702";
    private string _networkText = "Нет сети";
    private string _searchText = string.Empty;
    private bool _hasBattery;
    private bool _isCharging;
    private bool _isOnline;

    public TopBarViewModel(
        IWindowManager windows,
        IEventManager events,
        DispatcherQueue dispatcher)
    {
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        ArgumentNullException.ThrowIfNull(events);

        _foregroundChanged = events.Subscribe<ForegroundWindowChanged>(OnForegroundChanged);

        _timer = _dispatcher.CreateTimer();
        _timer.Interval = RefreshInterval;
        _timer.Tick += (_, _) => RefreshClockAndStatus();
        _timer.Start();

        RefreshClockAndStatus();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string AppName
    {
        get => _appName;
        private set => Set(ref _appName, value);
    }

    public string AppLocation
    {
        get => _appLocation;
        private set
        {
            if (Set(ref _appLocation, value))
            {
                OnPropertyChanged(nameof(HasLocation));
            }
        }
    }

    public bool HasLocation => _appLocation.Length > 0;

    public string AppInitial =>
        string.IsNullOrEmpty(_appName) ? "?" : char.ToUpperInvariant(_appName[0]).ToString();

    public string ClockTime
    {
        get => _clockTime;
        private set => Set(ref _clockTime, value);
    }

    public string ClockDate
    {
        get => _clockDate;
        private set => Set(ref _clockDate, value);
    }

    public string BatteryText
    {
        get => _batteryText;
        private set => Set(ref _batteryText, value);
    }

    public string BatteryGlyph
    {
        get => _batteryGlyph;
        private set => Set(ref _batteryGlyph, value);
    }

    public string NetworkGlyph
    {
        get => _networkGlyph;
        private set => Set(ref _networkGlyph, value);
    }

    public string NetworkText
    {
        get => _networkText;
        private set => Set(ref _networkText, value);
    }

    public bool IsOnline
    {
        get => _isOnline;
        private set => Set(ref _isOnline, value);
    }

    public bool HasBattery
    {
        get => _hasBattery;
        private set => Set(ref _hasBattery, value);
    }

    public bool IsCharging
    {
        get => _isCharging;
        private set => Set(ref _isCharging, value);
    }

    /// <summary>Текст строки поиска. Глобальный поиск появится в функции B1; пока поле информативное.</summary>
    public string SearchText
    {
        get => _searchText;
        set => Set(ref _searchText, value);
    }

    public void Dispose()
    {
        _timer.Stop();
        _foregroundChanged.Dispose();
    }

    private void OnForegroundChanged(ForegroundWindowChanged changed)
    {
        // Событие приходит с потока трекера окон: обновление состояния панели
        // переносится на поток интерфейса.
        _ = _dispatcher.TryEnqueue(() => ApplyForeground(changed.Window));
    }

    private void ApplyForeground(WindowInfo? window)
    {
        if (window is null)
        {
            AppName = DefaultAppName;
            AppLocation = string.Empty;
            return;
        }

        // Окна самого Glaze Shell не должны подменять имя активного приложения:
        // трекер может прислать их, если пропуск собственного процесса выключен.
        if (window.ProcessId == _ownProcessId)
        {
            return;
        }

        // Событие приходит без сведений о процессе, поэтому имя уточняется по идентификатору.
        var resolved = window.ProcessName is null ? _windows.GetWindow(window.Id) ?? window : window;
        var name = ApplicationNameResolver.GetFriendlyName(resolved.ProcessId)
            ?? resolved.ProcessName
            ?? resolved.Title;

        AppName = string.IsNullOrWhiteSpace(name) ? DefaultAppName : name;

        var location = resolved.Title;
        AppLocation = string.Equals(location, AppName, StringComparison.OrdinalIgnoreCase) ? string.Empty : location;
    }

    private void RefreshClockAndStatus()
    {
        var now = DateTime.Now;
        ClockTime = now.ToString("HH:mm", CultureInfo.CurrentCulture);
        ClockDate = now.ToString("ddd, d MMM", CultureInfo.CurrentCulture);

        var power = SystemStatusProvider.GetPowerStatus();
        HasBattery = power.HasBattery;
        IsCharging = power.IsCharging;
        BatteryGlyph = power.IsCharging ? "\uE83E" : "\uE83F";
        BatteryText = power.HasBattery
            ? string.Create(CultureInfo.CurrentCulture, $"{power.BatteryPercent}%")
            : "Сеть";

        var online = SystemStatusProvider.IsNetworkAvailable();
        IsOnline = online;
        NetworkGlyph = online ? "\uE701" : "\uE702";
        NetworkText = online ? "В сети" : "Нет сети";
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);

        if (propertyName == nameof(AppName))
        {
            OnPropertyChanged(nameof(AppInitial));
        }

        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
