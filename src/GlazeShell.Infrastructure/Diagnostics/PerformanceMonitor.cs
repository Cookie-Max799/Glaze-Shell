using GlazeShell.Core.Diagnostics;
using GlazeShell.Infrastructure.Logging;

namespace GlazeShell.Infrastructure.Diagnostics;

/// <summary>
/// Снимает baseline производительности при старте и итог при закрытии, записывая их в лог.
/// </summary>
/// <remarks>
/// <para>
/// Профилирование включается явно: baseline снимается один раз в момент, когда окно уже
/// показано, поэтому время работы процесса в этом снимке — фактическое время запуска.
/// Итог при закрытии считает среднюю загрузку процессора и прирост памяти, потоков и handles.
/// </para>
/// <para>
/// Периодическая выборка idle-состояния выключена по умолчанию и включается вызовом
/// <see cref="StartSampling"/>: постоянно работающий таймер — это фоновая нагрузка, которую
/// не следует создавать без конкретной потребности профилирования.
/// </para>
/// </remarks>
public sealed class PerformanceMonitor : IDisposable
{
    private readonly IGlazeLogger _logger;
    private readonly object _sync = new();

    private PerformanceSnapshot? _baseline;
    private Timer? _sampler;
    private int _disposed;

    public PerformanceMonitor(IGlazeLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Снимает и записывает опорный снимок, относительно которого считается итог сессии.
    /// </summary>
    public void LogBaseline(string label = "startup")
    {
        var snapshot = ProcessMetricsCollector.Capture(label);

        lock (_sync)
        {
            _baseline = snapshot;
        }

        _logger.Write(GlazeLogLevel.Information, "Performance", $"Baseline ({label}): {snapshot.ToLogString()}.");
    }

    /// <summary>
    /// Записывает единичный снимок. Используется периодической выборкой и ручными замерами.
    /// </summary>
    public void LogSnapshot(string label)
    {
        var snapshot = ProcessMetricsCollector.Capture(label);
        _logger.Write(GlazeLogLevel.Information, "Performance", $"Snapshot ({label}): {snapshot.ToLogString()}.");
    }

    /// <summary>
    /// Включает периодическую запись снимков: так снимается idle CPU и RAM за спокойный
    /// промежуток времени. Повторный вызов не создаёт второй таймер.
    /// </summary>
    public void StartSampling(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "The sampling interval must be positive.");
        }

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            _sampler ??= new Timer(OnSample, null, interval, interval);
        }
    }

    /// <summary>
    /// Снимает конечный снимок и считает итог сессии относительно baseline. Если baseline
    /// не снимался, итог не пишется: без опорного значения дельты не имеют смысла.
    /// </summary>
    /// <param name="label">Метка конечного снимка.</param>
    /// <returns>Сводка и конечный снимок, либо <see langword="null"/>, если baseline не снят.</returns>
    public PerformanceSessionResult? LogSession(string label = "shutdown")
    {
        PerformanceSnapshot? baseline;

        lock (_sync)
        {
            baseline = _baseline;
        }

        if (baseline is null)
        {
            return null;
        }

        var end = ProcessMetricsCollector.Capture(label);
        var summary = PerformanceSession.Summarize(baseline, end, Environment.ProcessorCount);

        _logger.Write(GlazeLogLevel.Information, "Performance", $"End ({label}): {end.ToLogString()}.");
        _logger.Write(GlazeLogLevel.Information, "Performance", $"Session summary: {summary.ToLogString()}.");

        return new PerformanceSessionResult(summary, end);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        lock (_sync)
        {
            _sampler?.Dispose();
            _sampler = null;
        }
    }

    private void OnSample(object? state)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            LogSnapshot("idle");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            // Выборка идёт из потока таймера: неперехваченное исключение завершило бы процесс.
            _logger.Write(GlazeLogLevel.Warning, "Performance", "Idle sampling failed.", exception);
        }
    }
}
