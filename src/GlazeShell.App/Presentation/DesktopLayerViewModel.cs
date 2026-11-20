using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Windows.Applications;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.UI;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Состояние одного значка на безопасном слое рабочего стола Glaze: позиция, подпись,
/// иконка и путь, который открывается по щелчку. Значок грузится асинхронно через
/// <see cref="ShellIconProvider"/>, как в dock (A1): пока иконки нет, видна буква на
/// пастельном градиенте, поэтому слой не «мигает» пустотой.
/// </summary>
public sealed class DesktopIconViewModel : INotifyPropertyChanged
{
    private ImageSource? _icon;
    private string _name;
    private string? _executablePath;

    public DesktopIconViewModel(
        string id,
        string tabId,
        string name,
        string? path,
        double x,
        double y)
    {
        Id = id;
        TabId = tabId;
        _name = name;
        Path = path;
        X = x;
        Y = y;
        FallbackBrush = CreateFallbackBrush(id);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }

    /// <summary>Вкладка layout, которой принадлежит значок: нужна для удаления.</summary>
    public string TabId { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (string.Equals(_name, value, StringComparison.Ordinal))
            {
                return;
            }

            _name = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Initial));
        }
    }

    /// <summary>Путь к файлу или идентификатор приложения Glaze.</summary>
    public string? Path { get; }

    /// <summary>
    /// Путь к исполняемому файлу приложения: нужен для «расположения файла», «свойств» и
    /// копирования пути. Заполняется асинхронно после поиска приложения.
    /// </summary>
    public string? ExecutablePath
    {
        get => _executablePath;
        set
        {
            if (string.Equals(_executablePath, value, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _executablePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasExecutablePath));
        }
    }

    public bool HasExecutablePath => _executablePath is { Length: > 0 };

    /// <summary>Позиция в DIP относительно левого верхнего угла рабочей области.</summary>
    public double X { get; }

    public double Y { get; }

    public string Initial =>
        string.IsNullOrEmpty(Name) ? "?" : char.ToUpperInvariant(Name[0]).ToString();

    /// <summary>Запасной фон плитки: устойчивый пастельный градиент по ключу элемента.</summary>
    public Brush FallbackBrush { get; }

    public bool HasIcon => Icon is not null;

    /// <summary>Показывает букву-заполнителю, пока иконка не загружена.</summary>
    public bool ShowPlaceholder => !HasIcon;

    public Microsoft.UI.Xaml.Visibility PlaceholderVisibility =>
        HasIcon ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

    public Microsoft.UI.Xaml.Visibility IconVisibility =>
        HasIcon ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            if (ReferenceEquals(_icon, value))
            {
                return;
            }

            _icon = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasIcon));
            OnPropertyChanged(nameof(ShowPlaceholder));
            OnPropertyChanged(nameof(PlaceholderVisibility));
            OnPropertyChanged(nameof(IconVisibility));
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Устойчивый пастельный градиент по ключу, как у плиток dock без иконки.</summary>
    private static LinearGradientBrush CreateFallbackBrush(string key)
    {
        var hash = 0;

        foreach (var character in key)
        {
            hash = (hash * 31) + character;
        }

        var hue = (hash % 360 + 360) % 360;
        var from = ColorFromHsv(hue, 0.45, 0.62);
        var to = ColorFromHsv(hue + 24, 0.55, 0.48);

        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
        };

        brush.GradientStops.Add(new GradientStop { Color = from, Offset = 0 });
        brush.GradientStops.Add(new GradientStop { Color = to, Offset = 1 });
        return brush;
    }

    private static Color ColorFromHsv(double hue, double saturation, double value)
    {
        var chroma = value * saturation;
        var sector = hue / 60;
        var x = chroma * (1 - Math.Abs(sector % 2 - 1));
        var (r, g, b) = ((int)sector % 6) switch
        {
            0 => (chroma, x, 0.0),
            1 => (x, chroma, 0.0),
            2 => (0.0, chroma, x),
            3 => (0.0, x, chroma),
            4 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x),
        };

        var offset = value - chroma;
        return Color.FromArgb(
            255,
            (byte)Math.Round((r + offset) * 255),
            (byte)Math.Round((g + offset) * 255),
            (byte)Math.Round((b + offset) * 255));
    }
}

