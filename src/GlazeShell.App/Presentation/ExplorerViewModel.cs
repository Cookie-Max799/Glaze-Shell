using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using GlazeShell.Core.Files;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Windows.Applications;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Состояние окна Explorer (E1/E2): текущая папка, элементы, сайдбар и история навигации.
/// </summary>
/// <remarks>
/// Чтение каталога вынесено с потока интерфейса: папка с десятками тысяч элементов иначе
/// подвесила бы окно, а извлечение иконок оболочки дополнительно выполняется в фоне.
/// Вид списка и избранные папки хранятся в настройках, поэтому вид сохраняется между
/// запусками. Память вида по папке — следующий шаг E1.
/// </remarks>
public sealed class ExplorerViewModel : INotifyPropertyChanged
{
    private const int IconSize = 48;

    private readonly IFileSystemBrowser _browser;
    private readonly ISettingsManager _settings;
    private readonly DispatcherQueue _dispatcher;

    private readonly Stack<string> _back = new();
    private readonly Stack<string> _forward = new();
    private readonly Dictionary<string, ImageSource> _iconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _iconRequested = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FileSystemEntry> _entries = [];

    private string _currentPath = string.Empty;
    private string _statusText = "Выберите папку";
    private bool _isBusy;
    private FileSystemSortColumn _sortColumn = FileSystemSortColumn.Name;
    private FileSystemSortDirection _sortDirection = FileSystemSortDirection.Ascending;
    private ExplorerSortOption _selectedSort;
    private ExplorerViewOption _selectedView;
    private ExplorerViewMode _viewMode;

    public ExplorerViewModel(IFileSystemBrowser browser, ISettingsManager settings, DispatcherQueue dispatcher)
    {
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        _viewMode = _settings.GetSettings().ExplorerViewMode;
        _selectedSort = SortOptions[0];
        _selectedView = ViewOptions[0];

        foreach (var option in ViewOptions)
        {
            if (option.Mode == _viewMode)
            {
                _selectedView = option;
                break;
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ExplorerItemViewModel> Items { get; } = [];

    /// <summary>Системные и избранные папки сайдбара (E2).</summary>
    public ObservableCollection<ExplorerSidebarItemViewModel> Favorites { get; } = [];

    /// <summary>Логические диски сайдбара с заполненностью (E2).</summary>
    public ObservableCollection<ExplorerSidebarItemViewModel> Drives { get; } = [];

    public IReadOnlyList<ExplorerSortOption> SortOptions { get; } =
    [
        new(FileSystemSortColumn.Name, "Имя"),
        new(FileSystemSortColumn.Kind, "Тип"),
        new(FileSystemSortColumn.Size, "Размер"),
        new(FileSystemSortColumn.Modified, "Изменён"),
    ];

    public IReadOnlyList<ExplorerViewOption> ViewOptions { get; } =
    [
        new(ExplorerViewMode.Icons, "Иконки"),
        new(ExplorerViewMode.List, "Список"),
    ];

    public string CurrentPath => _currentPath;

    public string Title => _currentPath.Length == 0 ? "Компьютер" : GetDisplayName(_currentPath);

    public string Breadcrumb => _currentPath.Length == 0 ? "Диски и избранное" : _currentPath;

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
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    /// <summary>Показывать «папка пуста»: загрузка закончилась, а элементов нет.</summary>
    public bool IsEmpty => !_isBusy && Items.Count == 0;

    public Visibility IconsVisibility => _viewMode == ExplorerViewMode.Icons ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ListVisibility => _viewMode == ExplorerViewMode.List ? Visibility.Visible : Visibility.Collapsed;

    public bool CanGoBack => _back.Count > 0;

    public bool CanGoForward => _forward.Count > 0;

    public bool CanGoUp => GetParent(_currentPath) is not null;

    public ExplorerSortOption SelectedSort
    {
        get => _selectedSort;
        set
        {
            if (value is null || !Set(ref _selectedSort, value))
            {
                return;
            }

            _sortColumn = value.Column;
            _sortDirection = FileSystemSortDirection.Ascending;
            OnPropertyChanged(nameof(SortGlyph));
            OnPropertyChanged(nameof(SortToolTip));
            RebuildItems();
        }
    }

    public ExplorerViewOption SelectedView
    {
        get => _selectedView;
        set
        {
            if (value is null || !Set(ref _selectedView, value))
            {
                return;
            }

            SetViewMode(value.Mode);
        }
    }

    public string SortGlyph => _sortDirection == FileSystemSortDirection.Descending ? "\uE74B" : "\uE74A";

    public string SortToolTip => _sortDirection == FileSystemSortDirection.Descending
        ? "Сортировка по убыванию (нажмите, чтобы изменить)"
        : "Сортировка по возрастанию (нажмите, чтобы изменить)";

    /// <summary>
    /// Первый показ окна: строит сайдбар и открывает запрошенную папку (или домашнюю).
    /// </summary>
    public async Task InitializeAsync(string? path)
    {
        RefreshSidebar();

        var start = ResolveStartPath(path);

        if (start.Length == 0)
        {
            StatusText = "Не найдено ни одной доступной папки";
            return;
        }

        await NavigateAsync(start, pushHistory: false);
    }

    /// <summary>
    /// Открывает папку. Повторный переход в текущую папку игнорируется, а недоступный путь
    /// не открывается: иначе окно показало бы пустой список вместо содержимого.
    /// </summary>
    public async Task NavigateAsync(string path, bool pushHistory = true)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var normalized = NormalizePath(path);

        if (string.Equals(normalized, _currentPath, StringComparison.OrdinalIgnoreCase) ||
            !_browser.DirectoryExists(normalized))
        {
            return;
        }

        if (pushHistory && _currentPath.Length > 0)
        {
            _back.Push(_currentPath);
            _forward.Clear();
        }

        _currentPath = normalized;

        OnPropertyChanged(nameof(CurrentPath));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Breadcrumb));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
        OnPropertyChanged(nameof(CanGoUp));

        await LoadAsync();
    }

