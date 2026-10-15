using System.Globalization;
using GlazeShell.Core.Models;

namespace GlazeShell.Core.Diagnostics;

/// <summary>
/// Итог сессии между двумя снимками ресурсов: длительность, средняя загрузка процессора
/// и прирост памяти, потоков и handles. Отрицательная дельта означает снижение показателя.
/// </summary>
public sealed record PerformanceSessionSummary
{
    public PerformanceSessionSummary(
        TimeSpan elapsed,
        double averageCpuUtilizationPercent,
        long workingSetDeltaBytes,
        long managedHeapDeltaBytes,
        int threadDelta,
        int handleDelta)
    {
        Elapsed = ModelValidation.NonNegative(elapsed, nameof(elapsed));
        ModelValidation.NonNegative(averageCpuUtilizationPercent, nameof(averageCpuUtilizationPercent));

        AverageCpuUtilizationPercent = averageCpuUtilizationPercent;
        WorkingSetDeltaBytes = workingSetDeltaBytes;
        ManagedHeapDeltaBytes = managedHeapDeltaBytes;
        ThreadDelta = threadDelta;
        HandleDelta = handleDelta;
    }

    public TimeSpan Elapsed { get; }

    /// <summary>
    /// Средняя загрузка процессора за сессию, нормированная на число ядер: значение в диапазоне
    /// <c>0..100</c>. Простой сессии без взаимодействия с пользователем даёт искомый idle CPU.
    /// </summary>
    public double AverageCpuUtilizationPercent { get; }

    public long WorkingSetDeltaBytes { get; }

    public long ManagedHeapDeltaBytes { get; }

    public int ThreadDelta { get; }

    public int HandleDelta { get; }

    public string ToLogString() =>
        $"session {Seconds(Elapsed)} s, average cpu {Percent(AverageCpuUtilizationPercent)}, " +
        $"working set {DeltaMegabytes(WorkingSetDeltaBytes)}, managed heap {DeltaMegabytes(ManagedHeapDeltaBytes)}, " +
        $"threads {Signed(ThreadDelta)}, handles {Signed(HandleDelta)}";

    private static string Seconds(TimeSpan value) =>
        value.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture);

    private static string Percent(double value) =>
        value.ToString("F2", CultureInfo.InvariantCulture) + "%";

    private static string DeltaMegabytes(long bytes)
    {
        var sign = bytes < 0 ? "-" : "+";
        return sign + (Math.Abs(bytes) / (1024.0 * 1024.0)).ToString("F1", CultureInfo.InvariantCulture) + " MB";
    }

    private static string Signed(int value) =>
        value.ToString("+#;-#;0", CultureInfo.InvariantCulture);
}

/// <summary>
/// Считает итог сессии по двум снимкам. Логика чистая и framework-free: она не читает
/// процесс и не пишет в лог, поэтому проверяется тестами на подготовленных снимках.
/// </summary>
public static class PerformanceSession
{
    /// <summary>
    /// Сопоставляет начальный и конечный снимки. Процессорное время нормируется на
    /// <paramref name="processorCount"/>, поэтому на многоядерной машине результат читается
    /// как загрузка одного ядра в процентах, а не как сумма по всем ядрам.
    /// </summary>
    public static PerformanceSessionSummary Summarize(
        PerformanceSnapshot start,
        PerformanceSnapshot end,
        int processorCount)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);

        if (processorCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processorCount), processorCount, "The processor count must be positive.");
        }

        var elapsed = end.Timestamp - start.Timestamp;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var processorDelta = end.TotalProcessorTime - start.TotalProcessorTime;
        if (processorDelta < TimeSpan.Zero)
        {
            processorDelta = TimeSpan.Zero;
        }

        var wallMilliseconds = elapsed.TotalMilliseconds * processorCount;
        var cpuPercent = wallMilliseconds <= 0
            ? 0
            : Math.Clamp(processorDelta.TotalMilliseconds / wallMilliseconds * 100.0, 0, 100);

        return new PerformanceSessionSummary(
            elapsed,
            cpuPercent,
            end.WorkingSetBytes - start.WorkingSetBytes,
            end.ManagedHeapBytes - start.ManagedHeapBytes,
            end.ThreadCount - start.ThreadCount,
            end.HandleCount - start.HandleCount);
    }
}
