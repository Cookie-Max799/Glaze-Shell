namespace GlazeShell.Core.Diagnostics;

/// <summary>
/// Результат сессии производительности: сводка между двумя снимками и конечный снимок.
/// </summary>
/// <param name="Summary">Итог сессии (дельты и средняя загрузка процессора).</param>
/// <param name="End">Конечный снимок ресурсов процесса.</param>
public sealed record PerformanceSessionResult(PerformanceSessionSummary Summary, PerformanceSnapshot End);
