namespace GlazeShell.Core.Calendar;

/// <summary>
/// День в сетке месяца (функция D4): дата и её положение относительно показываемого месяца.
/// </summary>
/// <remarks>
/// Модель framework-free: месяц строится без WinUI и без обращения к системным настройкам,
/// поэтому первый день недели и границы сетки проверяются обычными юнит-тестами.
/// </remarks>
/// <param name="Date">Календарная дата дня.</param>
/// <param name="IsCurrentMonth">День принадлежит показываемому месяцу, а не соседним.</param>
/// <param name="IsToday">День совпадает с сегодняшней датой.</param>
/// <param name="IsWeekend">День приходится на субботу или воскресенье.</param>
public sealed record CalendarDay(DateOnly Date, bool IsCurrentMonth, bool IsToday, bool IsWeekend);
