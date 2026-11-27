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

/// <summary>
/// Связывает клавиатурный перехват Alt+Tab с панелью переключателя: снимает снимок MRU,
/// ведёт выделение, обновляет порядок при каждой смене фокуса и активирует выбранное
/// приложение.
/// </summary>
/// <remarks>
/// <para>
/// События хука приходят с потока хука, поэтому все действия переносятся на поток
/// интерфейса. Снимок берётся только при входе в режим: приложения, появившиеся во время
/// переключения, не сдвигают выделение.
/// </para>
/// <para>
/// Панель показывается с задержкой из настроек. Если Alt отпущен раньше, приложение
/// переключается без панели — это и есть быстрое нажатие Alt+Tab. Отпускание Alt
/// дополнительно проверяется опросом: даже если событие хука потерялось, режим завершится
/// и не оставит панель на экране.
/// </para>
/// </remarks>
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
    private readonly RecentApplicationOrder _recent = new();
    private readonly HashSet<string> _hidden = new(StringComparer.Ordinal);

    private readonly AltTabInterceptor _interceptor;
    private readonly DispatcherQueueTimer _showTimer;
    private readonly DispatcherQueueTimer _guardTimer;
    private readonly IDisposable _foregroundSubscription;

    private AppSwitcherWindow? _window;
    private ApplicationSwitchSession? _session;
    private bool _panelRevealed;
    private bool _disposed;

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
        _interceptor.WindowsToggleRequested += OnWindowsToggleRequested;
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
            RunOnUi(() => Cancel(activateNothing: true));
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
        _interceptor.WindowsToggleRequested -= OnWindowsToggleRequested;
        _interceptor.CancelRequested -= OnCancelRequested;
        _interceptor.CloseRequested -= OnCloseRequested;
        _interceptor.HideRequested -= OnHideRequested;
        _interceptor.CommitRequested -= OnCommitRequested;
        _interceptor.WindowCycleRequested -= OnWindowCycleRequested;
        _interceptor.Dispose();

        _foregroundSubscription.Dispose();

        _session = null;
        _window?.CloseWindow();
        _window = null;
    }

    private void OnSwitchStarted() => RunOnUi(BeginSwitch);

    private void OnMoveRequested(int delta) => RunOnUi(() =>
    {
        if (_session is null || !_session.Move(delta) || !_panelRevealed)
        {
            return;
        }

        _window?.SetSelection(_session.SelectedIndex);
    });

    private void OnWindowsToggleRequested() => RunOnUi(() =>
    {
        if (_session is null)
        {
            return;
        }

        _session.ToggleWindows();

        if (_panelRevealed)
        {
            UpdateWindowList();
        }
    });

    private void OnCancelRequested() => RunOnUi(() => Cancel(activateNothing: true));

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

    private void BeginSwitch()
    {
        if (_disposed || _session is not null)
        {
            return;
        }

        TouchForeground();

        IReadOnlyList<SwitchableApplication> applications;

        try
        {
            applications = _provider.Snapshot(_recent);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            _logger.Write(GlazeLogLevel.Warning, "AltTab", "The application snapshot failed.", exception);
            return;
        }

        if (applications.Count < 2)
        {
            // Одно приложение переключать некуда: панель не показывается, режим не
            // начинается. Отпускание Alt придёт хуком и будет проигнорировано.
            return;
        }

        var monitor = ResolveMonitor();

        if (monitor is null)
        {
            return;
        }

        var foregroundId = GetForegroundGroupId();
        var selectNext = foregroundId is null ||
                         string.Equals(applications[0].Id, foregroundId, StringComparison.Ordinal);

        _session = new ApplicationSwitchSession(applications, selectNext);
        _hidden.Clear();
        _panelRevealed = false;

        EnsureWindow();
        _window!.Prepare(applications, _session.SelectedIndex, monitor);

        var delay = _settings.GetSettings().AltTabPanelDelayMs;

        if (delay <= 0)
        {
            RevealPanel();
        }
        else
        {
            _showTimer.Interval = TimeSpan.FromMilliseconds(delay);
            _showTimer.Start();
        }

        _guardTimer.Start();
    }

    private void RevealPanel()
    {
        if (_session is null || _panelRevealed)
        {
            return;
        }

        _panelRevealed = true;
        _window?.Reveal();
    }

    private void Commit(bool shift)
    {
        var session = _session;

        if (session is null)
        {
            return;
        }

        _showTimer.Stop();
        _guardTimer.Stop();
        _session = null;
        _panelRevealed = false;
        _interceptor.ResetSwitching();

        var selected = session.Selected;
        var preferredWindow = session.SelectedWindow is { } window ? (nint)window.Handle : 0;
        var hidden = selected is not null && _hidden.Contains(selected.Id);

        // Остальные скрытые клавишей H приложения возвращаются: H — временное действие
        // внутри переключения, а не команда «свернуть насовсем».
        var restoreLater = _hidden.Count == 0
            ? []
            : session.Items
                .Where(application => _hidden.Contains(application.Id) && !ReferenceEquals(application, selected))
                .ToArray();

        _hidden.Clear();
        _window?.Hide(animate: true);

        foreach (var application in restoreLater)
        {
            try
            {
                _ = ApplicationActivationService.Restore(application);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                _logger.Write(GlazeLogLevel.Warning, "AltTab", "Restoring a hidden application failed.", exception);
            }
        }

        if (selected is null)
        {
            return;
        }

        var settings = _settings.GetSettings();
        var restore = shift || settings.AltTabRestoreMinimized || hidden;

        try
        {
            if (ApplicationActivationService.Activate(selected, restore, preferredWindow))
            {
                _recent.Touch(selected.Id);
            }
            else
            {
                // Окно могло закрыться между снимком и выбором, либо принадлежит
                // приложению с повышенными правами — фокус система не отдала.
                _logger.Write(
                    GlazeLogLevel.Warning,
                    "AltTab",
                    $"The selected application '{selected.Name}' did not come to the foreground.");
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            _logger.Write(GlazeLogLevel.Warning, "AltTab", "Activating the selected application failed.", exception);
        }
    }

    private void Cancel(bool activateNothing)
    {
        _ = activateNothing;
        _showTimer.Stop();
        _guardTimer.Stop();
        _session = null;
        _hidden.Clear();
        _panelRevealed = false;
        _interceptor.ResetSwitching();
        _window?.Hide(animate: true);
    }

    private void CloseSelected()
    {
        if (_session is null)
        {
            return;
        }

        var removed = _session.RemoveSelected();

        if (removed is null)
        {
            return;
        }

        ApplicationActivationService.Close(removed);
        _hidden.Remove(removed.Id);

        if (_panelRevealed)
        {
            _window?.Remove(removed.Id);
            _window?.SetWindowList(null, -1);
            _window?.SetSelection(_session.SelectedIndex);
        }

        if (_session.IsEmpty)
        {
            Cancel(activateNothing: true);
        }
    }

    private void HideSelected()
    {
        if (_session?.Selected is not { } selected)
        {
            return;
        }

        ApplicationActivationService.Minimize(selected);
        _ = _hidden.Add(selected.Id);

        if (_panelRevealed)
        {
            _window?.SetHidden(selected.Id);
        }
    }

    private void UpdateWindowList()
    {
        if (_window is null)
        {
            return;
        }

        if (_session is not { WindowsExpanded: true, Selected: { } application })
        {
            _window.SetWindowList(null, -1);
            return;
        }

        _window.SetWindowList(
            application.Windows.Select(window => window.Title).ToArray(),
            _session.SelectedWindowIndex);
    }

    private void OnShowTimerTick(DispatcherQueueTimer sender, object args)
    {
        _showTimer.Stop();

        if (_session is not null && !_panelRevealed)
        {
            RevealPanel();
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
            Commit(shift: false);
        }
    }

    private void OnForegroundChanged(ForegroundWindowChanged changed)
    {
        if (_disposed || changed.Window is not { } window)
        {
            return;
        }

        RunOnUi(() =>
        {
            if (TryGetGroupId(window, out var id))
            {
                _recent.Touch(id);
            }
        });
    }

    private void TouchForeground()
    {
        if (TryGetForegroundGroupId(out var id))
        {
            _recent.Touch(id);
        }
    }

    private static string? GetForegroundGroupId()
    {
        return TryGetForegroundGroupId(out var id) ? id : null;
    }

    private static bool TryGetForegroundGroupId(out string id)
    {
        id = string.Empty;

        return OverlayWindow.TryGetForegroundWindow(out var hwnd, out _) &&
               SwitchableApplicationProvider.TryGetGroupId(hwnd, out id);
    }

    private static bool TryGetGroupId(WindowInfo window, out string id)
    {
        id = string.Empty;

        if (!long.TryParse(window.Id, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        return SwitchableApplicationProvider.TryGetGroupId(unchecked((nint)value), out id);
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
        window.SelectionRequested += OnWindowSelectionRequested;
        window.ItemActivated += OnWindowItemActivated;
        _window = window;
    }

    private void OnWindowSelectionRequested(int index)
    {
        if (_session is null || !_session.Select(index) || !_panelRevealed)
        {
            return;
        }

        _window?.SetSelection(_session.SelectedIndex);
    }

    private void OnWindowItemActivated(int index)
    {
        if (_session is null)
        {
            return;
        }

        if (_session.Select(index) || index == _session.SelectedIndex)
        {
            Commit(shift: false);
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
            action();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            _logger.Write(GlazeLogLevel.Error, "AltTab", "The application switcher failed.", exception);
        }
    }
}
