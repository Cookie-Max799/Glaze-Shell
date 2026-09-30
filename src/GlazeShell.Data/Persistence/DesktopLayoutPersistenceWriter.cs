using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;

namespace GlazeShell.Data.Persistence;

/// <summary>
/// Автосохранение desktop layout по событию <see cref="DesktopChanged"/>.
/// </summary>
/// <remarks>
/// Изменения вкладок идят пачками (перетаскивание, массовое удаление), поэтому запись
/// коалесцируется коротким одноразовым таймером: сохраняется только последний layout.
/// Таймер не является polling — он запускается событием и срабатывает один раз.
/// <see cref="Flush"/> записывает отложенное состояние немедленно и вызывается при закрытии окна,
/// чтобы последнее изменение не потерялось.
/// </remarks>
public sealed class DesktopLayoutPersistenceWriter : IDisposable
{
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(400);

    private readonly IUserDataStore _store;
    private readonly PersistenceErrorReporter? _errorReporter;
    private readonly TimeSpan _delay;
    private readonly Timer _timer;
    private readonly object _sync = new();

    private IDisposable? _subscription;
    private DesktopLayout? _pending;
    private bool _disposed;

    public DesktopLayoutPersistenceWriter(
        IUserDataStore store,
        IEventManager eventManager,
        PersistenceErrorReporter? errorReporter = null,
        TimeSpan? delay = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        ArgumentNullException.ThrowIfNull(eventManager);

        if (delay is { } configuredDelay && configuredDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), configuredDelay, "The delay cannot be negative.");
        }

        _errorReporter = errorReporter;
        _delay = delay ?? DefaultDelay;
        _timer = new Timer(OnElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _subscription = eventManager.Subscribe<DesktopChanged>(OnDesktopChanged);
    }

    /// <summary>
    /// Записывает отложенный layout, если он есть.
    /// </summary>
    public void Flush()
    {
        DesktopLayout? pending;

        lock (_sync)
        {
            pending = _pending;
            _pending = null;
        }

        if (pending is not null)
        {
            Save(pending);
        }
    }

    public void Dispose()
    {
        IDisposable? subscription;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timer.Dispose();
            subscription = _subscription;
            _subscription = null;
        }

        subscription?.Dispose();
        Flush();
    }

    private void OnDesktopChanged(DesktopChanged changed)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _pending = changed.Layout;
            _timer.Change(_delay, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnElapsed(object? state)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }
        }

        Flush();
    }

    private void Save(DesktopLayout layout)
    {
        try
        {
            _store.SaveLayout(layout);
        }
        catch (Exception exception) when (exception is PersistenceException or IOException or UnauthorizedAccessException)
        {
            _errorReporter?.Invoke("The desktop layout could not be saved.", exception);
        }
    }
}