/// <summary>
/// Представление безопасного слоя рабочего стола Glaze. Читает активную вкладку
/// <see cref="IDesktopManager"/> и показывает её приложения значками; перестраивается по
/// событию <see cref="DesktopChanged"/>. Позиции вычисляются сеткой сверху вниз слева
/// направо; подпись и путь подставляются после асинхронного поиска приложения.
/// </summary>
public sealed class DesktopLayerViewModel
{
    /// <summary>Размер ячейки сетки в DIP.</summary>
    private const double CellWidth = 96;
    private const double CellHeight = 112;
    private const double OriginX = 16;
    private const double OriginY = 16;
    private const int MaxRows = 6;
    private const int IconSize = 48;

    private readonly IDesktopManager _desktopManager;
    private readonly IApplicationManager _applications;
    private readonly IApplicationLauncher _launcher;
    private readonly DispatcherQueue _dispatcher;
    private readonly IDisposable _layoutSubscription;
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Поколение набора значков. Перестройка увеличивает его, а асинхронная загрузка иконки
    /// сверяет его перед применением: иначе результат для старого набора попадал бы в новый.
    /// </summary>
    private int _generation;

    public DesktopLayerViewModel(
        IDesktopManager desktopManager,
        IApplicationManager applications,
        IApplicationLauncher launcher,
        IEventManager events,
        DispatcherQueue dispatcher)
    {
        _desktopManager = desktopManager ?? throw new ArgumentNullException(nameof(desktopManager));
        _applications = applications ?? throw new ArgumentNullException(nameof(applications));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        ArgumentNullException.ThrowIfNull(events);

        _layoutSubscription = events.Subscribe<DesktopChanged>(OnDesktopChanged);
    }

    public ObservableCollection<DesktopIconViewModel> Icons { get; } = [];

