namespace GlazeShell.App.Presentation;

/// <summary>
/// Строка сетки месяца (функция D4): семь дней от первого дня недели текущей культуры.
/// </summary>
public sealed class CalendarWeekViewModel
{
    public CalendarWeekViewModel(IReadOnlyList<CalendarDayViewModel> days)
    {
        Days = days ?? throw new ArgumentNullException(nameof(days));
    }

    /// <summary>Семь дней строки.</summary>
    public IReadOnlyList<CalendarDayViewModel> Days { get; }
}
