using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using GlazeShell.Core.Discovery;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Core.Services;
using GlazeShell.Windows.Applications;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using CoreApplication = GlazeShell.Core.Models.Application;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.UI;

namespace GlazeShell.App.Presentation;

/// <summary>Вид плитки dock: элемент панели или разделитель между секциями.</summary>
public enum DockItemKind
{
    Start,
    Application,
    Folder,
    Trash,
    Separator,
}

/// <summary>
/// Состояние одной плитки dock (A1): имя, значок, признак запущенного приложения и
/// признак закрепления. Значок грузится асинхронно; пока он не получен, плитка показывает
/// букву на градиенте, поэтому панель не «мигает» пустотой.
/// </summary>
public sealed class DockItemViewModel : INotifyPropertyChanged
{
    private ImageSource? _icon;
    private bool _isRunning;
    private int _windowCount;

    public DockItemViewModel(
        DockItemKind kind,
        string key,
        string name,
        string? applicationId = null,
        string? executablePath = null,
        bool isPinned = false,
        string? glyph = null)
    {
        Kind = kind;
        Key = key;
        Name = name;
        ApplicationId = applicationId;
        ExecutablePath = executablePath;
        IsPinned = isPinned;
        Glyph = glyph;
        FallbackBrush = CreateFallbackBrush(key);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DockItemKind Kind { get; }

    /// <summary>Устойчивый ключ элемента: по нему обновляется состояние без пересоздания плитки.</summary>
    public string Key { get; }

    public string Name { get; }

    public string? ApplicationId { get; }

    /// <summary>Путь к файлу, из которого берётся значок: исполняемый файл или файл иконки.</summary>
    public string? ExecutablePath { get; }

    public bool IsPinned { get; }

    /// <summary>
    /// Глиф Segoe Fluent Icons для системных плиток (папка «Загрузки», корзина), если
    /// у элемента нет настоящей иконки.
    /// </summary>
    public string? Glyph { get; }

    public bool HasGlyph => !string.IsNullOrEmpty(Glyph);

    public bool IsStart => Kind == DockItemKind.Start;

    public bool IsFolder => Kind == DockItemKind.Folder;

    public bool IsTrash => Kind == DockItemKind.Trash;

    public bool IsSeparator => Kind == DockItemKind.Separator;

    public Visibility TileVisibility =>
        Kind == DockItemKind.Separator ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SeparatorVisibility =>
        Kind == DockItemKind.Separator ? Visibility.Visible : Visibility.Collapsed;

    public string Initial =>
        string.IsNullOrEmpty(Name) ? "?" : char.ToUpperInvariant(Name[0]).ToString();

    /// <summary>Запасной фон плитки: устойчивый пастельный градиент по ключу элемента.</summary>
    public Brush FallbackBrush { get; }

    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            if (!Set(ref _icon, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasIcon));
            OnPropertyChanged(nameof(HasPlate));
            OnPropertyChanged(nameof(ShowLetter));
        }
    }

    public bool HasIcon => _icon is not null;

    /// <summary>Показывать ли пастельную подложку плитки: она нужна, когда нет настоящей иконки.</summary>
    public bool HasPlate => _icon is null;

    /// <summary>Показывать ли букву-заглушку: только когда нет ни иконки, ни глифа.</summary>
    public bool ShowLetter => _icon is null && !HasGlyph;

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (Set(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(HasIndicator));
            }
        }
    }

    public bool HasIndicator => _isRunning;

    public int WindowCount
    {
        get => _windowCount;
        set
        {
            if (Set(ref _windowCount, value))
            {
                OnPropertyChanged(nameof(ToolTip));
            }
        }
    }

    public string ToolTip => _windowCount > 1 ? $"{Name} — окон: {_windowCount}" : Name;

    public string PinLabel => IsPinned ? "Открепить из Dock" : "Закрепить в Dock";

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

    private static LinearGradientBrush CreateFallbackBrush(string seed)
    {
        var hash = seed.GetHashCode() & 0x7FFFFFFF;
        var hue = (double)(hash % 360);

        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
        };

        brush.GradientStops.Add(new GradientStop { Color = FromHsl(hue, 0.58, 0.66), Offset = 0 });
        brush.GradientStops.Add(new GradientStop { Color = FromHsl((hue + 28) % 360, 0.58, 0.54), Offset = 1 });

        return brush;
    }

    private static Color FromHsl(double hue, double saturation, double lightness)
    {
        var c = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        var x = c * (1 - Math.Abs(((hue / 60) % 2) - 1));
        var m = lightness - (c / 2);

        var (r, g, b) = hue switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x),
        };

        return Color.FromArgb(
            255,
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}

