using GlazeShell.Core.Calendar;

namespace GlazeShell.Core.Tests.Calendar;

[TestClass]
public sealed class MonthCalendarTests
{
    private static readonly DateOnly Today = new(2026, 10, 23);

    [TestMethod]
    public void BuildsSixWeeksOfSevenConsecutiveDays()
    {
        var calendar = MonthCalendar.Create(new DateOnly(2026, 10, 1), Today);

        Assert.HasCount(MonthCalendar.WeekCount, calendar.Weeks);
        Assert.HasCount(MonthCalendar.DaysInWeek, calendar.Weeks[0]);
        Assert.AreEqual(new DateOnly(2026, 9, 28), calendar.FirstVisibleDate);
        Assert.AreEqual(new DateOnly(2026, 11, 8), calendar.LastVisibleDate);

        var expected = calendar.FirstVisibleDate;
        foreach (var week in calendar.Weeks)
        {
            foreach (var day in week)
            {
                Assert.AreEqual(expected, day.Date);
                expected = expected.AddDays(1);
            }
        }
    }

    [TestMethod]
    public void StartsOnMondayWhenFirstDayOfWeekIsMonday()
    {
        var calendar = MonthCalendar.Create(
            new DateOnly(2026, 10, 1),
            Today,
            DayOfWeek.Monday);

        Assert.AreEqual(DayOfWeek.Monday, calendar.Weeks[0][0].Date.DayOfWeek);
        Assert.AreEqual(new DateOnly(2026, 9, 28), calendar.FirstVisibleDate);
    }

    [TestMethod]
    public void StartsOnSundayWhenFirstDayOfWeekIsSunday()
    {
        var calendar = MonthCalendar.Create(
            new DateOnly(2026, 10, 1),
            Today,
            DayOfWeek.Sunday);

        Assert.AreEqual(DayOfWeek.Sunday, calendar.Weeks[0][0].Date.DayOfWeek);
        Assert.AreEqual(new DateOnly(2026, 9, 27), calendar.FirstVisibleDate);
    }

    [TestMethod]
    public void MarksDaysOfAdjacentMonthsAndWeekends()
    {
        var calendar = MonthCalendar.Create(
            new DateOnly(2026, 10, 1),
            Today,
            DayOfWeek.Monday);

        var leading = calendar.Weeks[0][0];
        Assert.IsFalse(leading.IsCurrentMonth);
        Assert.AreEqual(new DateOnly(2026, 9, 28), leading.Date);

        var firstOfMonth = calendar.Weeks[0][3];
        Assert.AreEqual(new DateOnly(2026, 10, 1), firstOfMonth.Date);
        Assert.IsTrue(firstOfMonth.IsCurrentMonth);
        Assert.IsFalse(firstOfMonth.IsWeekend);

        var trailing = calendar.Weeks[MonthCalendar.WeekCount - 1][MonthCalendar.DaysInWeek - 1];
        Assert.AreEqual(new DateOnly(2026, 11, 8), trailing.Date);
        Assert.IsFalse(trailing.IsCurrentMonth);
        Assert.IsTrue(trailing.IsWeekend);
    }

    [TestMethod]
    public void MarksExactlyOneCellAsToday()
    {
        var calendar = MonthCalendar.Create(new DateOnly(2026, 10, 1), Today);

        var todayCells = calendar.Weeks.SelectMany(week => week).Where(day => day.IsToday).ToList();

        Assert.HasCount(1, todayCells);
        Assert.AreEqual(Today, todayCells[0].Date);
    }

    [TestMethod]
    public void ShiftMovesToAdjacentMonthAndKeepsToday()
    {
        var calendar = MonthCalendar.Create(new DateOnly(2026, 12, 1), Today);

        var next = calendar.Shift(1);
        Assert.AreEqual(2027, next.Year);
        Assert.AreEqual(1, next.Month);
        Assert.AreEqual(Today, next.Today);
        Assert.IsFalse(next.Weeks.SelectMany(week => week).Any(day => day.IsToday));

        var previous = calendar.Shift(-1);
        Assert.AreEqual(2026, previous.Year);
        Assert.AreEqual(11, previous.Month);
    }

    [TestMethod]
    public void ShiftBackFromJanuaryLandsInDecemberOfPreviousYear()
    {
        var january = MonthCalendar.Create(new DateOnly(2027, 1, 15), Today);
        var december = january.Shift(-1);

        Assert.AreEqual(2026, december.Year);
        Assert.AreEqual(12, december.Month);
        Assert.AreEqual(new DateOnly(2026, 11, 30), december.FirstVisibleDate);
        Assert.AreEqual(new DateOnly(2026, 12, 1), december.Weeks[0][1].Date);
    }

    [TestMethod]
    public void ShowsMonthOfChecksYearAndMonth()
    {
        var calendar = MonthCalendar.Create(new DateOnly(2026, 10, 1), Today);

        Assert.IsTrue(calendar.ShowsMonthOf(new DateOnly(2026, 10, 31)));
        Assert.IsFalse(calendar.ShowsMonthOf(new DateOnly(2026, 11, 1)));
        Assert.IsFalse(calendar.ShowsMonthOf(new DateOnly(2025, 10, 1)));
    }

    [TestMethod]
    public void MonthStartingOnSundayFitsSixWeeks()
    {
        var calendar = MonthCalendar.Create(
            new DateOnly(2026, 11, 1),
            Today,
            DayOfWeek.Monday);

        Assert.AreEqual(new DateOnly(2026, 11, 1), calendar.Weeks[0][6].Date);
        Assert.IsTrue(calendar.Weeks[0][6].IsCurrentMonth);
        Assert.AreEqual(new DateOnly(2026, 12, 6), calendar.LastVisibleDate);
    }
}
