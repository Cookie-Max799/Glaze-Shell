using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Services;

namespace GlazeShell.Windows.SystemEvents;

/// <summary>
/// Результат запуска источников системных событий.
/// </summary>
public sealed record ShellEventStatus
{
    public bool WindowEventsStarted { get; init; }

    public bool DisplayEventsStarted { get; init; }

    public bool ProcessEventsStarted { get; init; }

    public bool AllSourcesStarted => WindowEventsStarted && DisplayEventsStarted && ProcessEventsStarted;

    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Единая точка управления источниками системных событий: оконные события
/// (<c>SetWinEventHook</c>), события дисплеев (hidden message window) и события процессов.
/// </summary>
/// <remarks>
/// <para>
/// Источники запускаются из одного места, чтобы ни один не был поднят дважды и ни один
/// не остался работать после закрытия приложения. Ошибка отдельного источника не
/// отменяет запуск остальных: неработающий источник попадает в
/// <see cref="ShellEventStatus.Diagnostics"/>, а подписки на его события просто не приходят.
/// </para>
/// <para>
/// Координатор получает <see cref="IWindowManager"/> и <see cref="IMonitorManager"/> во владение:
/// <see cref="Dispose"/> освобождает и их. Сервисы нужно создать отдельно только затем,
/// что они передаются в UI как источники запросов.
/// </para>
/// </remarks>
public sealed class ShellEventCoordinator : IDisposable
{
    private readonly IWindowManager _windows;
    private readonly IMonitorManager _monitors;
    private readonly ProcessEventMonitor _processes;
    private readonly Action<Exception> _errorHandler;
    private readonly object _sync = new();

    private bool _started;
    private bool _disposed;

    public ShellEventCoordinator(
        IEventManager events,
        IWindowManager windows,
        IMonitorManager monitors,
        ProcessWatchOptions? processWatch = null,
        Action<Exception>? errorHandler = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _monitors = monitors ?? throw new ArgumentNullException(nameof(monitors));
        _errorHandler = errorHandler ?? EventCoalescer.FallbackErrorHandler("ShellEventCoordinator");

        _processes = new ProcessEventMonitor(
            events,
            processWatch ?? ProcessWatchOptions.Default,
            _errorHandler);
    }

    public ShellEventStatus Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_sync)
        {
            if (_started)
            {
                throw new InvalidOperationException("The shell event coordinator has already been started.");
            }

            _started = true;
        }

        var diagnostics = new List<string>();

        var windowEvents = TryStart(
            "window events",
            "Window events will not be published: the WinEvent hook could not be installed.",
            _windows.Start,
            diagnostics);

        var displayEvents = TryStart(
            "display events",
            "Display events will not be published: the message window could not be created.",
            _monitors.Start,
            diagnostics);

        var processEvents = TryStart(
            "process events",
            "Process events will not be published: the process snapshot could not be started.",
            _processes.Start,
            diagnostics);

        return new ShellEventStatus
        {
            WindowEventsStarted = windowEvents,
            DisplayEventsStarted = displayEvents,
            ProcessEventsStarted = processEvents,
            Diagnostics = diagnostics,
        };
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
        }

        _processes.Dispose();
        DisposeSafely(_windows);
        DisposeSafely(_monitors);
    }

    private bool TryStart(string name, string diagnostic, Func<bool> start, List<string> diagnostics)
    {
        try
        {
            if (start())
            {
                return true;
            }

            diagnostics.Add(diagnostic);
            return false;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            diagnostics.Add($"{diagnostic} {exception.GetType().Name}: {exception.Message}");
            _errorHandler(new InvalidOperationException($"Starting {name} failed.", exception));
            return false;
        }
    }

    private void DisposeSafely(IDisposable service)
    {
        try
        {
            service.Dispose();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            _errorHandler(exception);
        }
    }
}