/// <summary>
/// Состояние плавающего dock (A1, базовый слайс): плитка «Пуск», закреплённые приложения
/// и запущенные приложения с индикатором. Список закреплённых хранится в настройках,
/// запущенные определяются по окнам и обновляются с коалесцированием событий.
/// </summary>
public sealed class DockViewModel : IDisposable
{
    /// <summary>Задержка перед пересборкой списка запущенных: окна появляются пачкой.</summary>
    private static readonly TimeSpan RunningRefreshDelay = TimeSpan.FromMilliseconds(250);

    private const int IconSize = 64;

    private readonly IApplicationManager _applications;
    private readonly IApplicationLauncher _launcher;
    private readonly IWindowManager _windows;
    private readonly ISettingsManager _settings;
    private readonly DispatcherQueue _dispatcher;
    private readonly int _ownProcessId = Environment.ProcessId;

    private readonly IDisposable _windowOpened;
    private readonly IDisposable _windowClosed;
    private readonly IDisposable _windowStateChanged;
    private readonly EventCoalescer _runningRefresh;

    private readonly List<CoreApplication> _apps = [];
    private readonly Dictionary<string, CoreApplication> _appById = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _pinnedIds = [];
    private readonly HashSet<string> _pinnedSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _runningIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DockItemViewModel> _itemsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ImageSource> _iconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _iconRequested = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _primaryWindowByAppId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _windowCountByAppId = new(StringComparer.OrdinalIgnoreCase);

    private bool _initialized;

    public DockViewModel(
        IApplicationManager applications,
        IApplicationLauncher launcher,
        IWindowManager windows,
        ISettingsManager settings,
        IEventManager events,
        DispatcherQueue dispatcher)
    {
        _applications = applications ?? throw new ArgumentNullException(nameof(applications));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        ArgumentNullException.ThrowIfNull(events);

        _runningRefresh = new EventCoalescer(
            RunningRefreshDelay,
            QueueRebuild,
            EventCoalescer.FallbackErrorHandler("Dock"));

        _windowOpened = events.Subscribe<WindowOpened>(_ => _runningRefresh.Request());
        _windowClosed = events.Subscribe<WindowClosed>(_ => _runningRefresh.Request());
        _windowStateChanged = events.Subscribe<WindowStateChanged>(_ => _runningRefresh.Request());
    }

    /// <summary>Возникает при нажатии плитки «Пуск»: главное окно выводится на передний план.</summary>
    public event EventHandler? ActivateRequested;

    public ObservableCollection<DockItemViewModel> Items { get; } = [];

    /// <summary>
    /// Загружает список приложений и закреплений и строит плитки. Ошибка обнаружения не
    /// прерывает работу: dock остаётся с плиткой «Пуск» и обновится по событиям окон.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        try
        {
            // Главное окно запускает сканирование первым, поэтому dock сначала ждёт готового
            // снимка: повторное сканирование здесь означало бы второй полный обход источников.
            var discovered = await DiscoverQuietlyAsync();

            foreach (var application in discovered)
            {
                if (_appById.TryAdd(application.Id, application))
                {
                    _apps.Add(application);
                }
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.Dock: application discovery failed. {exception}");
        }

        ApplyPins(_settings.GetSettings().DockPinnedApps);
        Rebuild();
    }

    /// <summary>
    /// Обрабатывает нажатие плитки: «Пуск» выводит главное окно, запущенное приложение
    /// переключает на своё окно, а незапущенное — запускается.
    /// </summary>
    public async Task ActivateItemAsync(DockItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.IsStart)
        {
            ActivateRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (item.IsFolder)
        {
            OpenShellTarget("shell:Downloads");
            return;
        }

        if (item.IsTrash)
        {
            OpenShellTarget("shell:RecycleBinFolder");
            return;
        }

        if (item.ApplicationId is not { } applicationId)
        {
            return;
        }

        if (_runningIds.Contains(applicationId) &&
            _primaryWindowByAppId.TryGetValue(applicationId, out var windowId))
        {
            // Поведение панели задач: повторное нажатие по активному приложению
            // сворачивает его окно, а не оставляет фокус на месте.
            if (IsForegroundWindow(windowId))
            {
                _windows.MinimizeWindow(windowId);
                return;
            }

            if (_windows.FocusWindow(windowId))
            {
                return;
            }
        }

        try
        {
            await _launcher.LaunchAsync(applicationId);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.Dock: launching '{applicationId}' failed. {exception}");
        }

        _runningRefresh.Request();
    }

