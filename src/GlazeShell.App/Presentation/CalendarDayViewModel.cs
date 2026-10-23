using System.Globalization;
using GlazeShell.Core.Calendar;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Один день в сетке popover часов и календаря (функция D4): число, признак «сегодня» и
/// озвучиваемое имя с полной датой.
/// </summary>
public sealed class CalendarDayViewModel
{
    public CalendarDayViewModel(CalendarDay day, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(day);
        ArgumentNullException.ThrowIfNull(culture);

        DayText = day.Date.Day.ToString(culture);
        IsToday = day.IsToday;
        IsWeekend = day.IsWeekend;

        // Дни соседних месяцев остаются видимыми, но приглушаются: так сетка не «прыгает»
        // при перелистывании, а граница месяца читается без дополнительных линий.
        Opacity = day.IsCurrentMonth ? 1.0 : 0.4;

        var name = day.Date.ToString("dddd, d MMMM yyyy", culture);
        AutomationName = day.IsToday ? $"{name}, сегодня" : name;
    }

    /// <summary>Число месяца для отображения (без ведущего нуля).</summary>
    public string DayText { get; }

    /// <summary>День совпадает с сегодняшней датой: вокруг числа рисуется акцентное кольцо.</summary>
    public bool IsToday { get; }

    /// <summary>День приходится на субботу или воскресенье.</summary>
    public bool IsWeekend { get; }

    /// <summary>Прозрачность числа: приглушённая для дней соседних месяцев.</summary>
    public double Opacity { get; }

    /// <summary>Озвучиваемое имя дня с полной датой для средств доступности.</summary>
    public string AutomationName { get; }
}
