using System.Diagnostics;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;

namespace GlazeShell.Windows.SystemEvents;

/// <summary>
/// Публикует <see cref="ProcessStarted"/> и <see cref="ProcessExited"/> по снимкам
/// списка процессов.
/// </summary>
/// <remarks>
/// <para>
/// Событие «процесс запущен» в Windows отсутствует: <c>SetWinEventHook</c> работает с окнами,
/// ETW требует трассировки с правами администратора, а WMI-события требуют службы, которая
/// часто отключена, и заметного объёма кода. Поэтому источник сверяет один системный снимок
/// через заданный интервал и публикует только разницу.
/// </para>
/// <para>
/// Снимок стоит одного перечисления процессов: имя и путь читаются только для новых
/// процессов, а для уже известных используется кэш предыдущего снимка. Поэтому цена
/// прохода в установившемся состоянии не зависит от количества запущенных программ.
/// Первый снимок — базовый и событий не порождает: процессы, работающие до запуска
/// Glaze Shell, не считаются только что запущенными.
/// </para>
/// <para>
/// Если на события никто не подписан, перечисление не выполняется вовсе.
/// </para>
/// </remarks>
internal sealed class ProcessEventMonitor : IDisposable
{
    private readonly IEventManager _events;
    private readonly Action<Exception> _errorHandler;
    private readonly TimeSpan _interval;
    private readonly Dictionary<int, ProcessEntry> _snapshot = [];
    private readonly object _sync = new();
    private readonly Timer _timer;

    private int _probing;
    private bool _started;
    private bool _disposed;

    internal ProcessEventMonitor(IEventManager events, ProcessWatchOptions options, Action<Exception> errorHandler)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _interval = (options ?? throw new ArgumentNullException(nameof(options))).Validate().Interval;
        _timer = new Timer(OnTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    internal bool Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_started)
        {
            throw new InvalidOperationException("The process event monitor has already been started.");
        }

        _started = true;

        try
        {
            ReplaceSnapshot(ReadSnapshot());
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            // Базовая линия недоступна: события запуска по-прежнему публикуются после
            // первого успешного снимка, но первый из них будет выглядеть как массовый запуск.
            _errorHandler(exception);
        }

        _timer.Change(_interval, _interval);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timer.Dispose();

        lock (_sync)
        {
            _snapshot.Clear();
        }
    }

    private void OnTick(object? state)
    {
        if (_disposed || !HasListeners())
        {
            return;
        }

        // Перечисление не должно накладываться на себя: интервал меньше времени прохода
        // просто приводит к пропуску такта, а не к двум параллельным проходам.
        if (Interlocked.CompareExchange(ref _probing, 1, 0) != 0)
        {
            return;
        }

        try
        {
            Probe();
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            _errorHandler(exception);
        }
        finally
        {
            Volatile.Write(ref _probing, 0);
        }
    }

    private bool HasListeners() => _events.HasSubscribers<ProcessStarted>() || _events.HasSubscribers<ProcessExited>();

    private void Probe()
    {
        var current = ReadSnapshot();
        var exited = new List<ProcessExited>();
        var started = new List<ProcessStarted>();

        lock (_sync)
        {
            foreach (var entry in _snapshot)
            {
                if (!current.ContainsKey(entry.Key))
                {
                    exited.Add(new ProcessExited(entry.Key, entry.Value.Name, entry.Value.ExecutablePath));
                }
            }

            foreach (var entry in current)
            {
                if (!_snapshot.ContainsKey(entry.Key))
                {
                    started.Add(new ProcessStarted(entry.Key, entry.Value.Name, entry.Value.ExecutablePath));
                }
            }

            _snapshot.Clear();
            foreach (var entry in current)
            {
                _snapshot[entry.Key] = entry.Value;
            }
        }

        // Выход публикуется раньше запуска: перезапуск приложения должен читаться
        // как «закрыто, затем открыто», а не наоборот.
        foreach (var process in exited)
        {
            _events.Publish(process);
        }

        foreach (var process in started)
        {
            _events.Publish(process);
        }
    }

    private Dictionary<int, ProcessEntry> ReadSnapshot()
    {
        var current = new Dictionary<int, ProcessEntry>();

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                int processId;
                try
                {
                    processId = process.Id;
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    continue;
                }

                if (TryGetCached(processId, out var cached))
                {
                    current[processId] = cached;
                    continue;
                }

                var name = ReadName(process);

                if (name.Length == 0)
                {
                    // Процесс закрылся или недоступен для чтения (например, системный):
                    // в снимке он учитывается, но событие о нём не публикуется.
                    continue;
                }

                current[processId] = new ProcessEntry(name, ReadPath(process));
            }
        }

        return current;
    }

    private bool TryGetCached(int processId, out ProcessEntry entry)
    {
        lock (_sync)
        {
            return _snapshot.TryGetValue(processId, out entry);
        }
    }

    private void ReplaceSnapshot(Dictionary<int, ProcessEntry> snapshot)
    {
        lock (_sync)
        {
            _snapshot.Clear();
            foreach (var entry in snapshot)
            {
                _snapshot[entry.Key] = entry.Value;
            }
        }
    }

    private static string ReadName(Process process)
    {
        try
        {
            return process.ProcessName ?? string.Empty;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return string.Empty;
        }
    }

    private static string? ReadPath(Process process)
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

    private readonly record struct ProcessEntry(string Name, string? ExecutablePath);
}