    /// <summary>
    /// Загружает список приложений и строит значки. Ошибка обнаружения не прерывает
    /// работу: слой остаётся пустым и обновится по событию <see cref="DesktopChanged"/>.
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
            await _applications.DiscoverAsync();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.DesktopLayer: application discovery failed. {exception}");
        }

        Rebuild();
    }

    /// <summary>
    /// Открывает значок через <see cref="IApplicationLauncher.LaunchAsync"/>: слой
    /// показывает приложения Glaze, а не файлы рабочего стола пользователя.
    /// </summary>
    public async Task ActivateAsync(DesktopIconViewModel icon)
    {
        ArgumentNullException.ThrowIfNull(icon);

        if (icon.Path is not { Length: > 0 } target)
        {
            return;
        }

        try
        {
            await _launcher.LaunchAsync(target);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.DesktopLayer: launching '{target}' failed. {exception}");
        }
    }

    /// <summary>
    /// Убирает значок из слоя: элемент удаляется из layout активной вкладки. Файлы на диске
    /// не трогаются — безопасный слой показывает ярлыки Glaze, а не сами приложения.
    /// </summary>
    public bool Remove(DesktopIconViewModel icon)
    {
        ArgumentNullException.ThrowIfNull(icon);

        if (_desktopManager.RemoveApplication(icon.TabId, icon.Id))
        {
            return true;
        }

        Trace.WriteLine($"GlazeShell.DesktopLayer: '{icon.Id}' was not removed from the layout.");
        return false;
    }

    /// <summary>Загружает иконки для значков без изображения. Вызывается окном один раз.</summary>
    public void LoadIcons()
    {
        var generation = Volatile.Read(ref _generation);

        // Снимок: перестройка набора идёт на этом же потоке, но загрузка ниже — асинхронная,
        // и перебирать ObservableCollection, пока её меняют, нельзя.
        foreach (var icon in Icons.ToList())
        {
            if (icon.HasIcon || icon.Path is not { Length: > 0 })
            {
                continue;
            }

            var captured = icon;

            _ = Task.Run(async () =>
            {
                try
                {
                    var path = await ResolveIconPathAsync(captured);
                    var pixels = path is { Length: > 0 }
                        ? ShellIconProvider.TryGetIcon(path, IconSize)
                        : null;

                    if (pixels is null)
                    {
                        return;
                    }

                    // Изображение создаётся на потоке интерфейса: WriteableBitmap к другому
                    // потоку привязан нельзя.
                    _ = _dispatcher.TryEnqueue(() => ApplyIcon(captured, pixels, generation));
                }
                catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
                {
                    Trace.WriteLine($"GlazeShell.DesktopLayer: icon for '{captured.Name}' failed. {exception}");
                }
            });
        }
    }

    private void ApplyIcon(DesktopIconViewModel icon, IconPixels pixels, int generation)
    {
        if (_disposed || generation != Volatile.Read(ref _generation))
        {
            return;
        }

        var bitmap = IconBitmap.Create(pixels);

        if (bitmap is not null)
        {
            icon.Icon = bitmap;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _layoutSubscription.Dispose();
    }

    private void OnDesktopChanged(DesktopChanged message)
    {
        if (_disposed)
        {
            return;
        }

        if (!_dispatcher.HasThreadAccess)
        {
            _ = _dispatcher.TryEnqueue(Rebuild);
            return;
        }

        Rebuild();
    }

    private void Rebuild()
    {
        if (_disposed)
        {
            return;
        }

        var generation = ++_generation;
        Icons.Clear();

        var layout = _desktopManager.GetLayout();
        var tab = layout.Tabs.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, layout.ActiveTabId, StringComparison.Ordinal))
            ?? (layout.Tabs.Count > 0 ? layout.Tabs[0] : null);

        if (tab is null)
        {
            return;
        }

        var row = 0;
        var column = 0;

        foreach (var category in tab.Categories)
        {
            foreach (var item in category.Items)
            {
                // Пока приложение не найдено, подписью служит ярлык или идентификатор;
                // EnrichAsync подставит настоящее имя и загрузит иконку.
                Icons.Add(new DesktopIconViewModel(
                    item.Id,
                    tab.Id,
                    item.Label ?? item.ApplicationId,
                    item.ApplicationId,
                    OriginX + column * CellWidth,
                    OriginY + row * CellHeight));

                column++;

                if (column >= MaxRows)
                {
                    column = 0;
                    row++;
                }
            }
        }

        _ = EnrichAsync(generation);
    }

    /// <summary>
    /// Подставляет настоящие имена приложений и загружает иконки. Поиск по одному запросу
    /// на элемент: <c>IApplicationManager</c> не отдаёт снимок синхронно. Перебор идёт по
    /// снимку набора и прерывается, если за время поиска набор перестроили.
    /// </summary>
    private async Task EnrichAsync(int generation)
    {
        foreach (var icon in Icons.ToList())
        {
            if (_disposed || generation != _generation)
            {
                return;
            }

            if (icon.Path is not { Length: > 0 } applicationId)
            {
                continue;
            }

            try
            {
                var application = await _applications.GetAsync(applicationId);

                if (application is null)
                {
                    continue;
                }

                icon.Name = application.Name;
                icon.ExecutablePath = application.ExecutablePath;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                Trace.WriteLine($"GlazeShell.DesktopLayer: resolving '{applicationId}' failed. {exception}");
            }
        }

        if (!_disposed && generation == _generation)
        {
            LoadIcons();
        }
    }

    private async Task<string?> ResolveIconPathAsync(DesktopIconViewModel icon)
    {
        if (icon.Path is not { Length: > 0 } applicationId)
        {
            return null;
        }

        try
        {
            var application = await _applications.GetAsync(applicationId);
            return application?.IconPath ?? application?.ExecutablePath;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.DesktopLayer: icon path for '{applicationId}' failed. {exception}");
            return null;
        }
    }
}
