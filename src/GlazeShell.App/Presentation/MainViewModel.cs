using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using Microsoft.UI.Dispatching;

namespace GlazeShell.App.Presentation;

public sealed class ApplicationListViewModel : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _typeBadge = string.Empty;
    private bool _isRunning;

    public ApplicationListViewModel(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        Application = application;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Application Application { get; }

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

    public void Set(bool isRunning) => IsRunning = isRunning;

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
    private const int RunningPollIntervalSeconds = 3;

    private readonly IApplicationManager _applications;
    private readonly IApplicationLauncher _launcher;
    private readonly DispatcherQueue _dispatcher;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly CancellationTokenSource _runningProbe = new();
    private readonly Timer _runningTimer;

    private IReadOnlyList<ApplicationListViewModel> _visible = Array.Empty<ApplicationListViewModel>();
    private IReadOnlyList<ApplicationListViewModel> _all = Array.Empty<ApplicationListViewModel>();
    private ApplicationListViewModel? _selected;
    private string _searchText = string.Empty;
    private string _statusText = "Готово к загрузке";
    private bool _isBusy;
    private bool _isLoaded;

    public MainViewModel(IApplicationManager applications, IApplicationLauncher launcher, DispatcherQueue dispatcher)
    {
        _applications = applications ?? throw new ArgumentNullException(nameof(applications));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _runningTimer = new Timer(_ => _ = ProbeRunningStateAsync(_runningProbe.Token), null, Timeout.Infinite, Timeout.Infinite);

        RefreshCommand = new AsyncCommand(() => RefreshAsync());
        LaunchCommand = new AsyncCommand(LaunchSelectedAsync, () => Selected is not null);
        CloseCommand = new AsyncCommand(CloseSelectedAsync, () => Selected is not null);
        RestartCommand = new AsyncCommand(RestartSelectedAsync, () => Selected is not null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ApplicationListViewModel> Items { get; } = new();

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
            _runningTimer.Change(RunningPollIntervalSeconds * 1000, RunningPollIntervalSeconds * 1000);

            await ProbeRunningStateAsync(cancellationToken).ConfigureAwait(true);
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

    private async Task ProbeRunningStateAsync(CancellationToken cancellationToken)
    {
        var snapshot = _all;

        if (snapshot.Count == 0)
        {
            return;
        }

        var states = await Task.WhenAll(snapshot.Select(ProbeAsync)).ConfigureAwait(true);

        await _dispatcher.EnqueueAsync(() =>
        {
            for (var index = 0; index < snapshot.Count; index++)
            {
                snapshot[index].Set(states[index]);
            }
        }).ConfigureAwait(true);
    }

    private async Task<bool> ProbeAsync(ApplicationListViewModel item)
    {
        try
        {
            return await _launcher.IsRunningAsync(item.Id, _runningProbe.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or InvalidOperationException or Win32Exception)
        {
            return item.IsRunning;
        }
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

    public void Dispose()
    {
        _runningProbe.Cancel();
        _runningTimer.Dispose();
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
