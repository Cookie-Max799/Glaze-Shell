using System.Globalization;
using GlazeShell.Core.Models;

namespace GlazeShell.Core.Diagnostics;

/// <summary>
/// Бюджеты производительности оболочки (функция I6).
/// </summary>
/// <remarks>
/// <para>
/// Бюджеты — измеримое правило, а не пожелание: без числа нельзя сказать, «стало легче»
/// или «стало тяжелее». Значения по умолчанию взяты из карты функций: idle CPU меньше
/// одного процента, idle-память меньше 200 МБ, открытие Glance быстрее 100 мс.
/// </para>
/// <para>
/// Правило живёт в ядре без WinUI и без файлов: измерения приходят аргументами, а вердикт
/// считается обычной функцией, поэтому его можно проверить тестом.
/// </para>
/// </remarks>
public sealed record PerformanceBudget
{
    public PerformanceBudget(double idleCpuPercent, long idleMemoryBytes, TimeSpan glanceOpen)
    {
        ModelValidation.NonNegative(idleCpuPercent, nameof(idleCpuPercent));
        ModelValidation.NonNegative(idleMemoryBytes, nameof(idleMemoryBytes));
        ModelValidation.NonNegative(glanceOpen, nameof(glanceOpen));

        IdleCpuPercent = idleCpuPercent;
        IdleMemoryBytes = idleMemoryBytes;
        GlanceOpen = glanceOpen;
    }

    /// <summary>Допустимая загрузка процессора в простое, в процентах.</summary>
    public double IdleCpuPercent { get; }

    /// <summary>Допустимая память в простое, в байтах.</summary>
    public long IdleMemoryBytes { get; }

    /// <summary>Допустимое время открытия Glance.</summary>
    public TimeSpan GlanceOpen { get; }
}

/// <summary>
/// Бюджеты производительности по умолчанию.
/// </summary>
public static class PerformanceBudgets
{
    /// <summary>Бюджеты из карты функций I6.</summary>
    public static PerformanceBudget Default { get; } = new(
        idleCpuPercent: 1.0,
        idleMemoryBytes: 200L * 1024 * 1024,
        glanceOpen: TimeSpan.FromMilliseconds(100));
}

/// <summary>
/// Фактические измерения для проверки бюджетов (функция I6).
/// </summary>
/// <remarks>
/// Отсутствующее измерение — это «не измерено», а не «ноль»: например, Glance мог ни разу не
/// открываться за сессию. Такое измерение просто не попадает в отчёт.
/// </remarks>
public sealed record PerformanceMeasurement
{
    public PerformanceMeasurement(double idleCpuPercent, long idleMemoryBytes, TimeSpan? glanceOpen = null)
    {
        ModelValidation.NonNegative(idleCpuPercent, nameof(idleCpuPercent));
        ModelValidation.NonNegative(idleMemoryBytes, nameof(idleMemoryBytes));

        if (glanceOpen is { } open && open < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(glanceOpen), open, "The duration cannot be negative.");
        }

        IdleCpuPercent = idleCpuPercent;
        IdleMemoryBytes = idleMemoryBytes;
        GlanceOpen = glanceOpen;
    }

    /// <summary>Загрузка процессора в простое, в процентах.</summary>
    public double IdleCpuPercent { get; }

    /// <summary>Память в простое, в байтах.</summary>
    public long IdleMemoryBytes { get; }

    /// <summary>Время открытия Glance, если Glance открывался в сессии.</summary>
    public TimeSpan? GlanceOpen { get; }
}

/// <summary>
/// Проверка одного бюджета: измеренное значение, предел и вердикт.
/// </summary>
/// <param name="Name">Название проверки.</param>
/// <param name="Measured">Измеренное значение.</param>
/// <param name="Limit">Предел бюджета.</param>
/// <param name="Unit">Единица измерения для журнала.</param>
public sealed record PerformanceBudgetCheck(string Name, double Measured, double Limit, string Unit)
{
    /// <summary>Укладывается ли измерение в бюджет.</summary>
    public bool IsWithinBudget => Measured <= Limit;

    /// <summary>Строка для журнала.</summary>
    public string ToLogString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Name}: {Measured:F2} {Unit} (budget {Limit:F2} {Unit})");
}

/// <summary>
/// Отчёт о проверке бюджетов (функция I6).
/// </summary>
/// <param name="Checks">Проверки по каждому измеренному бюджету.</param>
public sealed record PerformanceBudgetReport(IReadOnlyList<PerformanceBudgetCheck> Checks)
{
    /// <summary>Все ли измерения укладываются в бюджеты.</summary>
    public bool IsWithinBudget => Checks.All(static check => check.IsWithinBudget);

    /// <summary>Строка для журнала: значения и вердикт целиком.</summary>
    public string ToLogString()
    {
        if (Checks.Count == 0)
        {
            return "Performance budget: nothing was measured.";
        }

        var details = string.Join(", ", Checks.Select(static check => check.ToLogString()));
        var verdict = IsWithinBudget ? "within budget" : "over budget";

        return string.Create(CultureInfo.InvariantCulture, $"Performance budget: {details} — {verdict}.");
    }
}

/// <summary>
/// Проверка измерений против бюджетов (функция I6).
/// </summary>
public static class PerformanceBudgetEvaluator
{
    private const double BytesInMegabyte = 1024d * 1024d;

    /// <summary>
    /// Сравнивает измерения с бюджетами и возвращает отчёт.
    /// </summary>
    /// <param name="budget">Бюджеты, с которыми сравниваются измерения.</param>
    /// <param name="measurement">Измеренные значения.</param>
    /// <returns>Отчёт по каждому измеренному бюджету.</returns>
    public static PerformanceBudgetReport Evaluate(PerformanceBudget budget, PerformanceMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(measurement);

        var checks = new List<PerformanceBudgetCheck>(3)
        {
            new("idle cpu", measurement.IdleCpuPercent, budget.IdleCpuPercent, "%"),
            new("idle memory", measurement.IdleMemoryBytes / BytesInMegabyte, budget.IdleMemoryBytes / BytesInMegabyte, "MB"),
        };

        if (measurement.GlanceOpen is { } glanceOpen)
        {
            checks.Add(new("glance open", glanceOpen.TotalMilliseconds, budget.GlanceOpen.TotalMilliseconds, "ms"));
        }

        return new PerformanceBudgetReport(checks);
    }
}
