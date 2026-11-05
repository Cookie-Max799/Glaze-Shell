using System.Collections.ObjectModel;
using System.ComponentModel;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Services;
using GlazeShell.Core.Settings;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Одна строка настройки в панели: описание из каталога и текущее значение. Одна модель
/// обслуживает все виды контролов (переключатель, список, текст, ползунок), а вид выбирается
/// по <see cref="SettingKind"/>; при изменении значение сразу записывается через переданный
/// обработчик, поэтому настройки применяются вживую.
/// </summary>
/// <remarks>
/// Ползунок шлёт новое значение на каждый пиксель перетаскивания, а запись настроек — это
/// файл на диске. Поэтому для <see cref="SettingKind.Range"/> запись объединяется короткой
/// задержкой: интерфейс обновляется сразу, а на диск попадает последнее значение.
/// </remarks>
public sealed class SettingRowViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan RangeApplyDelay = TimeSpan.FromMilliseconds(150);

    private readonly SettingDefinition _definition;
    private readonly Func<SettingDefinition, object, bool> _apply;
    private readonly EventCoalescer? _rangeApply;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue? _dispatcher;
    private object? _value;
    private object? _pendingRangeValue;
    private bool _suppress;

    public SettingRowViewModel(SettingDefinition definition, object? value, Func<SettingDefinition, object, bool> apply)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _value = value;
        _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        if (definition.Kind == SettingKind.Range)
        {
            _rangeApply = new EventCoalescer(
                RangeApplyDelay,
                ApplyPendingRangeOnDispatcher,
                EventCoalescer.FallbackErrorHandler("Settings"));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id => _definition.Id;

    public string Title => _definition.Title;

    public string Description => _definition.Description;

    public SettingKind Kind => _definition.Kind;

    public bool IsToggle => _definition.Kind == SettingKind.Toggle;

    public bool IsChoice => _definition.Kind == SettingKind.Choice;

    public bool IsText => _definition.Kind == SettingKind.Text;

    public bool IsRange => _definition.Kind == SettingKind.Range;

    /// <summary>Минимум ползунка (<see cref="SettingKind.Range"/>).</summary>
    public double Minimum => _definition.Minimum;

    /// <summary>Максимум ползунка (<see cref="SettingKind.Range"/>).</summary>
    public double Maximum => _definition.Maximum;

    /// <summary>Шаг ползунка (<see cref="SettingKind.Range"/>).</summary>
    public double Step => _definition.Step;

    /// <summary>Значение ползунка. Применяется с короткой задержкой, чтобы не писать файл на каждый пиксель.</summary>
    public double RangeValue
    {
        get => _value is double number ? number : 0;
        set
        {
            if (_value is double number && Math.Abs(number - value) < double.Epsilon)
            {
                return;
            }

            if (_rangeApply is null)
            {
                Set(value);
                return;
            }

            _pendingRangeValue = value;
            _value = value;
            OnPropertyChanged(nameof(RangeValue));
            OnPropertyChanged(nameof(RangeLabel));
            _rangeApply.Request();
        }
    }

    /// <summary>
    /// Подпись текущего значения ползунка с единицей измерения (например, «45 %»),
    /// чтобы число читалось без наведения на ползунок.
    /// </summary>
    public string RangeLabel => $"{RangeValue:0}{Unit}";

    /// <summary>Единица измерения значения ползунка.</summary>
    public string Unit => _definition.Unit;

    public IReadOnlyList<SettingChoice> Choices => _definition.Choices;

    /// <summary>Значение переключателя. Изменение применяется сразу.</summary>
    public bool ToggleValue
    {
        get => _value is true;
        set
        {
            if (_value is true == value)
            {
                return;
            }

            Set(value);
        }
    }

    /// <summary>Выбранный вариант списка. Изменение применяется сразу.</summary>
    public SettingChoice? SelectedChoice
    {
        get => _definition.Choices.FirstOrDefault(choice => string.Equals(
            choice.Value,
            _value as string ?? string.Empty,
            StringComparison.OrdinalIgnoreCase));
        set
        {
            if (value is null || _suppress)
            {
                return;
            }

            Set(value.Value);
        }
    }

    /// <summary>Текстовое значение. Применяется сразу.</summary>
    public string TextValue
    {
        get => _value as string ?? string.Empty;
        set
        {
            if (_suppress || string.Equals(_value as string, value, StringComparison.Ordinal))
            {
                return;
            }

            Set(value);
        }
    }

    /// <summary>
    /// Обновляет значение без применения: вызывается, когда настройки изменились из другого
    /// места (например, popover «Центр управления»), чтобы строка показала актуальное значение.
    /// </summary>
    public void Refresh(object? value)
    {
        _suppress = true;
        _value = value;
        OnPropertyChanged(nameof(ToggleValue));
        OnPropertyChanged(nameof(SelectedChoice));
        OnPropertyChanged(nameof(TextValue));
        OnPropertyChanged(nameof(RangeValue));
        OnPropertyChanged(nameof(RangeLabel));
        _suppress = false;
    }

    /// <summary>
    /// Останавливает отложенное применение ползунка. Последнее значение записывается сразу:
    /// закрытие окна не должно терять только что выбранную прозрачность.
    /// </summary>
    public void Dispose()
    {
        _rangeApply?.Dispose();
        ApplyPendingRange();
    }

    private void Set(object newValue)
    {
        if (_apply(_definition, newValue))
        {
            _value = newValue;
            OnPropertyChanged(nameof(ToggleValue));
            OnPropertyChanged(nameof(SelectedChoice));
            OnPropertyChanged(nameof(TextValue));
            OnPropertyChanged(nameof(RangeValue));
            OnPropertyChanged(nameof(RangeLabel));
        }
    }

    /// <summary>Записывает последнее значение ползунка после паузы перетаскивания.</summary>
    private void ApplyPendingRange()
    {
        if (_pendingRangeValue is { } value)
        {
            _pendingRangeValue = null;
            _apply(_definition, value);
        }
    }

    /// <summary>
    /// Таймер объединения работает в собственном потоке, а запись настроек публикует
    /// <c>SettingsChanged</c>, на который подписаны окна. Поэтому применение переносится
    /// в UI-поток — тот же, где живут кисти и визуальное дерево.
    /// </summary>
    private void ApplyPendingRangeOnDispatcher()
    {
        if (_dispatcher is null)
        {
            return;
        }

        _ = _dispatcher.TryEnqueue(ApplyPendingRange);
    }

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Пункт сайдбара: страница настроек и её видимость под текущий поиск. Страница без настроек
/// (заглушка «скоро») остаётся видимой, пока поиск пуст.
/// </summary>
public sealed class SettingsPageItemViewModel
{
    public SettingsPageItemViewModel(SettingsPage page)
    {
        Page = page ?? throw new ArgumentNullException(nameof(page));
    }

    public SettingsPage Page { get; }

    public string Id => Page.Id;

    public string Title => Page.Title;

    public string Glyph => Page.Glyph;

    public string Section => Page.Section;
}

/// <summary>
/// Модель окна настроек (I1, базовый слайс): строит сайдбар из каталога, показывает настройки
/// выбранной страницы и применяет изменения вживую через <see cref="ISettingsManager"/>.
/// Поиск фильтрует настройки по всем страницам, а не только по выбранной, поэтому нужное
/// находится из любого места.
/// </summary>
public sealed class SettingsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ISettingsManager _settings;
    private readonly IDisposable _settingsChanged;
    private readonly List<SettingRowViewModel> _allRows = [];
    private SettingsPageItemViewModel? _selectedPage;
    private string _searchText = string.Empty;
    private string _emptyHint = "На этой странице нет настроек.";

    public SettingsViewModel(ISettingsManager settings, IEventManager events)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ArgumentNullException.ThrowIfNull(events);

        Pages = new ObservableCollection<SettingsPageItemViewModel>(
            SettingsCatalog.Pages.Select(page => new SettingsPageItemViewModel(page)));

        foreach (var definition in SettingsCatalog.All)
        {
            _allRows.Add(new SettingRowViewModel(definition, definition.Read(_settings.GetSettings()), Apply));
        }

        Settings = new ObservableCollection<SettingRowViewModel>();
        _settingsChanged = events.Subscribe<SettingsChanged>(_ => ReloadFromSettings());

        _selectedPage = Pages.FirstOrDefault();
        ApplyFilter();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SettingsPageItemViewModel> Pages { get; }

    /// <summary>Настройки, показанные в панели, — уже отфильтрованные под поиск.</summary>
    public ObservableCollection<SettingRowViewModel> Settings { get; }

    public SettingsPageItemViewModel? SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (!ReferenceEquals(_selectedPage, value))
            {
                _selectedPage = value;
                ApplyFilter();
                OnPropertyChanged(nameof(SelectedPage));
                OnPropertyChanged(nameof(SelectedPageTitle));
            }
        }
    }

    public string SelectedPageTitle => _selectedPage?.Title ?? "Настройки";

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!string.Equals(_searchText, value, StringComparison.Ordinal))
            {
                _searchText = value ?? string.Empty;
                ApplyFilter();
                OnPropertyChanged(nameof(SearchText));
            }
        }
    }

    public string EmptyHint => _emptyHint;

    public bool HasSettings => Settings.Count > 0;

    public bool IsEmpty => Settings.Count == 0;

    /// <summary>Сброс настроек текущей страницы к значениям по умолчанию (критерий I1).</summary>
    public void ResetSelectedPage()
    {
        if (_selectedPage is null)
        {
            return;
        }

        var reset = SettingsCatalog.Reset(_settings.GetSettings(), _selectedPage.Id);
        _settings.SetSettings(reset);
        ReloadFromSettings();
    }

    public void Dispose()
    {
        _settingsChanged.Dispose();

        foreach (var row in _allRows)
        {
            row.Dispose();
        }
    }

    /// <summary>
    /// Записывает новое значение настройки. Возвращает <c>true</c>, если значение изменено и
    /// настройки сохранены: тогда строка обновит своё состояние. Менеджер настроек сам
    /// публикует <see cref="Core.Events.SettingsChanged"/>, поэтому панели и тема обновляются
    /// без участия окна настроек.
    /// </summary>
    private bool Apply(SettingDefinition definition, object value)
    {
        var current = _settings.GetSettings();
        var updated = definition.Write(current, value);

        // Ничего не меняем, если значение уже такое: иначе каждое открытие панели и каждый
        // клик по уже выбранному варианту писали бы файл настроек заново.
        if (Equals(definition.Read(current), definition.Read(updated)))
        {
            return false;
        }

        _settings.SetSettings(updated);
        return true;
    }

    private void ReloadFromSettings()
    {
        var settings = _settings.GetSettings();

        foreach (var row in _allRows)
        {
            var definition = SettingsCatalog.All.First(item => item.Id == row.Id);
            row.Refresh(definition.Read(settings));
        }
    }

    /// <summary>
    /// Пересобирает панель: при пустом поиске показывает настройки выбранной страницы, при
    /// непустом — все совпавшие настройки независимо от страницы, чтобы результат поиска был
    /// полным.
    /// </summary>
    private void ApplyFilter()
    {
        var query = _searchText.Trim();
        var searching = query.Length > 0;

        IEnumerable<SettingRowViewModel> rows = searching
            ? SettingsCatalog.Search(query)
                .Select(definition => _allRows.First(row => row.Id == definition.Id))
            : _selectedPage is null
                ? []
                : SettingsCatalog.ForPage(_selectedPage.Id)
                    .Select(definition => _allRows.First(row => row.Id == definition.Id));

        Settings.Clear();

        foreach (var row in rows)
        {
            Settings.Add(row);
        }

        _emptyHint = searching
            ? "Ничего не найдено. Попробуйте другое слово."
            : "На этой странице нет настроек.";

        OnPropertyChanged(nameof(HasSettings));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyHint));
    }

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
