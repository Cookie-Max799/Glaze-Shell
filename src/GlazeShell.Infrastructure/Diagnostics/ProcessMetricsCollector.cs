using System.Diagnostics;
using GlazeShell.Core.Diagnostics;

namespace GlazeShell.Infrastructure.Diagnostics;

/// <summary>
/// Снимает метрики текущего процесса для профилирования. Модуль зависит только от BCL
/// и Core: он не знает о UI и Win32, поэтому его можно вызывать из composition root.
/// </summary>
/// <remarks>
/// Отдельные свойства <see cref="Process"/> могут быть недоступны в ограниченной среде
/// (например, <see cref="Process.StartTime"/> требует прав). Недоступная метрика не должна
/// срывать профилирование: вместо неё подставляется <c>0</c>, а остальные значения снимаются.
/// </remarks>
public static class ProcessMetricsCollector
{
    public static PerformanceSnapshot Capture(string label)
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();

        return new PerformanceSnapshot(
            label,
            DateTimeOffset.UtcNow,
            Metric(() => process.WorkingSet64),
            Metric(() => process.PrivateMemorySize64),
            GC.GetTotalMemory(forceFullCollection: false),
            Metric(() => process.Threads.Count),
            Metric(() => process.HandleCount),
            ProcessorTime(process),
            Uptime(process));
    }

    private static long Metric(Func<long> read)
    {
        try
        {
            return read();
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return 0;
        }
    }

    private static int Metric(Func<int> read)
    {
        try
        {
            return read();
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return 0;
        }
    }

    private static TimeSpan ProcessorTime(Process process)
    {
        try
        {
            return process.TotalProcessorTime;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return TimeSpan.Zero;
        }
    }

    private static TimeSpan Uptime(Process process)
    {
        try
        {
            var uptime = DateTime.Now - process.StartTime;
            return uptime < TimeSpan.Zero ? TimeSpan.Zero : uptime;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return TimeSpan.Zero;
        }
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and not StackOverflowException;
}
