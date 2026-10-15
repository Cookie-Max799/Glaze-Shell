using System.Globalization;

namespace GlazeShell.Core.Diagnostics;

/// <summary>
/// Статистика задержки доставки событий: сколько раз событие было доставлено и каково было
/// минимальное, среднее и максимальное время выполнения подписчиков. Время измеряется вокруг
/// всего цикла доставки события, поэтому аномально большое значение указывает на подписчика,
/// который задерживает публикацию.
/// </summary>
public sealed record EventLatencyStats
{
    public required long Count { get; init; }

    public required TimeSpan Total { get; init; }

    public required TimeSpan Minimum { get; init; }

    public required TimeSpan Maximum { get; init; }

    public TimeSpan Average => Count == 0 ? TimeSpan.Zero : Total / Count;

    public string ToLogString() => Count == 0
        ? "no events were dispatched"
        : $"dispatches {Count}, average {Microseconds(Average)} µs, " +
          $"maximum {Microseconds(Maximum)} µs, minimum {Microseconds(Minimum)} µs";

    private static string Microseconds(TimeSpan value) =>
        value.TotalMicroseconds.ToString("F1", CultureInfo.InvariantCulture);
}

/// <summary>
/// Накапливает задержку доставки событий. Запись потокобезопасна: события публикуются
/// и из фонового hook-потока, и из UI-потока, поэтому счётчики защищены блокировкой.
/// </summary>
/// <remarks>
/// Сборщик необязателен: пока он не передан <c>EventManager</c>, горячий путь доставки не
/// измеряется и не получает дополнительных блокировок. Замеры выполняются по требованию,
/// а не постоянно.
/// </remarks>
public sealed class EventLatencyRecorder
{
    private readonly object _sync = new();

    private long _count;
    private long _totalTicks;
    private long _minimumTicks = long.MaxValue;
    private long _maximumTicks;

    public void Record(TimeSpan elapsed)
    {
        var ticks = elapsed.Ticks;
        if (ticks < 0)
        {
            ticks = 0;
        }

        lock (_sync)
        {
            _count++;
            _totalTicks += ticks;
            _minimumTicks = Math.Min(_minimumTicks, ticks);
            _maximumTicks = Math.Max(_maximumTicks, ticks);
        }
    }

    public EventLatencyStats Snapshot()
    {
        lock (_sync)
        {
            return new EventLatencyStats
            {
                Count = _count,
                Total = TimeSpan.FromTicks(_totalTicks),
                Minimum = _count == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(_minimumTicks),
                Maximum = TimeSpan.FromTicks(_maximumTicks),
            };
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _count = 0;
            _totalTicks = 0;
            _minimumTicks = long.MaxValue;
            _maximumTicks = 0;
        }
    }
}
