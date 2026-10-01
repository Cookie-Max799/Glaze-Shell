namespace GlazeShell.Windows.SystemEvents;

/// <summary>
/// Параметры наблюдения за процессами.
/// </summary>
public sealed record ProcessWatchOptions
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaximumInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Интервал между снимками списка процессов. Windows не предоставляет штатного
    /// события «процесс запущен» для произвольного <c>exe</c>: ETW и WMI потребовали бы
    /// либо новой зависимости, либо недокументированных вызовов, поэтому источник
    /// сверяет один системный снимок за интервал. Значение по умолчанию — 2 секунды.
    /// </summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(2);

    public static ProcessWatchOptions Default { get; } = new();

    public ProcessWatchOptions Validate()
    {
        if (Interval < MinimumInterval || Interval > MaximumInterval)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Interval),
                Interval,
                $"The interval must be between {MinimumInterval} and {MaximumInterval}.");
        }

        return this;
    }
}
