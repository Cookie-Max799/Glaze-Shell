using System.Globalization;
using GlazeShell.App.Theming;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Core.Switching;
using GlazeShell.Infrastructure.Logging;
using GlazeShell.Windows.Shell;
using GlazeShell.Windows.Switching;
using GlazeShell.Windows.WindowManagement;
using Microsoft.UI.Dispatching;

namespace GlazeShell.App;

public sealed class AppSwitcherController : IDisposable
{
    private static readonly TimeSpan GuardInterval = TimeSpan.FromMilliseconds(100);

    private readonly ISettingsManager _settings;
    private readonly IMonitorManager _monitors;
    private readonly ThemeApplicator _themes;
    private readonly IEventManager _events;
    private readonly DispatcherQueue _dispatcher;
    private readonly FileLogger _logger;

    private readonly SwitchableApplicationProvider _provider = new();
    private readonly RecentWindowOrder _recent = new();
    private readonly HashSet<SwitchableWindow> _hidden = [];

    private readonly AltTabInterceptor _interceptor;
    private readonly DispatcherQueueTimer _showTimer;
    private readonly DispatcherQueueTimer _guardTimer;
    private readonly IDisposable _foregroundSubscription;

    private AppSwitcherWindow? _window;
    private MonitorInfo? _monitor;
    private WindowSwitchSession? _session;
    private bool _panelRevealed;
    private bool _disposed;

    public event Action<bool>? OverviewVisibilityChanged;

    public AppSwitcherController(
        ISettingsManager settings,
        IMonitorManager monitors,
        ThemeApplicator themes,
        IEventManager events,
        DispatcherQueue dispatcher,
        FileLogger logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _monitors = monitors ?? throw new ArgumentNullException(nameof(monitors));
        _themes = themes ?? throw new ArgumentNullException(nameof(themes));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _interceptor = new AltTabInterceptor();

        _interceptor.SwitchStarted += OnSwitchStarted;
        _interceptor.MoveRequested += OnMoveRequested;
        _interceptor.MoveRowRequested += OnMoveRowRequested;
        _interceptor.CancelRequested += OnCancelRequested;
        _interceptor.CloseRequested += OnCloseRequested;
        _interceptor.HideRequested += OnHideRequested;
        _interceptor.CommitRequested += OnCommitRequested;
        _interceptor.WindowCycleRequested += OnWindowCycleRequested;

        _showTimer = dispatcher.CreateTimer();
        _showTimer.IsRepeating = false;
        _showTimer.Tick += OnShowTimerTick;

        _guardTimer = dispatcher.CreateTimer();
        _guardTimer.Interval = GuardInterval;
        _guardTimer.Tick += OnGuardTimerTick;

        _foregroundSubscription = _events.Subscribe<ForegroundWindowChanged>(OnForegroundChanged);
    }

    /// <summary>
    /// Устанавливает хук. Возвращает <see langword="false"/>, если перехват недоступен:
    /// тогда системный Alt+Tab продолжает работать как обычно.
    /// </summary>
    public bool Start()
    {
        if (_disposed)
        {
            return false;
        }

        if (_interceptor.Start())
        {
            _interceptor.SetEnabled(true);
            _ = Task.Run(_provider.Warm);
            return true;
        }

        _logger.Write(
            GlazeLogLevel.Warning,
            "AltTab",
            "The Alt+Tab hook was not installed; the system window switcher stays in place.");
        return false;
    }

