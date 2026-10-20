using System.Collections.ObjectModel;
using System.ComponentModel;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Settings;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Одна строка настройки в панели: описание из каталога и текущее значение. Одна модель
/// обслуживает все виды контролов (переключатель, список, текст), а вид выбирается по
/// <see cref="SettingKind"/>; при изменении значение сразу записывается через переданный
/// обработчик, поэтому настройки применяются вживую.
/// </summary>
public sealed class SettingRowViewModel : INotifyPropertyChanged
{
    private readonly SettingDefinition _definition;
    private readonly Func<SettingDefinition, object, bool> _apply;
    private object? _value;
    private bool _suppress;

    public SettingRowViewModel(SettingDefinition definition, object? value, Func<SettingDefinition, object, bool> apply)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _value = value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id => _definition.Id;

    public string Title => _definition.Title;

    public string Description => _definition.Description;

    public SettingKind Kind => _definition.Kind;

    public bool IsToggle => _definition.Kind == SettingKind.Toggle;

    public bool IsChoice => _definition.Kind == SettingKind.Choice;

    public bool IsText => _definition.Kind == SettingKind.Text;

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
        _suppress = false;
    }

    private void Set(object newValue)
    {
        if (_apply(_definition, newValue))
        {
            _value = newValue;
            OnPropertyChanged(nameof(ToggleValue));
            OnPropertyChanged(nameof(SelectedChoice));
            OnPropertyChanged(nameof(TextValue));
        }
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
    private string _emptyHint = "На этой странице пока нет настроек — раздел появится в следующих слайсах.";

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

    public void Dispose() => _settingsChanged.Dispose();

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
            : "На этой странице пока нет настроек — раздел появится в следующих слайсах.";

        OnPropertyChanged(nameof(HasSettings));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyHint));
    }

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