    public Task GoBackAsync()
    {
        if (_back.Count == 0)
        {
            return Task.CompletedTask;
        }

        var path = _back.Pop();

        if (_currentPath.Length > 0)
        {
            _forward.Push(_currentPath);
        }

        return NavigateAsync(path, pushHistory: false);
    }

    public Task GoForwardAsync()
    {
        if (_forward.Count == 0)
        {
            return Task.CompletedTask;
        }

        var path = _forward.Pop();

        if (_currentPath.Length > 0)
        {
            _back.Push(_currentPath);
        }

        return NavigateAsync(path, pushHistory: false);
    }

    public Task GoUpAsync() => GetParent(_currentPath) is { } parent ? NavigateAsync(parent) : Task.CompletedTask;

    public Task RefreshAsync() => LoadAsync();

    /// <summary>Сообщает в строке состояния, что стартовая папка не открылась.</summary>
    public void ReportInitializationFailure() => StatusText = "Не удалось открыть папку";

    /// <summary>Открывает элемент: папка — навигацией, файл — системным приложением по умолчанию.</summary>
    public async Task OpenItemAsync(ExplorerItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.IsFolder)
        {
            await NavigateAsync(item.FullPath);
            return;
        }

        ShellTargets.Open(item.FullPath);
    }

    /// <summary>Меняет направление сортировки, не перечитывая каталог.</summary>
    public void ToggleSortDirection()
    {
        _sortDirection = _sortDirection == FileSystemSortDirection.Descending
            ? FileSystemSortDirection.Ascending
            : FileSystemSortDirection.Descending;

        OnPropertyChanged(nameof(SortGlyph));
        OnPropertyChanged(nameof(SortToolTip));
        RebuildItems();
    }

    /// <summary>Добавляет папку в «Избранное» сайдбара и сохраняет список в настройках.</summary>
    public void AddFavorite(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !_browser.DirectoryExists(path))
        {
            return;
        }

        var normalized = NormalizePath(path);
        var settings = _settings.GetSettings();

        if (settings.ExplorerFavorites.Count >= UserSettings.MaxExplorerFavorites)
        {
            return;
        }

        foreach (var favorite in settings.ExplorerFavorites)
        {
            if (string.Equals(TrimRoot(favorite), TrimRoot(normalized), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        var updated = new List<string>(settings.ExplorerFavorites) { normalized };
        SaveExplorerState(updated, null);
        RefreshSidebar();
    }

    /// <summary>Убирает папку из «Избранного» и сохраняет список в настройках.</summary>
    public void RemoveFavorite(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var settings = _settings.GetSettings();
        var updated = new List<string>(settings.ExplorerFavorites.Count);

        foreach (var favorite in settings.ExplorerFavorites)
        {
            if (!string.Equals(TrimRoot(favorite), TrimRoot(path), StringComparison.OrdinalIgnoreCase))
            {
                updated.Add(favorite);
            }
        }

        if (updated.Count == settings.ExplorerFavorites.Count)
        {
            return;
        }

        SaveExplorerState(updated, null);
        RefreshSidebar();
    }

    /// <summary>
    /// Перестраивает сайдбар: системные папки, избранное пользователя (без дублей системных)
    /// и диски. Вызывается при первом показе и при активации окна — подключённый диск должен
    /// появиться без перезапуска.
    /// </summary>
    public void RefreshSidebar()
    {
        var systemFolders = _browser.GetSystemFolders();
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Favorites.Clear();

        foreach (var folder in systemFolders)
        {
            known.Add(TrimRoot(folder.Path));
            Favorites.Add(new ExplorerSidebarItemViewModel(folder.Name, folder.Path, GlyphFor(folder.Kind)));
        }

        foreach (var favorite in _settings.GetSettings().ExplorerFavorites)
        {
            if (!known.Add(TrimRoot(favorite)))
            {
                continue;
            }

            Favorites.Add(new ExplorerSidebarItemViewModel(GetDisplayName(favorite), favorite, "\uE8B7", isFavorite: true, toolTip: favorite));
        }

        Drives.Clear();

        foreach (var drive in _browser.GetDrives())
        {
            Drives.Add(CreateDriveItem(drive));
        }
    }

    private void SetViewMode(ExplorerViewMode mode)
    {
        if (_viewMode == mode)
        {
            return;
        }

        _viewMode = mode;

        OnPropertyChanged(nameof(IconsVisibility));
        OnPropertyChanged(nameof(ListVisibility));
        SaveExplorerState(null, mode);
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        var path = _currentPath;

        IReadOnlyList<FileSystemEntry> loaded;

        try
        {
            loaded = await Task.Run(() => _browser.GetEntries(path)).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.Explorer: reading '{path}' failed. {exception}");
            loaded = [];
        }

        // Пока каталог читался, пользователь мог уйти в другую папку: устаревший результат
        // не применяется, иначе список показал бы содержимое прежней папки.
        if (!string.Equals(path, _currentPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _entries.Clear();
        _entries.AddRange(loaded);

        IsBusy = false;
        RebuildItems();
    }

    private void RebuildItems()
    {
        var sorted = FileSystemSorter.Sort(_entries, _sortColumn, _sortDirection);

        Items.Clear();

        foreach (var entry in sorted)
        {
            Items.Add(CreateItem(entry));
        }

        OnPropertyChanged(nameof(IsEmpty));
        UpdateStatus();
        LoadIcons();
    }

    private ExplorerItemViewModel CreateItem(FileSystemEntry entry)
    {
        var item = new ExplorerItemViewModel(entry);

        if (_iconCache.TryGetValue(entry.FullPath, out var icon))
        {
            item.Icon = icon;
        }

        return item;
    }

    private void UpdateStatus()
    {
        if (Items.Count == 0)
        {
            StatusText = "Папка пуста";
            return;
        }

        var folders = 0;

        foreach (var item in Items)
        {
            if (item.IsFolder)
            {
                folders++;
            }
        }

        StatusText = $"Элементов: {Items.Count} · папок: {folders} · файлов: {Items.Count - folders}";
    }

    /// <summary>
    /// Загружает значки в фоне: извлечение значка системы — небыстрая операция, вынесенная
    /// с потока интерфейса. Готовый значок кэшируется по пути, а изображение создаётся
    /// на потоке интерфейса.
    /// </summary>
    private void LoadIcons()
    {
        foreach (var item in Items)
        {
            var path = item.FullPath;

            if (_iconCache.ContainsKey(path) || !_iconRequested.Add(path))
            {
                continue;
            }

            _ = Task.Run(() =>
            {
                var pixels = ShellIconProvider.TryGetIcon(path, IconSize);

                if (pixels is null)
                {
                    return;
                }

                _ = _dispatcher.TryEnqueue(() =>
                {
                    var source = IconBitmap.Create(pixels);

                    if (source is null)
                    {
                        return;
                    }

                    _iconCache[path] = source;

                    foreach (var current in Items)
                    {
                        if (string.Equals(current.FullPath, path, StringComparison.OrdinalIgnoreCase))
                        {
                            current.Icon = source;
                        }
                    }
                });
            });
        }
    }

    /// <summary>
    /// Сохраняет избранное и вид списка, копируя остальные поля настроек. Список полей
    /// дублируется намеренно: <see cref="UserSettings"/> — неизменяемая запись, и частичное
    /// обновление возможно только пересозданием.
    /// </summary>
    private void SaveExplorerState(IReadOnlyList<string>? favorites, ExplorerViewMode? viewMode)
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
            settings.DockPinnedApps,
            settings.DockReplacesTaskbar,
            favorites ?? settings.ExplorerFavorites,
            viewMode ?? settings.ExplorerViewMode));
    }

    private string ResolveStartPath(string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested) && _browser.DirectoryExists(requested))
        {
            return NormalizePath(requested);
        }

        var systemFolders = _browser.GetSystemFolders();

        foreach (var folder in systemFolders)
        {
            if (folder.Kind == SystemFolderKind.Home)
            {
                return folder.Path;
            }
        }

        foreach (var folder in systemFolders)
        {
            return folder.Path;
        }

        foreach (var drive in _browser.GetDrives())
        {
            return drive.RootPath;
        }

        return string.Empty;
    }

    private static ExplorerSidebarItemViewModel CreateDriveItem(DriveEntry drive)
    {
        var root = drive.RootPath.TrimEnd('\\');
        var name = string.IsNullOrWhiteSpace(drive.Label) ? root : $"{drive.Label} ({root})";

        return new ExplorerSidebarItemViewModel(
            name,
            drive.RootPath,
            "\uE8B7",
            usedFraction: drive.UsedFraction,
            toolTip: DescribeDrive(drive));
    }

    private static string DescribeDrive(DriveEntry drive)
    {
        if (drive.TotalBytes is not { } total || drive.FreeBytes is not { } free)
        {
            return drive.RootPath;
        }

        return $"{drive.RootPath} · свободно {ExplorerItemViewModel.FormatSize(free)} из {ExplorerItemViewModel.FormatSize(total)}";
    }

    private static string GlyphFor(SystemFolderKind kind) => kind switch
    {
        SystemFolderKind.Home => "\uE80F",
        SystemFolderKind.Downloads => "\uE896",
        _ => "\uE8B7",
    };

    /// <summary>Имя папки для заголовка: последний сегмент пути или сам путь для корня диска.</summary>
    internal static string GetDisplayName(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        var trimmed = path.TrimEnd('\\');

        if (trimmed.Length == 0)
        {
            return path;
        }

        if (trimmed.Length == 2 && trimmed[1] == ':')
        {
            return trimmed.ToUpperInvariant();
        }

        var index = trimmed.LastIndexOf('\\');

        return index >= 0 && index < trimmed.Length - 1 ? trimmed[(index + 1)..] : trimmed;
    }

    /// <summary>Родительская папка или <c>null</c>, если путь — корень диска (выше некуда).</summary>
    internal static string? GetParent(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        try
        {
            var parent = Path.GetDirectoryName(path);

            return string.IsNullOrEmpty(parent) ? null : parent;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// Убирает завершающий разделитель, но сохраняет его у корня диска (<c>C:\</c>): без
    /// разделителя корневой путь не открывается.
    /// </summary>
    private static string NormalizePath(string path)
    {
        var trimmed = path.Trim();

        return trimmed.Length > 3 ? trimmed.TrimEnd('\\') : trimmed;
    }

    /// <summary>Приводит путь к виду без завершающего разделителя для сравнения дублей.</summary>
    private static string TrimRoot(string path) => path.TrimEnd('\\');

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
