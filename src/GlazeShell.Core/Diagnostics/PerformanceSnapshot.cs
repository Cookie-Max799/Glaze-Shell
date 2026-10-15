using System.Globalization;
using GlazeShell.Core.Models;

namespace GlazeShell.Core.Diagnostics;

/// <summary>
/// Снимок ресурсов процесса в один момент времени: рабочая и приватная память, managed heap,
/// число потоков и handles, накопленное процессорное время и время работы процесса.
/// </summary>
/// <remarks>
/// Снимок — только данные: он описывает измерение, но не собирает его. Благодаря этому
/// Core остаётся framework-free, а сбор метрик процесса выполняет Infrastructure
/// (<c>ProcessMetricsCollector</c>), и математику сессии можно проверить без запуска приложения.
/// </remarks>
public sealed record PerformanceSnapshot
{
    public PerformanceSnapshot(
        string label,
        DateTimeOffset timestamp,
        long workingSetBytes,
        long privateMemoryBytes,
        long managedHeapBytes,
        int threadCount,
        int handleCount,
        TimeSpan totalProcessorTime,
        TimeSpan uptime)
    {
        Label = ModelValidation.Required(label, nameof(label));
        Timestamp = timestamp;
        WorkingSetBytes = NonNegative(workingSetBytes, nameof(workingSetBytes));
        PrivateMemoryBytes = NonNegative(privateMemoryBytes, nameof(privateMemoryBytes));
        ManagedHeapBytes = NonNegative(managedHeapBytes, nameof(managedHeapBytes));
        ThreadCount = NonNegative(threadCount, nameof(threadCount));
        HandleCount = NonNegative(handleCount, nameof(handleCount));
        TotalProcessorTime = ModelValidation.NonNegative(totalProcessorTime, nameof(totalProcessorTime));
        Uptime = ModelValidation.NonNegative(uptime, nameof(uptime));
    }

    /// <summary>Метка измерения: например <c>startup</c>, <c>idle</c> или <c>shutdown</c>.</summary>
    public string Label { get; }

    public DateTimeOffset Timestamp { get; }

    public long WorkingSetBytes { get; }

    public long PrivateMemoryBytes { get; }

    public long ManagedHeapBytes { get; }

    public int ThreadCount { get; }

    /// <summary>
    /// Число открытых handles процесса. Рост этого числа между снимками указывает на утечку
    /// native ресурсов (окон, hooks, событий), которую ищет профилирование Stage 11.
    /// </summary>
    public int HandleCount { get; }

    public TimeSpan TotalProcessorTime { get; }

    public TimeSpan Uptime { get; }

    public string ToLogString() =>
        $"working set {Megabytes(WorkingSetBytes)} MB, private {Megabytes(PrivateMemoryBytes)} MB, " +
        $"managed heap {Megabytes(ManagedHeapBytes)} MB, threads {ThreadCount}, handles {HandleCount}, " +
        $"cpu {Seconds(TotalProcessorTime)} s, uptime {Seconds(Uptime)} s";

    // Числа форматируются инвариантно: запись лога не должна зависеть от региональных
    // настроек, иначе разделитель дробной части менялся бы вместе с языком системы.
    private static string Megabytes(long bytes) =>
        (bytes / (1024.0 * 1024.0)).ToString("F1", CultureInfo.InvariantCulture);

    private static string Seconds(TimeSpan value) =>
        value.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture);

    private static long NonNegative(long value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The value cannot be negative.");
        }

        return value;
    }

    private static int NonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The value cannot be negative.");
        }

        return value;
    }
}