    /// <summary>Закрепляет или открепляет приложение и сохраняет список закреплённых.</summary>
    public void TogglePinned(DockItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.ApplicationId is not { } applicationId)
        {
            return;
        }

        if (item.IsPinned)
        {
            _pinnedIds.RemoveAll(id => string.Equals(id, applicationId, StringComparison.OrdinalIgnoreCase));
            _pinnedSet.Remove(applicationId);
        }
        else if (_pinnedIds.Count < UserSettings.MaxDockPinnedApps)
        {
            _pinnedIds.Add(applicationId);
            _pinnedSet.Add(applicationId);
        }
        else
        {
            return;
        }

        PersistPins();
        Rebuild();
    }

    /// <summary>
    /// Открывает системную цель оболочки (например, <c>shell:Downloads</c>) через ShellExecute.
    /// </summary>
    private static void OpenShellTarget(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.Dock: opening '{target}' failed. {exception}");
        }
    }

    /// <summary>
    /// Проверяет, что окно приложения активно. Сравнивается идентификатор окна, а не
    /// состояние снимка: активное окно могло смениться после последней пересборки.
    /// </summary>
    private bool IsForegroundWindow(string windowId)
    {
        try
        {
            var foreground = _windows.GetForegroundWindow();

            return foreground is not null &&
                string.Equals(foreground.Id, windowId, StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.Dock: reading the foreground window failed. {exception}");
            return false;
        }
    }

    public void Dispose()
    {
        _runningRefresh.Dispose();
        _windowOpened.Dispose();
        _windowClosed.Dispose();
        _windowStateChanged.Dispose();
    }

    /// <summary>
    /// Ждёт готовый снимок обнаружения. Главное окно запускает сканирование первым, и если
    /// dock запустил бы своё, источники обошлись бы дважды; поэтому здесь сначала читается
    /// готовый снимок, а пустой — перечитывается с коротким ожиданием.
    /// </summary>
    private async Task<IReadOnlyList<CoreApplication>> DiscoverQuietlyAsync()
    {
        const int maxAttempts = 15;
        var delay = TimeSpan.FromMilliseconds(300);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var snapshot = await _applications.DiscoverAsync();

            if (snapshot.Count > 0 || attempt == maxAttempts)
            {
                return snapshot;
            }

            await Task.Delay(delay);
        }

        return [];
    }

    private void ApplyPins(IReadOnlyList<string> pinned)
    {
        _pinnedIds.Clear();
        _pinnedSet.Clear();

        foreach (var id in pinned)
        {
            if (_pinnedIds.Count >= UserSettings.MaxDockPinnedApps)
            {
                break;
            }

            if (_pinnedSet.Add(id))
            {
                _pinnedIds.Add(id);
            }
        }
    }

    private void PersistPins()
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
            settings.ThemeMode,
            settings.AccentId,
            settings.TopBarEnabled,
            settings.DockEnabled,
            _pinnedIds,
            settings.DockReplacesTaskbar,
            settings.ExplorerFavorites,
            settings.ExplorerViewMode));
    }

    private void QueueRebuild() => _ = _dispatcher.TryEnqueue(Rebuild);

    private void Rebuild()
    {
        if (!_initialized)
        {
            return;
        }

        var running = ComputeRunning();
        var desired = BuildDesiredItems(running);

        if (!SameMembership(desired))
        {
            Items.Clear();

            foreach (var item in desired)
            {
                Items.Add(item);
            }
        }

        LoadIcons(desired);
    }

    /// <summary>
    /// Определяет запущенные приложения по окнам: собственный процесс, системные окна и
    /// окна без заголовка не учитываются. Приложение приводится к закреплённому или
    /// отдельному элементу по сопоставлению процесса и пути (см. <see cref="ApplicationProcessMatcher"/>).
    /// </summary>
    private List<CoreApplication> ComputeRunning()
    {
        _runningIds.Clear();
        _primaryWindowByAppId.Clear();
        _windowCountByAppId.Clear();

        IReadOnlyList<WindowInfo> all;

        try
        {
            all = _windows.GetWindows();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.Dock: window enumeration failed. {exception}");
            return [];
        }

        var candidates = new List<WindowInfo>(all.Count);

        foreach (var window in all)
        {
            if (window.ProcessId == _ownProcessId)
            {
                continue;
            }

            if (window.Type is WindowType.None or WindowType.System)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(window.Title))
            {
                continue;
            }

            candidates.Add(window);
        }

        var running = new List<CoreApplication>();
        var matchedWindowIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var application in _apps)
        {
            WindowInfo? primary = null;
            var count = 0;

            foreach (var window in candidates)
            {
                if (!ApplicationProcessMatcher.Matches(application, window.ExecutablePath, window.ProcessName))
                {
                    continue;
                }

                count++;
                primary ??= window;
                matchedWindowIds.Add(window.Id);
            }

            if (count == 0)
            {
                continue;
            }

            running.Add(application);
            _runningIds.Add(application.Id);
            _primaryWindowByAppId[application.Id] = primary!.Id;
            _windowCountByAppId[application.Id] = count;
        }

        AddUnknownRunningApplications(candidates, matchedWindowIds, running);

        return running;
    }

    /// <summary>
    /// Добавляет запущенные приложения, которых нет в результате обнаружения (портативные
    /// программы, распакованные архивы, приложения без ярлыка). Без этого dock заменял бы
    /// панель задач только для программ из меню «Пуск», а остальные окна не показывал бы.
    /// Группировка идёт по пути исполняемого файла, поэтому несколько окон одной программы
    /// получают одну плитку.
    /// </summary>
    private void AddUnknownRunningApplications(
        List<WindowInfo> candidates,
        HashSet<string> matchedWindowIds,
        List<CoreApplication> running)
    {
        var unknown = new Dictionary<string, UnknownApplication>(StringComparer.OrdinalIgnoreCase);

        foreach (var window in candidates)
        {
            if (matchedWindowIds.Contains(window.Id) || string.IsNullOrWhiteSpace(window.ExecutablePath))
            {
                continue;
            }

            if (unknown.TryGetValue(window.ExecutablePath, out var existing))
            {
                unknown[window.ExecutablePath] = existing with { Count = existing.Count + 1 };
                continue;
            }

            unknown[window.ExecutablePath] = new UnknownApplication(window, 1);
        }

        foreach (var (executablePath, entry) in unknown)
        {
            var id = $"running:{executablePath}";
            var name = Path.GetFileNameWithoutExtension(executablePath);

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var application = new CoreApplication(id, name, executablePath: executablePath);

            running.Add(application);
            _runningIds.Add(application.Id);
            _primaryWindowByAppId[application.Id] = entry.Primary.Id;
            _windowCountByAppId[application.Id] = entry.Count;
        }
    }

    private sealed record UnknownApplication(WindowInfo Primary, int Count);

    private List<DockItemViewModel> BuildDesiredItems(List<CoreApplication> running)
    {
        var desired = new List<DockItemViewModel> { GetOrCreateStart() };

        if (_pinnedIds.Count > 0)
        {
            desired.Add(GetOrCreateSeparator("sep-pinned"));

            foreach (var id in _pinnedIds)
            {
                if (_appById.TryGetValue(id, out var application))
                {
                    desired.Add(GetOrCreatePinned(application));
                }
            }
        }

        var runningOnly = running.FindAll(application => !_pinnedSet.Contains(application.Id));

        if (runningOnly.Count > 0)
        {
            desired.Add(GetOrCreateSeparator("sep-running"));

            foreach (var application in runningOnly)
            {
                desired.Add(GetOrCreateRunning(application));
            }
        }

        // Секция системных папок по DESIGN §7.5: папка «Загрузки» и корзина в конце dock.
        desired.Add(GetOrCreateSeparator("sep-folders"));
        desired.Add(GetOrCreateDownloads());
        desired.Add(GetOrCreateTrash());

        foreach (var item in desired)
        {
            if (item.ApplicationId is not { } applicationId)
            {
                continue;
            }

            item.IsRunning = _runningIds.Contains(applicationId);
            item.WindowCount = _windowCountByAppId.TryGetValue(applicationId, out var count) ? count : 0;
        }

        return desired;
    }

    private bool SameMembership(List<DockItemViewModel> desired)
    {
        if (Items.Count != desired.Count)
        {
            return false;
        }

        for (var i = 0; i < desired.Count; i++)
        {
            if (!string.Equals(Items[i].Key, desired[i].Key, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private DockItemViewModel GetOrCreateStart()
    {
        const string key = "start";

        return _itemsByKey.TryGetValue(key, out var existing)
            ? existing
            : Register(new DockItemViewModel(
                DockItemKind.Start,
                key,
                "Glaze Shell",
                executablePath: Environment.ProcessPath));
    }

    private DockItemViewModel GetOrCreateSeparator(string key) =>
        _itemsByKey.TryGetValue(key, out var existing)
            ? existing
            : Register(new DockItemViewModel(DockItemKind.Separator, key, string.Empty));

    /// <summary>Папка «Загрузки» — открывается в проводнике (DESIGN §7.5).</summary>
    private DockItemViewModel GetOrCreateDownloads()
    {
        const string key = "folder:downloads";

        return _itemsByKey.TryGetValue(key, out var existing)
            ? existing
            : Register(new DockItemViewModel(
                DockItemKind.Folder,
                key,
                "Загрузки",
                glyph: "\uE8B7"));
    }

    /// <summary>Корзина — открывает системную папку «Корзина» (DESIGN §7.5).</summary>
    private DockItemViewModel GetOrCreateTrash()
    {
        const string key = "trash";

        return _itemsByKey.TryGetValue(key, out var existing)
            ? existing
            : Register(new DockItemViewModel(
                DockItemKind.Trash,
                key,
                "Корзина",
                glyph: "\uE74D"));
    }

    private DockItemViewModel GetOrCreatePinned(CoreApplication application)
    {
        var key = $"pinned:{application.Id}";

        return _itemsByKey.TryGetValue(key, out var existing)
            ? existing
            : Register(new DockItemViewModel(
                DockItemKind.Application,
                key,
                application.Name,
                application.Id,
                ResolveIconPath(application),
                isPinned: true));
    }

    private DockItemViewModel GetOrCreateRunning(CoreApplication application)
    {
        var key = $"running:{application.Id}";

        return _itemsByKey.TryGetValue(key, out var existing)
            ? existing
            : Register(new DockItemViewModel(
                DockItemKind.Application,
                key,
                application.Name,
                application.Id,
                ResolveIconPath(application)));
    }

    private DockItemViewModel Register(DockItemViewModel item)
    {
        _itemsByKey[item.Key] = item;

        if (item.ExecutablePath is { Length: > 0 } path && _iconCache.TryGetValue(path, out var icon))
        {
            item.Icon = icon;
        }

        return item;
    }

    private static string? ResolveIconPath(CoreApplication application)
    {
        if (!string.IsNullOrEmpty(application.ExecutablePath))
        {
            return application.ExecutablePath;
        }

        return string.IsNullOrEmpty(application.IconPath) ? null : application.IconPath;
    }

    /// <summary>
    /// Загружает значки в фоне: извлечение значка системы — небыстрая операция, вынесенная
    /// с потока интерфейса. Готовый значок кэшируется по пути и применяется сразу, а
    /// готовое изображение создаётся на потоке интерфейса.
    /// </summary>
    private void LoadIcons(List<DockItemViewModel> desired)
    {
        foreach (var item in desired)
        {
            if (item.ExecutablePath is not { Length: > 0 } path)
            {
                continue;
            }

            if (_iconCache.ContainsKey(path) || !_iconRequested.Add(path))
            {
                continue;
            }

            var captured = path;

            _ = Task.Run(() =>
            {
                var pixels = ShellIconProvider.TryGetIcon(captured, IconSize);

                if (pixels is null)
                {
                    return;
                }

                _ = _dispatcher.TryEnqueue(() =>
                {
                    var source = CreateImageSource(pixels);

                    if (source is not null)
                    {
                        ApplyIcon(captured, source);
                    }
                });
            });
        }
    }

    private void ApplyIcon(string path, ImageSource source)
    {
        _iconCache[path] = source;

        foreach (var item in Items)
        {
            if (string.Equals(item.ExecutablePath, path, StringComparison.OrdinalIgnoreCase))
            {
                item.Icon = source;
            }
        }
    }

    /// <summary>
    /// Создаёт источник изображения из пикселей значка. Общий помощник с Explorer (E1):
    /// <c>WriteableBitmap</c> обязан создаваться на потоке интерфейса, поэтому создание
    /// вынесено в <see cref="IconBitmap"/>.
    /// </summary>
    private static WriteableBitmap? CreateImageSource(IconPixels pixels) => IconBitmap.Create(pixels);
}
