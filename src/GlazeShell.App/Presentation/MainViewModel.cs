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
    private string _path = string.Empty;
    private bool _isRunning;
    private bool _hasWindows;
    private bool _isSelected;
    private bool _isLaunching;

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

    /// <summary>
    /// Полный путь к исполняемому файлу (или идентификатор пакета). Отображается в списке
    /// в сокращённом виде, но копируется целиком кнопкой «Копировать».
    /// </summary>
    public string Path
    {
        get => _path;
        set
        {
            if (Set(ref _path, value))
            {
                OnPropertyChanged(nameof(HasPath));
            }
        }
    }

    public bool HasPath => _path.Length > 0;

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

    /// <summary>
    /// Выбран ли элемент в списке. Управляется из <see cref="MainViewModel.Selected"/>:
    /// так карточка получает яркое выделение и акцентный индикатор независимо от
    /// визуальных состояний <c>ListViewItem</c>.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    /// <summary>
    /// Приложение сейчас запускается: карточка показывает круговой индикатор вместо
    /// значка состояния.
    /// </summary>
    public bool IsLaunching
    {
        get => _isLaunching;
        set
        {
            if (Set(ref _isLaunching, value))
            {
                OnPropertyChanged(nameof(IsNotLaunching));
            }
        }
    }

    public bool IsNotLaunching => !_isLaunching;

    public void Set(bool isRunning) => IsRunning = isRunning;

    public void SetWindows(
        IReadOnlyList<WindowInfo> windows,
        IWindowManager windowManager,
        Func<string?, string?>? monitorName = null)
    {
        ArgumentNullException.ThrowIfNull(windowManager);

        Windows.Clear();

        foreach (var window in windows)
        {
            Windows.Add(new WindowListItemViewModel(window, windowManager, monitorName));
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

    /// <summary>
    /// Предельная длина отображаемого пути. Короткие пути выводятся целиком, длинные
    /// обрезаются по конец строки: так карточка не растягивается из-за глубоких каталогов
    /// и длинных имён пакетов MSIX.
    /// </summary>
    private const int MaxDisplayPathLength = 58;

    /// <summary>
    /// Предельная длина описания приложения в карточке. Описание из ярлыка часто длиннее,
    /// чем строка карточки, поэтому оно обрезается по конец троеточием.
    /// </summary>
    private const int MaxDisplayDescriptionLength = 80;

    private readonly IApplicationManager _applications;
    private readonly IApplicationLauncher _launcher;
    private readonly IWindowManager _windows;
    private readonly IMonitorManager _monitors;
    private readonly IDesktopManager _desktop;
    private readonly ISettingsManager _settings;
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
    private readonly IDisposable _settingsChanged;
    private readonly EventCoalescer _runningRefresh;

    private IReadOnlyList<ApplicationListViewModel> _visible = Array.Empty<ApplicationListViewModel>();
    private IReadOnlyList<ApplicationListViewModel> _all = Array.Empty<ApplicationListViewModel>();
    private IReadOnlyList<WindowInfo> _lastWindows = Array.Empty<WindowInfo>();
    private readonly Dictionary<string, string> _displayNames = new(StringComparer.OrdinalIgnoreCase);
    private ApplicationListViewModel? _selected;
    private string _searchText = string.Empty;
    private string _statusText = "Готово к загрузке";
    private string _monitorSummary = "Мониторы не определены";
    private bool _isBusy;
    private bool _isLoaded;
    private bool _windowsRefreshQueued;
    private ThemeMode _themeMode;
    private string? _accentId;
    private bool _topBarEnabled;
    private bool _dockEnabled;
    private bool _dockReplacesTaskbar;

    public MainViewModel(
        IApplicationManager applications,
        IApplicationLauncher launcher,
        IWindowManager windows,
        IMonitorManager monitors,
        IDesktopManager desktop,
        ISettingsManager settings,
        IEventManager events,
        DispatcherQueue dispatcher)
    {
        _applications = applications ?? throw new ArgumentNullException(nameof(applications));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _monitors = monitors ?? throw new ArgumentNullException(nameof(monitors));
        _desktop = desktop ?? throw new ArgumentNullException(nameof(desktop));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        ArgumentNullException.ThrowIfNull(events);
        _windowOpened = events.Subscribe<WindowOpened>(_ => QueueWindowsRefresh());
        _windowClosed = events.Subscribe<WindowClosed>(_ => QueueWindowsRefresh());
        _foregroundChanged = events.Subscribe<ForegroundWindowChanged>(_ => QueueWindowsRefresh());
        _windowStateChanged = events.Subscribe<WindowStateChanged>(_ => QueueWindowsRefresh());
        _displayChanged = events.Subscribe<DisplayChanged>(changed => ApplyMonitors(changed.Monitors));
        _processStarted = events.Subscribe<ProcessStarted>(changed => QueueRunningRefresh(changed.ExecutablePath, changed.ProcessName));
        _processExited = events.Subscribe<ProcessExited>(changed => QueueRunningRefresh(changed.ExecutablePath, changed.ProcessName));

        // Тему можно поменять и не из главного окна (popover «Центр управления», D1):
        // тогда переключатели здесь должны показать новое значение и перекрасить окно.
        _settingsChanged = events.Subscribe<SettingsChanged>(OnSettingsChanged);

        _runningRefresh = new EventCoalescer(
            RunningRefreshDelay,
            RefreshPendingRunningState,
            static exception => Debug.WriteLine($"GlazeShell.MainViewModel: {exception}"));

        RefreshCommand = new AsyncCommand(() => RefreshAsync());
        LaunchCommand = new AsyncCommand(LaunchSelectedAsync, () => Selected is not null);
        CloseCommand = new AsyncCommand(CloseSelectedAsync, () => Selected is not null);
        RestartCommand = new AsyncCommand(RestartSelectedAsync, () => Selected is not null);
        TerminateCommand = new AsyncCommand(TerminateSelectedAsync, () => Selected is not null);

        var initial = _settings.GetSettings();
        _themeMode = initial.ThemeMode;
        _accentId = initial.AccentId;
        _topBarEnabled = initial.TopBarEnabled;
        _dockEnabled = initial.DockEnabled;
        _dockReplacesTaskbar = initial.DockReplacesTaskbar;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Возникает, когда пользователь меняет тему или акцент оформления. Обработчик
    /// (окно) применяет новое оформление к визуальному дереву.
    /// </summary>
    public event EventHandler<ThemeSelectionChangedEventArgs>? ThemeSelectionChanged;

    public IDesktopManager Desktop => _desktop;

    public ObservableCollection<ApplicationListViewModel> Items { get; } = new();

    public ObservableCollection<MonitorListItemViewModel> Monitors { get; } = new();

    public AsyncCommand RefreshCommand { get; }

    public AsyncCommand LaunchCommand { get; }

    public AsyncCommand CloseCommand { get; }

    public AsyncCommand RestartCommand { get; }

    public AsyncCommand TerminateCommand { get; }

    /// <summary>
    /// Доступные режимы оформления для переключателя.
    /// </summary>
    public IReadOnlyList<ThemeModeOption> ThemeModeOptions { get; } = ThemeOptions.CreateThemeModes();

    /// <summary>
    /// Доступные акцентные цвета для переключателя.
    /// </summary>
    public IReadOnlyList<AccentOption> AccentOptions { get; } = ThemeOptions.CreateAccents();

    public ThemeModeOption SelectedThemeMode
    {
        get => ThemeModeOptions.FirstOrDefault(option => option.Mode == _themeMode) ?? ThemeModeOptions[0];
        set
        {
            if (value is not null && value.Mode != _themeMode)
            {
                _themeMode = value.Mode;
                ApplyThemeSelection();
            }
        }
    }

    public AccentOption SelectedAccent
    {
        get => AccentOptions.FirstOrDefault(option => string.Equals(option.Id, _accentId, StringComparison.OrdinalIgnoreCase)) ?? AccentOptions[0];
        set
        {
            if (value is not null && !string.Equals(value.Id, _accentId, StringComparison.OrdinalIgnoreCase))
            {
                _accentId = value.Id;
                ApplyThemeSelection();
            }
        }
    }

    /// <summary>
    /// Показывать ли плавающую верхнюю панель. Переключение сохраняется в настройках;
    /// окно верхней панели создаётся или закрывается по событию настроек.
    /// </summary>
    public bool IsTopBarEnabled
    {
        get => _topBarEnabled;
        set
        {
            if (value == _topBarEnabled)
            {
                return;
            }

            _topBarEnabled = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    /// <summary>
    /// Показывать ли плавающий dock (функция A1). Переключение сохраняется в настройках;
    /// окно dock создаётся или закрывается по событию настроек.
    /// </summary>
    public bool IsDockEnabled
    {
        get => _dockEnabled;
        set
        {
            if (value == _dockEnabled)
            {
                return;
            }

            _dockEnabled = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    /// <summary>
    /// Заменяет ли dock системную панель задач. Переключение сохраняется в настройках;
    /// окно dock открывается или закрывается независимо, а видимость панели задач
    /// применяется по событию настроек.
    /// </summary>
    public bool IsDockReplacesTaskbar
    {
        get => _dockReplacesTaskbar;
        set
        {
            if (value == _dockReplacesTaskbar)
            {
                return;
            }

            _dockReplacesTaskbar = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

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
            var previous = _selected;

            if (!Set(ref _selected, value))
            {
                return;
            }

            if (previous is not null)
            {
                previous.IsSelected = false;
            }

            value?.IsSelected = true;
            RaiseActionState();
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
        TerminateCommand.RaiseCanExecuteChanged();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(true);

        try
        {
            IsBusy = true;
            SetStatus("Сканирование установленных приложений…");

            var discovered = await Task.Run(
                () => _applications.RefreshAsync(cancellationToken),
                cancellationToken).ConfigureAwait(true);

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

        target.IsLaunching = true;

        try
        {
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
        finally
        {
            target.IsLaunching = false;
        }
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

            // Состояние берётся из фактического опроса, а не из флага закрытия: приложение
            // могло завершиться уже после таймаута ожидания (тогда флаг равен false), и точка
            // запуска иначе осталась бы светлой, хотя приложение закрыто.
            target.Set(await ProbeAsync(target, token).ConfigureAwait(true));

            if (closed)
            {
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

    public async Task TerminateSelectedAsync()
    {
        var target = Selected;

        if (target is null)
        {
            return;
        }

        await RunAsync($"Принудительное завершение {target.Name}…", async token =>
        {
            var terminated = await _launcher.TerminateAsync(target.Id, token).ConfigureAwait(true);

            target.Set(await ProbeAsync(target, token).ConfigureAwait(true));

            if (terminated)
            {
                SetStatus($"Завершено принудительно: {target.Name}");
            }
            else
            {
                SetStatus($"Не удалось завершить {target.Name}");
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

        var states = await Task.Run(
            () => Task.WhenAll(items.Select(item => ProbeAsync(item, cancellationToken))),
            cancellationToken).ConfigureAwait(true);

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
    /// к исполняемому файлу, каталогу установки (приложения-лаунчеры) и по имени образа.
    /// Приложения MSIX без <see cref="Application.ExecutablePath"/> определяются по имени
    /// семейства пакета, извлечённому из пути процесса.
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
            if (ApplicationProcessMatcher.Matches(item.Application, executablePath, processName))
            {
                matches.Add(item.Id);
            }
        }

        return matches;
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
                existing.Path = BuildPath(application);
                models.Add(existing);
                continue;
            }

            models.Add(new ApplicationListViewModel(application)
            {
                Name = application.Name,
                Description = BuildDescription(application),
                TypeBadge = BuildBadge(application),
                Path = BuildPath(application),
            });
        }

        _all = models;
        IsLoaded = true;
        ApplyFilter(_searchText);

        // Отметка времени делает результат обновления заметным даже тогда, когда список
        // не изменился: иначе повторное сканирование выглядело бы как «кнопка не работает».
        SetStatus(models.Count == 0
            ? $"Приложения не найдены · обновлено {DateTime.Now:HH:mm:ss}"
            : $"Найдено приложений: {models.Count} · обновлено {DateTime.Now:HH:mm:ss}");
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
            return ClampText(application.Description, MaxDisplayDescriptionLength);
        }

        if (!string.IsNullOrWhiteSpace(application.Publisher))
        {
            return ClampText(application.Publisher, MaxDisplayDescriptionLength);
        }

        return ShortenPath(BuildPath(application));
    }

    /// <summary>
    /// Обрезает текст до предельной длины троеточием в конце. Используется для описаний,
    /// которые не имеют смыслового «хвоста», в отличие от путей.
    /// </summary>
    private static string ClampText(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        return string.Concat(text.AsSpan(0, maxLength - 1), "…");
    }

    /// <summary>
    /// Полный путь запуска приложения: исполняемый файл, а при его отсутствии (MSIX) —
    /// идентификатор модели пользователя или имя семейства пакета.
    /// </summary>
    private static string BuildPath(Application application)
    {
        if (!string.IsNullOrWhiteSpace(application.ExecutablePath))
        {
            return application.ExecutablePath;
        }

        return application.ApplicationUserModelId ?? application.PackageFamilyName ?? string.Empty;
    }

    /// <summary>
    /// Сокращает абсолютный путь до вида «корень\первый каталог\…\последний каталог\файл»,
    /// оставляя видимыми начало и конец. Слишком длинный результат дополнительно обрезается
    /// по центру троеточием, а не-пути (например AUMID пакетов MSIX) возвращаются без изменений.
    /// </summary>
    private static string ShortenPath(string path)
    {
        if (path.Length == 0 || path.IndexOf('\\') < 0)
        {
            return ClampPath(path);
        }

        var segments = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);

        // Корень — диск («C:») для обычных путей или пара «server\share» для UNC.
        var root = path.StartsWith(@"\\", StringComparison.Ordinal) ? 2 : 1;
        var directories = segments.Length - root - 1;

        if (directories <= 2)
        {
            return ClampPath(path);
        }

        var prefix = path.StartsWith(@"\\", StringComparison.Ordinal)
            ? @"\\" + string.Join('\\', segments[..root])
            : string.Join('\\', segments[..root]);

        return ClampPath($"{prefix}\\{segments[root]}\\…\\{segments[^2]}\\{segments[^1]}");
    }

    /// <summary>
    /// Обрезает строку до предельной длины, сохраняя начало и конец и вставляя троеточие
    /// в середине. Нужна для глубоких каталогов и длинных имён пакетов, которые иначе
    /// растягивают карточку приложения.
    /// </summary>
    private static string ClampPath(string path)
    {
        if (path.Length <= MaxDisplayPathLength)
        {
            return path;
        }

        var head = (MaxDisplayPathLength - 1) / 2;
        var tail = MaxDisplayPathLength - 1 - head;
        return string.Concat(path.AsSpan(0, head), "…", path.AsSpan(path.Length - tail));
    }

    private void SetStatus(string text) => StatusText = text;

    /// <summary>Показывает в строке состояния, что путь приложения скопирован в буфер обмена.</summary>
    public void NotifyPathCopied(string path) => SetStatus($"Путь скопирован: {ShortenPath(path)}");

    private void ApplyMonitors(IReadOnlyList<MonitorInfo> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);

        _ = _dispatcher.EnqueueAsync(() =>
        {
            MonitorSummary = BuildMonitorSummary(monitors);

            var defaults = BuildDefaultMonitorNames(monitors);
            var custom = _settings.GetSettings().MonitorNames;

            var previous = Monitors.ToDictionary(static item => item.Id, StringComparer.OrdinalIgnoreCase);
            Monitors.Clear();
            _displayNames.Clear();

            foreach (var monitor in monitors)
            {
                var defaultName = defaults.TryGetValue(monitor.Id, out var name) ? name : monitor.DeviceName;
                custom.TryGetValue(monitor.Id, out var customName);

                if (previous.TryGetValue(monitor.Id, out var existing))
                {
                    existing.Update(monitor);
                    existing.SetNames(defaultName, customName);
                    Monitors.Add(existing);
                    _displayNames[monitor.Id] = existing.DisplayName;
                    continue;
                }

                var model = new MonitorListItemViewModel(monitor);
                model.SetNames(defaultName, customName);
                Monitors.Add(model);
                _displayNames[monitor.Id] = model.DisplayName;
            }

            AssignWindowsToMonitors(monitors);
        });
    }

    /// <summary>
    /// Имена дисплеев по умолчанию: основной получает «MAIN DISPLAY», остальные — «DISPLAY N»
    /// в порядке слева направо и сверху вниз. Пользователь может переименовать любой дисплей.
    /// </summary>
    private static Dictionary<string, string> BuildDefaultMonitorNames(IReadOnlyList<MonitorInfo> monitors)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ordered = monitors.OrderBy(static monitor => monitor.Bounds.X).ThenBy(static monitor => monitor.Bounds.Y);
        var number = 1;

        foreach (var monitor in ordered)
        {
            if (monitor.IsPrimary)
            {
                names[monitor.Id] = "MAIN DISPLAY";
            }
            else
            {
                number++;
                names[monitor.Id] = $"DISPLAY {number}";
            }
        }

        return names;
    }

    /// <summary>
    /// Переименовывает дисплей и сохраняет пользовательское имя в настройках. Пустое имя
    /// возвращает дисплей к имени по умолчанию.
    /// </summary>
    public void RenameMonitor(MonitorListItemViewModel monitor, string? name)
    {
        ArgumentNullException.ThrowIfNull(monitor);

        var settings = _settings.GetSettings();
        var names = new Dictionary<string, string>(settings.MonitorNames, StringComparer.OrdinalIgnoreCase);
        var normalized = string.IsNullOrWhiteSpace(name) ? null : name.Trim();

        if (normalized is null)
        {
            names.Remove(monitor.Id);
        }
        else
        {
            names[monitor.Id] = normalized;
        }

        _settings.SetSettings(new UserSettings(
            settings.StartWithWindows,
            settings.MinimizeToTray,
            settings.ShowDesktopOnStartup,
            settings.RestoreLastLayout,
            settings.Language,
            settings.ActiveThemeId,
            names,
            settings.ThemeMode,
            settings.AccentId,
            settings.TopBarEnabled,
            settings.DockEnabled,
            settings.DockPinnedApps,
            settings.DockReplacesTaskbar));

        monitor.SetNames(
            BuildDefaultMonitorNames(Monitors.Select(static item => item.Monitor).ToList()).TryGetValue(monitor.Id, out var fallback)
                ? fallback
                : monitor.DeviceName,
            normalized);

        _displayNames[monitor.Id] = monitor.DisplayName;
        SetStatus($"Дисплей переименован: {monitor.DisplayName}");

        // Подписи монитора у окон берутся из карты имён, поэтому список окон пересобирается.
        RefreshWindows();
    }

    /// <summary>
    /// Подхватывает тему и акцент, изменённые вне главного окна (popover «Центр управления»),
    /// и просит окно применить новое оформление.
    /// </summary>
    private void OnSettingsChanged(SettingsChanged changed)
    {
        _themeMode = changed.Settings.ThemeMode;
        _accentId = changed.Settings.AccentId;

        OnPropertyChanged(nameof(SelectedThemeMode));
        OnPropertyChanged(nameof(SelectedAccent));

        ThemeSelectionChanged?.Invoke(this, new ThemeSelectionChangedEventArgs(_themeMode, _accentId));
    }

    /// <summary>
    /// Сохраняет выбранную тему и акцент и уведомляет окно, чтобы оно применило оформление.
    /// </summary>
    private void ApplyThemeSelection()
    {
        SaveSettings();

        OnPropertyChanged(nameof(SelectedThemeMode));
        OnPropertyChanged(nameof(SelectedAccent));
        ThemeSelectionChanged?.Invoke(this, new ThemeSelectionChangedEventArgs(_themeMode, _accentId));
    }

    /// <summary>
    /// Сохраняет текущий выбор оформления, видимость верхней панели
    /// и видимость dock, сохраняя остальные
    /// поля загруженных настроек без изменений.
    /// </summary>
    private void SaveSettings()
    {
        var settings = _settings.GetSettings();

        _settings.SetSettings(new UserSettings(
            settings.StartWithWindows,
            settings.MinimizeToTray,
            settings.ShowDesktopOnStartup,
            settings.RestoreLastLayout,
            settings.Language,
            settings.ActiveThemeId,
            settings.MonitorNames,
            _themeMode,
            _accentId,
            _topBarEnabled,
            _dockEnabled,
            settings.DockPinnedApps,
            _dockReplacesTaskbar));
    }

    private static Func<string?, string?> BuildMonitorNameResolver(Dictionary<string, string> displayNames) =>
        monitorId => monitorId is not null && displayNames.TryGetValue(monitorId, out var name) ? name : null;

    /// <summary>
    /// Раскладывает окна по мониторам для панели мониторов: у каждого монитора
    /// показываются окна, находящиеся на нём.
    /// </summary>
    private void AssignWindowsToMonitors(IReadOnlyList<MonitorInfo> monitors)
    {
        var resolveMonitor = BuildMonitorNameResolver(_displayNames);

        foreach (var monitor in Monitors)
        {
            var matching = _lastWindows
                .Where(window => string.Equals(window.MonitorId, monitor.Id, StringComparison.OrdinalIgnoreCase))
                .Select(window => new WindowListItemViewModel(window, _windows, resolveMonitor))
                .ToArray();

            monitor.SetWindows(matching);
        }
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

        _lastWindows = windows;

        IReadOnlyList<MonitorInfo> monitors;

        try
        {
            monitors = _monitors.GetMonitors();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            monitors = Array.Empty<MonitorInfo>();
        }

        var resolveMonitor = BuildMonitorNameResolver(_displayNames);

        foreach (var application in _all)
        {
            var model = application.Application;

            var matching = windows
                .Where(window => ApplicationProcessMatcher.MatchesExecutable(model, window.ExecutablePath))
                .ToArray();

            application.SetWindows(matching, _windows, resolveMonitor);
        }

        AssignWindowsToMonitors(monitors);
    }

    public void Dispose()
    {
        _windowOpened.Dispose();
        _windowClosed.Dispose();
        _foregroundChanged.Dispose();
        _windowStateChanged.Dispose();
        _displayChanged.Dispose();
        _processStarted.Dispose();
        _processExited.Dispose();
        _settingsChanged.Dispose();
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
