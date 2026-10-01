using System.Diagnostics;

namespace GlazeShell.Core.Services;

/// <summary>
/// Объединяет частые запросы в одно выполнение: пока запросы продолжаются, действие
/// откладывается, и после паузы в <see cref="Delay"/> выполняется один раз.
/// Используется там, где один системный сигнал приходит пачкой сообщений
/// (например, <c>WM_DISPLAYCHANGE</c> и <c>WM_DEVICECHANGE</c> при подключении монитора),
/// а перечисление мониторов на каждое сообщение было бы лишней работой.
/// </summary>
/// <remarks>
/// Класс не создаёт собственного потока: действие выполняется в потоке таймера.
/// Поэтому обработчик ошибок обязателен — неперехваченное исключение в потоке таймера
/// завершило бы процесс, а молча проглотить ошибку нельзя.
/// </remarks>
public sealed class EventCoalescer : IDisposable
{
    private static readonly TimeSpan InfiniteDelay = Timeout.InfiniteTimeSpan;

    private readonly TimeSpan _delay;
    private readonly Action _action;
    private readonly Action<Exception> _errorHandler;
    private readonly object _sync = new();
    private readonly Timer _timer;

    private bool _pending;
    private bool _disposed;

    public EventCoalescer(TimeSpan delay, Action action, Action<Exception> errorHandler)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "The delay must not be negative.");
        }

        _delay = delay;
        _action = action ?? throw new ArgumentNullException(nameof(action));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _timer = new Timer(OnElapsed, null, InfiniteDelay, InfiniteDelay);
    }

    /// <summary>
    /// Запрашивает выполнение действия. Запросы, пришедшие до истечения задержки,
    /// приводят к одному выполнению; уже запланированное выполнение переносится на паузу.
    /// </summary>
    public void Request()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _pending = true;
            _timer.Change(_delay, InfiniteDelay);
        }
    }

    /// <summary>
    /// Выполняет действие немедленно, если выполнение уже запланировано.
    /// Используется при завершении работы, чтобы не потерять последний сигнал.
    /// </summary>
    public void Flush()
    {
        bool execute;

        lock (_sync)
        {
            if (_disposed || !_pending)
            {
                return;
            }

            _pending = false;
            execute = true;
            _timer.Change(InfiniteDelay, InfiniteDelay);
        }

        if (execute)
        {
            Invoke();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pending = false;
            _timer.Change(InfiniteDelay, InfiniteDelay);
        }

        _timer.Dispose();
    }

    /// <summary>
    /// Отменяет отложенное выполнение без запуска действия. Используется при
    /// отключении источника событий, чтобы он не публиковал данные после остановки.
    /// </summary>
    public void Cancel()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _pending = false;
            _timer.Change(InfiniteDelay, InfiniteDelay);
        }
    }

    private void OnElapsed(object? state)
    {
        lock (_sync)
        {
            if (_disposed || !_pending)
            {
                return;
            }

            _pending = false;
        }

        Invoke();
    }

    private void Invoke()
    {
        try
        {
            _action();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            _errorHandler(exception);
        }
    }

    /// <summary>
    /// Последний получатель ошибки для сервисов, которым владелец не передал обработчик:
    /// ошибка попадает в вывод отладки, а не теряется и не завершает процесс.
    /// </summary>
    public static Action<Exception> FallbackErrorHandler(string component) =>
        exception => Trace.WriteLine($"GlazeShell.{component}: {exception}");
}
