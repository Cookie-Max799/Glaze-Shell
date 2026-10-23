namespace GlazeShell.Core.Calendar;

/// <summary>
/// Сетка месяца для popover часов и календаря (функция D4).
/// </summary>
/// <remarks>
/// Сетка всегда состоит из шести недель по семь дней: так высота popover не прыгает при
/// перелистывании месяцев, а дни соседних месяцев остаются видимыми. Первый день недели —
/// параметр, а не константа: UI передаёт значение из текущей культуры, поэтому в русской
/// локали неделя начинается с понедельника, а в американской — с воскресенья.
/// </remarks>
public sealed class MonthCalendar
{
    /// <summary>Число недель в сетке: шесть строк всегда вмещают любой месяц.</summary>
    public const int WeekCount = 6;

    /// <summary>Число дней в неделе.</summary>
    public const int DaysInWeek = 7;

    private readonly CalendarDay[][] _weeks;

    private MonthCalendar(
        int year,
        int month,
        DateOnly today,
        DayOfWeek firstDayOfWeek,
        CalendarDay[][] weeks)
    {
        Year = year;
        Month = month;
        Today = today;
        FirstDayOfWeek = firstDayOfWeek;
        _weeks = weeks;
    }

    /// <summary>Год показываемого месяца.</summary>
    public int Year { get; }

    /// <summary>Месяц показываемого месяца (1–12).</summary>
    public int Month { get; }

    /// <summary>Сегодняшняя дата, переданная при построении: используется для флага «сегодня».</summary>
    public DateOnly Today { get; }

    /// <summary>Первый день недели, с которого начинается каждая строка сетки.</summary>
    public DayOfWeek FirstDayOfWeek { get; }

    /// <summary>
    /// Шесть недель сетки, по семь дней в каждой. Первая и последняя недели могут содержать
    /// дни соседних месяцев: они помечены <see cref="CalendarDay.IsCurrentMonth"/> = <c>false</c>.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<CalendarDay>> Weeks => _weeks;

    /// <summary>Первый видимый день сетки (может относиться к прошлому месяцу).</summary>
    public DateOnly FirstVisibleDate => _weeks[0][0].Date;

    /// <summary>Последний видимый день сетки (может относиться к следующему месяцу).</summary>
    public DateOnly LastVisibleDate => _weeks[WeekCount - 1][DaysInWeek - 1].Date;

    /// <summary>Показывает ли сетка месяц, в котором находится <paramref name="date"/>.</summary>
    public bool ShowsMonthOf(DateOnly date) => date.Year == Year && date.Month == Month;

    /// <summary>
    /// Строит сетку месяца, в котором находится <paramref name="anchor"/>. Позиция
    /// <paramref name="today"/> отмечается флагом «сегодня» во всех шести неделях.
    /// </summary>
    /// <param name="anchor">Любая дата показываемого месяца.</param>
    /// <param name="today">Сегодняшняя дата для флага «сегодня».</param>
    /// <param name="firstDayOfWeek">Первый день недели из текущей культуры.</param>
    public static MonthCalendar Create(
        DateOnly anchor,
        DateOnly today,
        DayOfWeek firstDayOfWeek = DayOfWeek.Monday)
    {
        var firstOfMonth = new DateOnly(anchor.Year, anchor.Month, 1);

        // Сколько дней прошлого месяца попадает в первую строку: разница между первым
        // днём месяца и выбранным первым днём недели по модулю семи.
        var leadingDays = ((int)firstOfMonth.DayOfWeek - (int)firstDayOfWeek + DaysInWeek) % DaysInWeek;
        var start = firstOfMonth.AddDays(-leadingDays);

        var weeks = new CalendarDay[WeekCount][];

        for (var week = 0; week < WeekCount; week++)
        {
            var days = new CalendarDay[DaysInWeek];

            for (var day = 0; day < DaysInWeek; day++)
            {
                var date = start.AddDays((week * DaysInWeek) + day);

                days[day] = new CalendarDay(
                    date,
                    date.Year == firstOfMonth.Year && date.Month == firstOfMonth.Month,
                    date == today,
                    date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
            }

            weeks[week] = days;
        }

        return new MonthCalendar(firstOfMonth.Year, firstOfMonth.Month, today, firstDayOfWeek, weeks);
    }

    /// <summary>
    /// Возвращает сетку соседнего месяца: <paramref name="months"/> может быть отрицательным.
    /// Флаг «сегодня» и первый день недели сохраняются.
    /// </summary>
    public MonthCalendar Shift(int months) =>
        Create(new DateOnly(Year, Month, 1).AddMonths(months), Today, FirstDayOfWeek);
}
