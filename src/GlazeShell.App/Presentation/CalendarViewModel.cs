using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using GlazeShell.Core.Calendar;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Состояние popover часов и календаря (функция D4): сетка месяца, перелистывание и переход
/// к сегодняшнему дню.
/// </summary>
/// <remarks>
/// Правила сетки живут в framework-free <see cref="MonthCalendar"/>, а эта модель только
/// переводит их в элементы интерфейса и учитывает культуру: первый день недели, названия
/// месяцев и подписи дней берутся из <see cref="CultureInfo.CurrentCulture"/>, поэтому
/// порядок дней и форматы чисел совпадают с системными. События календаря (ICS, аккаунты)
/// в базовый слайс не входят: сетка работает полностью офлайн.
/// </remarks>
public sealed class CalendarViewModel : INotifyPropertyChanged
{
    private readonly CultureInfo _culture;
    private readonly DayOfWeek _firstDayOfWeek;

    private MonthCalendar _month;
    private bool _isOpen;
    private string _monthTitle = string.Empty;
    private string _todayLabel = string.Empty;

    public CalendarViewModel()
        : this(CultureInfo.CurrentCulture)
    {
    }

    public CalendarViewModel(CultureInfo culture)
    {
        _culture = culture ?? throw new ArgumentNullException(nameof(culture));
        _firstDayOfWeek = culture.DateTimeFormat.FirstDayOfWeek;
        WeekdayLabels = BuildWeekdayLabels(culture, _firstDayOfWeek);
        _month = MonthCalendar.Create(Today, Today, _firstDayOfWeek);
        ApplyMonth();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Открыт ли popover календаря.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (value == _isOpen)
            {
                return;
            }

            // Каждое открытие показывает текущий месяц: календарь не «запоминает» чужой
            // месяц, который пользователь пролистал в прошлый раз.
            if (value)
            {
                ResetToToday();
            }

            Set(ref _isOpen, value);
        }
    }

    /// <summary>Заголовок месяца с учётом культуры: название месяца и год.</summary>
    public string MonthTitle
    {
        get => _monthTitle;
        private set => Set(ref _monthTitle, value);
    }

    /// <summary>Строка «Сегодня, 23 октября» под заголовком.</summary>
    public string TodayLabel
    {
        get => _todayLabel;
        private set => Set(ref _todayLabel, value);
    }

    /// <summary>
    /// Подписи дней недели в порядке текущей культуры (например, «ПН ВТ СР…»).
    /// </summary>
    public IReadOnlyList<string> WeekdayLabels { get; }

    /// <summary>Шесть строк сетки месяца.</summary>
    public ObservableCollection<CalendarWeekViewModel> Weeks { get; } = [];

    /// <summary>
    /// Показывается ли месяц, отличный от текущего: кнопка «Сегодня» доступна только тогда,
    /// когда есть куда возвращаться.
    /// </summary>
    public bool CanGoToToday => !_month.ShowsMonthOf(Today);

    /// <summary>Перелистывает сетку на предыдущий месяц.</summary>
    public void PreviousMonth() => ShowMonth(_month.Shift(-1));

    /// <summary>Перелистывает сетку на следующий месяц.</summary>
    public void NextMonth() => ShowMonth(_month.Shift(1));

    /// <summary>Возвращает сетку к месяцу, в котором находится сегодняшняя дата.</summary>
    public void GoToToday() => ResetToToday();

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    private static string[] BuildWeekdayLabels(CultureInfo culture, DayOfWeek firstDayOfWeek)
    {
        var names = culture.DateTimeFormat.AbbreviatedDayNames;
        var labels = new string[MonthCalendar.DaysInWeek];

        for (var index = 0; index < labels.Length; index++)
        {
            var name = names[((int)firstDayOfWeek + index) % MonthCalendar.DaysInWeek];
            labels[index] = culture.TextInfo.ToUpper(name);
        }

        return labels;
    }

    private void ResetToToday()
    {
        var today = Today;
        _month = MonthCalendar.Create(today, today, _firstDayOfWeek);
        ApplyMonth();
    }

    private void ShowMonth(MonthCalendar month)
    {
        _month = month;
        ApplyMonth();
    }

    private void ApplyMonth()
    {
        MonthTitle = BuildMonthTitle();
        TodayLabel = $"Сегодня, {_month.Today.ToString("d MMMM", _culture)}";

        Weeks.Clear();

        foreach (var week in _month.Weeks)
        {
            Weeks.Add(new CalendarWeekViewModel(
                week.Select(day => new CalendarDayViewModel(day, _culture)).ToArray()));
        }

        OnPropertyChanged(nameof(CanGoToToday));
    }

    private string BuildMonthTitle()
    {
        var name = _culture.DateTimeFormat.MonthNames[_month.Month - 1];
        return string.Concat(_culture.TextInfo.ToTitleCase(name), " ", _month.Year.ToString(_culture));
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
