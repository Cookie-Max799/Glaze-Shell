using System.Diagnostics;

namespace GlazeShell.Windows.SystemEvents;

/// <summary>
/// Снимок одного процесса: идентификатор, имя образа и путь к исполняемому файлу.
/// </summary>
internal readonly record struct ProcessSnapshotEntry(int ProcessId, string Name, string? ExecutablePath);

/// <summary>
/// Источник снимков списка процессов для <see cref="ProcessEventMonitor"/>.
/// </summary>
/// <remarks>
/// Выделен в отдельный контракт по той же причине, что и <c>IProcessInspector</c> в слое
/// приложений: перечисление процессов — единственное место, где тест наблюдает побочный
/// эффект (снимок системы), и подменить его детерминированной реализацией дешевле, чем
/// запускать и убивать настоящие процессы в каждом прогоне.
/// </remarks>
internal interface IProcessSnapshotSource
{
    /// <summary>
    /// Возвращает текущий снимок процессов. Процессы, недоступные для чтения
    /// (завершившиеся или защищённые), в снимок не попадают.
    /// </summary>
    IReadOnlyList<ProcessSnapshotEntry> Capture();
}

/// <summary>
/// Реальный источник снимков поверх <see cref="Process.GetProcesses"/>.
/// </summary>
internal sealed class SystemProcessSnapshotSource : IProcessSnapshotSource
{
    public IReadOnlyList<ProcessSnapshotEntry> Capture()
    {
        var entries = new List<ProcessSnapshotEntry>();

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                var entry = TryRead(process);

                if (entry is { } value)
                {
                    entries.Add(value);
                }
            }
        }

        return entries;
    }

    private static ProcessSnapshotEntry? TryRead(Process process)
    {
        int processId;
        string name;

        try
        {
            processId = process.Id;
            name = process.ProcessName ?? string.Empty;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return null;
        }

        if (name.Length == 0)
        {
            return null;
        }

        return new ProcessSnapshotEntry(processId, name, TryReadPath(process));
    }

    private static string? TryReadPath(Process process)
    {
        try
        {
            var path = process.MainModule?.FileName;
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return null;
        }
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and not StackOverflowException;
}