    /// <summary>Включает или выключает перехват без снятия хука.</summary>
    public void SetEnabled(bool enabled)
    {
        if (_disposed)
        {
            return;
        }

        _interceptor.SetEnabled(enabled);

        if (!enabled)
        {
            RunOnUi(() => Cancel());
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _showTimer.Stop();
        _guardTimer.Stop();
        _showTimer.Tick -= OnShowTimerTick;
        _guardTimer.Tick -= OnGuardTimerTick;

        _interceptor.SwitchStarted -= OnSwitchStarted;
        _interceptor.MoveRequested -= OnMoveRequested;
        _interceptor.MoveRowRequested -= OnMoveRowRequested;
        _interceptor.CancelRequested -= OnCancelRequested;
        _interceptor.CloseRequested -= OnCloseRequested;
        _interceptor.HideRequested -= OnHideRequested;
        _interceptor.CommitRequested -= OnCommitRequested;
        _interceptor.WindowCycleRequested -= OnWindowCycleRequested;
        _interceptor.Dispose();

        _foregroundSubscription.Dispose();

        EndSession(null);
        _window?.Close();
        _window = null;
    }

    private void OnSwitchStarted(bool backwards) => RunOnUi(() => BeginSwitch(backwards));

    private void OnMoveRequested(int delta) => RunOnUi(() => MoveSelection(delta));

    private void MoveSelection(int delta)
    {
        if (_session is null || !_session.Move(delta) || !_panelRevealed)
        {
            return;
        }

        _window?.SetSelection(_session.SelectedIndex);
    }

    private void OnMoveRowRequested(int delta) =>
        RunOnUi(() => MoveSelection(delta * (_panelRevealed ? _window!.Columns : 1)));

    private void OnCancelRequested() => RunOnUi(() => Cancel());

    private void OnCloseRequested() => RunOnUi(CloseSelected);

    private void OnHideRequested() => RunOnUi(HideSelected);

    private void OnCommitRequested(bool shift) => RunOnUi(() => Commit(shift));

    private void OnWindowCycleRequested() => RunOnUi(() =>
    {
        try
        {
            _ = ApplicationActivationService.CycleForegroundWindow();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            _logger.Write(GlazeLogLevel.Warning, "AltTab", "Switching windows of the current application failed.", exception);
        }
    });

    private void BeginSwitch(bool backwards)
    {
        if (_disposed || _session is not null)
        {
            return;
        }

        _ = OverlayWindow.TryGetForegroundWindow(out var foreground, out _);
        _recent.Touch((long)foreground);

        IReadOnlyList<SwitchableWindow> windows;

        try
        {
            windows = _provider.Snapshot(_recent);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            _logger.Write(GlazeLogLevel.Warning, "AltTab", "The window snapshot failed.", exception);
            return;
        }

        var monitor = ResolveMonitor();

        if (monitor is null)
        {
            return;
        }

        _session = new WindowSwitchSession(windows, (long)foreground, backwards);

        _hidden.Clear();
        _panelRevealed = false;

        _monitor = monitor;

        _guardTimer.Start();
        var delay = _settings.GetSettings().AltTabPanelDelayMs;

        if (delay <= 0)
        {
            Invoke(RevealPanel);
        }
        else
        {
            _showTimer.Interval = TimeSpan.FromMilliseconds(delay);
            _showTimer.Start();
        }
    }

    private void RevealPanel()
    {
        if (_session is not { } session || _panelRevealed)
        {
            return;
        }

        if (_monitor is not { } monitor)
        {
            return;
        }

        EnsureWindow();
        _panelRevealed = true;
        try
        {
            _window!.ShowOverview(session.Items, session.SelectedIndex, monitor);
            OverviewVisibilityChanged?.Invoke(true);
        }
        catch
        {
            EndSession(null);
            throw;
        }
    }

    private void Commit(bool restoreMinimized)
    {
        var selected = _session?.Selected;
        var restore = selected is not null && _hidden.Contains(selected);
        EndSession(selected);

        if (selected is not null &&
            ApplicationActivationService.Activate(selected, restore || restoreMinimized || _settings.GetSettings().AltTabRestoreMinimized))
        {
            _recent.Touch(selected.Handle);
        }
    }

    private void Cancel() => EndSession(null);

    private void EndSession(SwitchableWindow? selected)
    {
        _showTimer.Stop();
        _guardTimer.Stop();
        _session = null;
        _monitor = null;
        _interceptor.ResetSwitching();

        if (_panelRevealed)
        {
            _panelRevealed = false;
            try
            {
                _window?.HideOverview();
            }
            finally
            {
                OverviewVisibilityChanged?.Invoke(false);
            }
        }

        foreach (var window in _hidden)
        {
            if (window != selected)
            {
                ApplicationActivationService.Restore(window);
            }
        }

        _hidden.Clear();
    }

    private void CloseSelected()
    {
        if (_session is not { Selected: { } selected } session)
        {
            return;
        }

        if (!ApplicationActivationService.Close(selected))
        {
            return;
        }

        if (_hidden.Remove(selected))
        {
            ApplicationActivationService.Restore(selected);
        }

        session.RemoveSelected();
        if (_panelRevealed)
        {
            _window?.Refresh(session.Items, session.SelectedIndex);
        }
    }

    private void HideSelected()
    {
        if (_session?.Selected is { } selected && ApplicationActivationService.Minimize(selected))
        {
            _hidden.Add(selected);
        }
    }

    private void OnShowTimerTick(DispatcherQueueTimer sender, object args)
    {
        _showTimer.Stop();

        if (_session is not null && !_panelRevealed)
        {
            Invoke(RevealPanel);
        }
    }

    private void OnGuardTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (_session is null)
        {
            _guardTimer.Stop();
            return;
        }

        if (!OverlayWindow.IsAltKeyDown())
        {
            // Страховка от потерянного события: Alt уже отпущен, режим завершается.
            Commit(restoreMinimized: false);
        }
    }

    private void OnForegroundChanged(ForegroundWindowChanged changed)
    {
        if (changed.Window is { } window &&
            long.TryParse(window.Id, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var handle))
        {
            RunOnUi(() => _recent.Touch(handle));
        }
    }

    private MonitorInfo? ResolveMonitor()
    {
        try
        {
            // Активное окно важнее курсора: Alt+Tab нажимают с клавиатуры, и курсор
            // может остаться на другом мониторе. Курсор — запасной вариант.
            if (OverlayWindow.TryGetForegroundWindow(out var hwnd, out _))
            {
                var windowId = string.Create(CultureInfo.InvariantCulture, $"{hwnd:X}");

                if (_monitors.GetMonitorForWindow(windowId) is { } activeMonitor)
                {
                    return activeMonitor;
                }
            }

            if (OverlayWindow.TryGetCursorScreenPosition(out var x, out var y) &&
                _monitors.GetMonitorForPoint(x, y) is { } cursorMonitor)
            {
                return cursorMonitor;
            }

            return _monitors.GetPrimaryMonitor();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            _logger.Write(GlazeLogLevel.Warning, "AltTab", "Resolving the target monitor failed.", exception);
            return null;
        }
    }

    private void EnsureWindow()
    {
        if (_window is not null)
        {
            return;
        }

        var window = new AppSwitcherWindow(_themes);

        window.ItemActivated += OnWindowItemActivated;
        _window = window;
    }

    private void OnWindowItemActivated(int index)
    {
        if (_session is null)
        {
            return;
        }

        if (_session.Select(index) || index == _session.SelectedIndex)
        {
            Commit(restoreMinimized: true);
        }
    }

    private void RunOnUi(Action action)
    {
        if (_disposed)
        {
            return;
        }

        if (_dispatcher.HasThreadAccess)
        {
            Invoke(action);
            return;
        }

        _ = _dispatcher.TryEnqueue(() => Invoke(action));
    }

    private void Invoke(Action action)
    {
        try
        {
            if (!_disposed)
            {
                action();
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            _logger.Write(GlazeLogLevel.Error, "AltTab", "The window switcher failed.", exception);
        }
    }
}
