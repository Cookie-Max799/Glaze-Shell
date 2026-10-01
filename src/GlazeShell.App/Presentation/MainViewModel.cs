using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using GlazeShell.Core.Discovery;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Core.Services;
using Microsoft.UI.Dispatching;

namespace GlazeShell.App.Presentation;

public sealed class ApplicationListViewModel : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _typeBadge = string.Empty;
    private bool _isRunning;
    private bool _hasWindows;

    public ApplicationListViewModel(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        Application = application;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Application Application { get; }

    public ObservableCollection<WindowListItemViewModel> Windows { get; } = new();

    public string Id => Application.Id;

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => Set(ref _description, value);
    }

    public string TypeBadge
    {
        get => _typeBadge;
        set => Set(ref _typeBadge, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (Set(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(RunningGlyph));
                OnPropertyChanged(nameof(IsNotRunning));
            }
        }
    }

    public bool IsNotRunning => !_isRunning;

    public string RunningGlyph => _isRunning ? "●" : "○";

    public bool HasWindows
    {
        get => _hasWindows;
        private set => Set(ref _hasWindows, value);
    }

    public void Set(bool isRunning) => IsRunning = isRunning;

    public void SetWindows(IReadOnlyList<WindowInfo> windows, IWindowManager windowManager)
    {
        ArgumentNullException.ThrowIfNull(windowManager);

        Windows.Clear();

        foreach (var window in windows)
        {
            Windows.Add(new WindowListItemViewModel(window, windowManager));
        }

        HasWindows = Windows.Count > 0;
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    /// <summary>
    /// Пачка событий процессов (запуск приложения поднимает несколько процессов) объединяется
    /// в один пересчёт, и пересчитываются только те приложения, чей образ процесса совпал.
    /// </summary>
    private static readonly TimeSpan RunningRefreshDelay = TimeSpan.FromMilliseconds(250);

    private readonly IApplicationManager _applications;
    private readonly IApplicationLauncher _launcher;
    private readonly IWindowManager _windows;
    private readonly IDesktopManager _desktop;
    private readonly DispatcherQueue _dispatcher;
    private readonly IDisposable _displayChanged;
    private readonly IDisposable _processStarted;
    private readonly IDisposable _processExited;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly CancellationTokenSource _runningProbe = new();
    private readonly object _windowsRefreshGate = new();
    private readonly object _runningRefreshGate = new();
    private readonly HashSet<string> _pendingRunningProbe = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDisposable _windowOpened;
    private readonly IDisposable _windowClosed;
    private readonly IDisposable _foregroundChanged;
    private readonly IDisposable _windowStateChanged;
    private readonly EventCoalescer _runningRefresh;

    private IReadOnlyList<ApplicationListViewModel> _visible = Array.Empty<ApplicationListViewModel>();
    private IReadOnlyList<ApplicationListViewModel> _all = Array.Empty<ApplicationListViewModel>();
    private ApplicationListViewModel? _selected;
    private string _searchText = string.Empty;
    private string _statusText = "Готово к загрузке";
    private string _monitorSummary = "Мониторы не определены";
    private bool _isBusy;
    private bool _isLoaded;
    private bool _windowsRefreshQueued;

    public MainViewModel(
        IApplicationManager applications,
        IApplicationLauncher launcher,
        IWindowManager windows,
        IMonitorManager monitors,
        IDesktopManager desktop,
        IEventManager events,
        DispatcherQueue dispatcher)
    {
        _applications = applications ?? throw new ArgumentNullException(nameof(applications));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _desktop = desktop ?? throw new ArgumentNullException(nameof(desktop));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(monitors);
        _windowOpened = events.Subscribe<WindowOpened>(_ => QueueWindowsRefresh());
        _windowClosed = events.Subscribe<WindowClosed>(_ => QueueWindowsRefresh());
        _foregroundChanged = events.Subscribe<ForegroundWindowChanged>(_ => QueueWindowsRefresh());
        _windowStateChanged = events.Subscribe<WindowStateChanged>(_ => QueueWindowsRefresh());
        _displayChanged = events.Subscribe<DisplayChanged>(changed => ApplyMonitors(changed.Monitors));
        _processStarted = events.Subscribe<ProcessStarted>(changed => QueueRunningRefresh(changed.ExecutablePath, changed.ProcessName));
        _processExited = events.Subscribe<ProcessExited>(changed => QueueRunningRefresh(changed.ExecutablePath, changed.ProcessName));

        _runningRefresh = new EventCoalescer(
            RunningRefreshDelay,
            RefreshPendingRunningState,
            static exception => Debug.WriteLine($"GlazeShell.MainViewModel: {exception}"));

        RefreshCommand = new AsyncCommand(() => RefreshAsync());
        LaunchCommand = new AsyncCommand(LaunchSelectedAsync, () => Selected is not null);
        CloseCommand = new AsyncCommand(CloseSelectedAsync, () => Selected is not null);
        RestartCommand = new AsyncCommand(RestartSelectedAsync, () => Selected is not null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IDesktopManager Desktop => _desktop;

    public ObservableCollection<ApplicationListViewModel> Items { get; } = new();

    public ObservableCollection<MonitorListItemViewModel> Monitors { get; } = new();

    public AsyncCommand RefreshCommand { get; }

    public AsyncCommand LaunchCommand { get; }

    public AsyncCommand CloseCommand { get; }

    public AsyncCommand RestartCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value))
            {
                ApplyFilter(value);
            }
        }
    }

    public ApplicationListViewModel? Selected
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
            {
                RaiseActionState();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    public string MonitorSummary
    {
        get => _monitorSummary;
        private set => Set(ref _monitorSummary, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsIdle => !_isBusy;

    public bool IsLoaded
    {
        get => _isLoaded;
        private set => Set(ref _isLoaded, value);
    }

    public bool IsEmpty => Items.Count == 0;

    public void Initialize()
    {
        RefreshCommand.RaiseCanExecuteChanged();
        RaiseActionState();
    }

    private void RaiseActionState()
    {
        LaunchCommand.RaiseCanExecuteChanged();
        CloseCommand.RaiseCanExecuteChanged();
        RestartCommand.RaiseCanExecuteChanged();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(true);

        try
        {
            IsBusy = true;
            SetStatus("Сканирование установленных приложений…");

            var discovered = await _applications.RefreshAsync(cancellationToken).ConfigureAwait(true);

            ApplySnapshot(discovered);

            // Полный пересчёт состояния выполняется один раз после сканирования: он задаёт
            // базовую линию для приложений, запущенных до Glaze Shell. Дальше состояние
            // обновляется по событиям процессов, поэтому периодического опроса нет.
            await ProbeRunningStateAsync(_all, cancellationToken).ConfigureAwait(true);

            // Окна, открытые до запуска Glaze Shell, не порождают события, поэтому список
            // окон заполняется здесь, а не только по WindowOpened.
            RefreshWindows();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Сканирование отменено");
        }
        catch (Exception exception)
        {
            SetStatus($"Не удалось выполнить сканирование: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
            _refreshGate.Release();
        }
    }

    public async Task LaunchSelectedAsync()
    {
        var target = Selected;

        if (target is null)
        {
            return;
        }

        await RunAsync($"Запуск {target.Name}…", async token =>
        {
            var result = await _launcher.LaunchAsync(target.Id, token).ConfigureAwait(true);

            if (result.Started)
            {
                target.Set(true);
                SetStatus(result.ProcessId is { } pid ? $"Запущено, PID {pid}" : $"Запущено: {target.Name}");
            }
            else
            {
                SetStatus(result.Error ?? $"Не удалось запустить {target.Name}");
            }
        }).ConfigureAwait(true);
    }

    public async Task CloseSelectedAsync()
    {
        var target = Selected;

        if (target is null)
        {
            return;
        }

        await RunAsync($"Закрытие {target.Name}…", async token =>
        {
            var closed = await _launcher.CloseAsync(target.Id, token).ConfigureAwait(true);

            if (closed)
            {
                target.Set(false);
                SetStatus($"Закрыто: {target.Name}");
            }
            else
            {
                SetStatus($"Не удалось закрыть {target.Name}");
            }
        }).ConfigureAwait(true);
    }

    public async Task RestartSelectedAsync()
    {
        var target = Selected;

        if (target is null)
        {
            return;
        }

        await RunAsync($"Перезапуск {target.Name}…", async token =>
        {
            var result = await _launcher.RestartAsync(target.Id, token).ConfigureAwait(true);

            if (result.Started)
            {
                target.Set(true);
                SetStatus($"Перезапущено: {target.Name}");
            }
            else
            {
                SetStatus(result.Error ?? $"Не удалось перезапустить {target.Name}");
            }
        }).ConfigureAwait(true);
    }

    public void MoveSelection(int offset)
    {
        if (Items.Count == 0)
        {
            return;
        }

        var current = Selected is null ? -1 : Items.IndexOf(Selected);
        var next = Math.Clamp(current + offset, 0, Items.Count - 1);

        Selected = Items[next];
    }

    private async Task RunAsync(string progress, Func<CancellationToken, Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            SetStatus(progress);
            await action(_runningProbe.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Операция отменена");
        }
        catch (Exception exception)
        {
            SetStatus($"Ошибка: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ProbeRunningStateAsync(IReadOnlyList<ApplicationListViewModel> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var states = await Task.WhenAll(items.Select(item => ProbeAsync(item, cancellationToken))).ConfigureAwait(true);

        await _dispatcher.EnqueueAsync(() =>
        {
            for (var index = 0; index < items.Count; index++)
            {
                items[index].Set(states[index]);
            }
        }).ConfigureAwait(true);
    }

    private async Task<bool> ProbeAsync(ApplicationListViewModel item, CancellationToken cancellationToken)
    {
        try
        {
            return await _launcher.IsRunningAsync(item.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or InvalidOperationException or Win32Exception)
        {
            return item.IsRunning;
        }
    }

    /// <summary>
    /// Ставит в очередь адресный пересчёт состояния для приложений, которых касается
    /// событие процесса. События приходят пачками (запуск приложения поднимает несколько
    /// процессов), поэтому пересчёт объединяется и выполняется один раз.
    /// </summary>
    private void QueueRunningRefresh(string? executablePath, string? processName)
    {
        var targets = SelectRunningTargets(executablePath, processName);

        if (targets.Count == 0)
        {
            return;
        }

        lock (_runningRefreshGate)
        {
            foreach (var target in targets)
            {
                _pendingRunningProbe.Add(target);
            }
        }

        _runningRefresh.Request();
    }

    private async void RefreshPendingRunningState()
    {
        List<ApplicationListViewModel> pending;

        lock (_runningRefreshGate)
        {
            if (_pendingRunningProbe.Count == 0)
            {
                return;
            }

            var byId = _all.ToDictionary(static item => item.Id, StringComparer.OrdinalIgnoreCase);
            pending = [];

            foreach (var id in _pendingRunningProbe)
            {
                if (byId.TryGetValue(id, out var item))
                {
                    pending.Add(item);
                }
            }

            _pendingRunningProbe.Clear();
        }

        if (pending.Count == 0)
        {
            return;
        }

        try
        {
            await ProbeRunningStateAsync(pending, _runningProbe.Token).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            // Пересчёт идёт из потока таймера: неперехваченное исключение здесь завершило бы
            // процесс, поэтому ошибка показывается в строке состояния, а не пробрасывается.
            _ = _dispatcher.EnqueueAsync(() => SetStatus($"Не удалось обновить состояние процессов: {exception.Message}"));
        }
    }

    /// <summary>
    /// Выбирает приложения, которым принадлежит процесс. Сопоставление идёт по полному пути
    /// к исполняемому файлу, а при его отсутствии — по имени образа. Приложения MSIX без
    /// <see cref="Application.ExecutablePath"/> определяются по имени семейства пакета,
    /// извлечённому из пути процесса.
    /// </summary>
    private List<string> SelectRunningTargets(string? executablePath, string? processName)
    {
        var matches = new List<string>();

        if (string.IsNullOrWhiteSpace(executablePath) && string.IsNullOrWhiteSpace(processName))
        {
            return matches;
        }

        foreach (var item in _all)
        {
            if (MatchesProcess(item.Application, executablePath, processName))
            {
                matches.Add(item.Id);
            }
        }

        return matches;
    }

    private static bool MatchesProcess(Application application, string? executablePath, string? processName)
    {
        if (application.ExecutablePath is { } target)
        {
            if (!string.IsNullOrWhiteSpace(executablePath) && IsSamePath(target, executablePath))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(processName))
            {
                return false;
            }

            var imageName = Path.GetFileNameWithoutExtension(target);
            return !string.IsNullOrWhiteSpace(imageName)
                && string.Equals(imageName, processName, StringComparison.OrdinalIgnoreCase);
        }

        // У приложений MSIX нет ExecutablePath: пакет опознаётся по каталогу установки,
        // в котором живёт исполняемый файл процесса (WindowsApps\<PackageFullName>).
        return application.PackageFamilyName is { } packageFamilyName
            && PackageIdentity.IsPathOfPackage(executablePath, packageFamilyName);
    }

    private void ApplySnapshot(IReadOnlyList<Application> applications)
    {
        var previous = _all.ToDictionary(static item => item.Id, StringComparer.OrdinalIgnoreCase);
        var models = new List<ApplicationListViewModel>(applications.Count);

        foreach (var application in applications)
        {
            if (previous.TryGetValue(application.Id, out var existing))
            {
                existing.Name = application.Name;
                existing.Description = BuildDescription(application);
                existing.TypeBadge = BuildBadge(application);
                models.Add(existing);
                continue;
            }

            models.Add(new ApplicationListViewModel(application)
            {
                Name = application.Name,
                Description = BuildDescription(application),
                TypeBadge = BuildBadge(application),
            });
        }

        _all = models;
        IsLoaded = true;
        ApplyFilter(_searchText);
        SetStatus(models.Count == 0
            ? "Приложения не найдены"
            : $"Найдено приложений: {models.Count}");
    }

    private void ApplyFilter(string query)
    {
        var matches = Filter(_all, query);

        Items.Clear();

        foreach (var match in matches)
        {
            Items.Add(match);
        }

        OnPropertyChanged(nameof(IsEmpty));

        if (Items.Count > 0)
        {
            Selected ??= Items[0];
        }
        else
        {
            Selected = null;
        }
    }

    private static List<ApplicationListViewModel> Filter(IReadOnlyList<ApplicationListViewModel> source, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [.. source];
        }

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var matches = new List<ApplicationListViewModel>(source.Count);

        foreach (var item in source)
        {
            if (terms.All(term => Matches(item, term)))
            {
                matches.Add(item);
            }
        }

        return matches;
    }

    private static bool Matches(ApplicationListViewModel item, string term) =>
        item.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        item.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        item.TypeBadge.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        item.Application.ExecutablePath?.Contains(term, StringComparison.OrdinalIgnoreCase) is true ||
        item.Application.ApplicationUserModelId?.Contains(term, StringComparison.OrdinalIgnoreCase) is true;

    private static string BuildBadge(Application application) => application.Type switch
    {
        ApplicationType.Msix => "MSIX",
        ApplicationType.Win32 => "Win32",
        _ => application.Type.ToString(),
    };

    private static string BuildDescription(Application application)
    {
        if (!string.IsNullOrWhiteSpace(application.Description))
        {
            return application.Description;
        }

        if (!string.IsNullOrWhiteSpace(application.Publisher))
        {
            return application.Publisher;
        }

        if (!string.IsNullOrWhiteSpace(application.ExecutablePath))
        {
            return application.ExecutablePath;
        }

        return application.ApplicationUserModelId ?? application.PackageFamilyName ?? string.Empty;
    }

    private void SetStatus(string text) => StatusText = text;

    private void ApplyMonitors(IReadOnlyList<MonitorInfo> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);

        _ = _dispatcher.EnqueueAsync(() =>
        {
            MonitorSummary = BuildMonitorSummary(monitors);

            var previous = Monitors.ToDictionary(static item => item.Id, StringComparer.OrdinalIgnoreCase);
            Monitors.Clear();

            foreach (var monitor in monitors)
            {
                if (previous.TryGetValue(monitor.Id, out var existing))
                {
                    existing.Update(monitor);
                    Monitors.Add(existing);
                }
                else
                {
                    Monitors.Add(new MonitorListItemViewModel(monitor));
                }
            }
        });
    }

    private static string BuildMonitorSummary(IReadOnlyList<MonitorInfo> monitors)
    {
        if (monitors.Count == 0)
        {
            return "Мониторы не определены";
        }

        var primary = monitors.FirstOrDefault(monitor => monitor.IsPrimary);
        var primaryText = primary is null ? "нет основного" : $"{primary.Bounds.Width}×{primary.Bounds.Height}";
        var suffix = monitors.Count == 1 ? "монитор" : $"монитора: {monitors.Count}";

        return $"{suffix}, основной {primaryText}";
    }

    private void QueueWindowsRefresh()
    {
        lock (_windowsRefreshGate)
        {
            if (_windowsRefreshQueued)
            {
                return;
            }

            _windowsRefreshQueued = true;
        }

        _ = _dispatcher.EnqueueAsync(() =>
        {
            try
            {
                RefreshWindows();
            }
            finally
            {
                lock (_windowsRefreshGate)
                {
                    _windowsRefreshQueued = false;
                }
            }
        });
    }

    private void RefreshWindows()
    {
        IReadOnlyList<WindowInfo> windows;

        try
        {
            windows = _windows.GetWindows();
        }
        catch (Exception exception)
        {
            SetStatus($"Не удалось обновить список окон: {exception.Message}");
            return;
        }

        foreach (var application in _all)
        {
            var model = application.Application;
            WindowInfo[] matching;

            if (model.ExecutablePath is { } path)
            {
                matching = windows.Where(window => IsSamePath(path, window.ExecutablePath)).ToArray();
            }
            else if (model.PackageFamilyName is { } packageFamilyName)
            {
                matching = windows
                    .Where(window => PackageIdentity.IsPathOfPackage(window.ExecutablePath, packageFamilyName))
                    .ToArray();
            }
            else
            {
                matching = Array.Empty<WindowInfo>();
            }

            application.SetWindows(matching, _windows);
        }
    }

    private static bool IsSamePath(string expected, string? actual) =>
        !string.IsNullOrWhiteSpace(actual) &&
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        _windowOpened.Dispose();
        _windowClosed.Dispose();
        _foregroundChanged.Dispose();
        _windowStateChanged.Dispose();
        _displayChanged.Dispose();
        _processStarted.Dispose();
        _processExited.Dispose();
        _runningRefresh.Dispose();
        _runningProbe.Cancel();
        _runningProbe.Dispose();
        _refreshGate.Dispose();
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
