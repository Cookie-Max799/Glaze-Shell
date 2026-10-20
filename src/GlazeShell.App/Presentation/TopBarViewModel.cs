using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using GlazeShell.Core.Events;
using GlazeShell.Core.Glance;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Windows.Applications;
using GlazeShell.Windows.Audio;
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

    /// <summary>Сколько результатов Glance показывается одновременно (B1).</summary>
    private const int MaxResults = 8;

    private const string DefaultAppName = "Glaze Shell";

    private readonly IWindowManager _windows;
    private readonly IApplicationLauncher _launcher;
    private readonly GlanceSearchCatalog _catalog;
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
    private GlanceResultViewModel? _selectedResult;
    private bool _hasBattery;
    private bool _isCharging;
    private bool _isOnline;

    public TopBarViewModel(
        IWindowManager windows,
        IApplicationLauncher launcher,
        GlanceSearchCatalog catalog,
        ISettingsManager settings,
        IEventManager events,
        DispatcherQueue dispatcher)
    {
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(events);

        QuickSettings = new QuickSettingsViewModel(settings, new SystemVolumeProvider());

        _foregroundChanged = events.Subscribe<ForegroundWindowChanged>(OnForegroundChanged);

        _timer = _dispatcher.CreateTimer();
        _timer.Interval = RefreshInterval;
        _timer.Tick += (_, _) =>
        {
            RefreshClockAndStatus();
            QuickSettings.RefreshVolume();
        };

        _timer.Start();

        RefreshClockAndStatus();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Состояние popover «Центр управления» (D1).</summary>
    public QuickSettingsViewModel QuickSettings { get; }

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

    /// <summary>
    /// Текст строки поиска Glance (B1). Изменение запускает ранжирование кандидатов;
    /// результаты появляются в <see cref="Results"/>.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value))
            {
                return;
            }

            // Поиск и «Центр управления» не показываются одновременно: они занимают
            // одну и ту же полосу под панелью.
            if (HasQuery)
            {
                QuickSettings.IsOpen = false;
            }

            OnPropertyChanged(nameof(HasQuery));
            _ = UpdateResultsAsync();
        }
    }

    /// <summary>Результаты поиска в порядке убывания релевантности.</summary>
    public ObservableCollection<GlanceResultViewModel> Results { get; } = [];

    /// <summary>Выбранная клавиатурой строка результата.</summary>
    public GlanceResultViewModel? SelectedResult
    {
        get => _selectedResult;
        set => Set(ref _selectedResult, value);
    }

    /// <summary>Есть ли результаты: панель результатов показывается только при них.</summary>
    public bool HasResults => Results.Count > 0;

    /// <summary>В строке поиска есть запрос (не только пробелы).</summary>
    public bool HasQuery => !string.IsNullOrWhiteSpace(_searchText);

    /// <summary>Показывать «ничего не найдено»: запрос есть, список просканирован, совпадений нет.</summary>
    public bool ShowEmptyState => HasQuery && !HasResults && _catalog.IsApplicationListLoaded;

    /// <summary>
    /// Просьба вывести главное окно Glaze Shell (команда <c>open-shell</c>). Окно
    /// верхней панели не владеет главным окном, поэтому переключает его владелец —
    /// приложение.
    /// </summary>
    public event EventHandler? ActivateRequested;

    /// <summary>
    /// Просьба открыть проводник Glaze Shell (команда <c>open-explorer</c>); аргумент — папка,
    /// которую нужно показать, или <c>null</c> для папки по умолчанию. Окном Explorer владеет
    /// приложение, поэтому панель только сообщает о желании.
    /// </summary>
    public event EventHandler<string?>? ExplorerRequested;

    /// <summary>Очищает строку поиска вместе с панелью результатов.</summary>
    public void ClearSearch() => SearchText = string.Empty;

    /// <summary>
    /// Открывает или закрывает popover «Центр управления» (D1). Открытие закрывает поиск:
    /// обе панели живут под верхней панелью в одном месте.
    /// </summary>
    public void ToggleQuickSettings()
    {
        var open = !QuickSettings.IsOpen;
        ClearSearch();
        QuickSettings.IsOpen = open;
    }

    /// <summary>Закрывает popover «Центр управления», не трогая поиск.</summary>
    public void CloseQuickSettings() => QuickSettings.IsOpen = false;

    /// <summary>Сдвигает выбор по списку результатов на <paramref name="offset"/> позиций.</summary>
    public void MoveSelection(int offset)
    {
        if (Results.Count == 0)
        {
            return;
        }

        var current = SelectedResult is null ? -1 : Results.IndexOf(SelectedResult);
        var next = Math.Clamp(current + offset, 0, Results.Count - 1);
        SelectedResult = Results[next];
    }

    /// <summary>
    /// Открывает выбранный результат: запускает приложение, переключает окно, открывает
    /// страницу настроек или выполняет команду. Строка поиска очищается, панель скрывается.
    /// </summary>
    public async Task ActivateSelectedAsync()
    {
        if (SelectedResult is not { } selected)
        {
            return;
        }

        var result = selected.Result;
        ClearSearch();

        try
        {
            switch (result.Kind)
            {
                case GlanceResultKind.Application:
                    await _launcher.LaunchAsync(result.Id);
                    break;

                case GlanceResultKind.Window:
                    _windows.FocusWindow(result.Id);
                    break;

                case GlanceResultKind.Setting:
                    OpenShellTarget(result.Id);
                    break;

                case GlanceResultKind.Command:
                    await RunCommandAsync(result.Id);
                    break;

                default:
                    break;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.Glance: opening '{result.Id}' failed. {exception}");
        }
    }

    private async Task UpdateResultsAsync()
    {
        var query = _searchText;

        if (string.IsNullOrWhiteSpace(query))
        {
            ApplyResults([]);
            return;
        }

        // Приложения сканируются лениво при первом запросе; повторные запросы ждут тот же
        // проход, поэтому поиск не дублирует сканирование меню «Пуск».
        if (!_catalog.IsApplicationListLoaded)
        {
            try
            {
                await _catalog.EnsureApplicationsAsync();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                Trace.WriteLine($"GlazeShell.Glance: application discovery failed. {exception}");
            }
        }

        // Пока грузились приложения, запрос мог смениться: устаревший результат не применяем.
        if (!string.Equals(query, _searchText, StringComparison.Ordinal))
        {
            return;
        }

        var ranked = GlanceRanker.Rank(_catalog.BuildCandidates(), query, MaxResults);

        ApplyResults(ranked.Select(result => new GlanceResultViewModel(result)));
    }

    private void ApplyResults(IEnumerable<GlanceResultViewModel> results)
    {
        Results.Clear();

        foreach (var result in results)
        {
            Results.Add(result);
        }

        SelectedResult = Results.Count > 0 ? Results[0] : null;
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private async Task RunCommandAsync(string commandId)
    {
        switch (commandId)
        {
            case "open-shell":
                ActivateRequested?.Invoke(this, EventArgs.Empty);
                break;

            case "open-explorer":
                ExplorerRequested?.Invoke(this, null);
                break;

            case "refresh-apps":
                await _catalog.RefreshApplicationsAsync();
                break;

            case "open-downloads":
                OpenShellTarget("shell:Downloads");
                break;

            case "open-recycle-bin":
                OpenShellTarget("shell:RecycleBinFolder");
                break;

            default:
                break;
        }
    }

    /// <summary>Открывает системную цель оболочки (например, <c>ms-settings:display</c>) через ShellExecute.</summary>
    private static void OpenShellTarget(string target) => ShellTargets.Open(target);

    public void Dispose()
    {
        _timer.Stop();
        _foregroundChanged.Dispose();
        QuickSettings.Dispose();
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
