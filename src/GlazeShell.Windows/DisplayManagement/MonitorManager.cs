using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Core.Services;
using GlazeShell.Windows.SystemEvents;

namespace GlazeShell.Windows.DisplayManagement;

public sealed class MonitorManager : IMonitorManager, IWindowsEventSource
{
    /// <summary>
    /// Система присылает изменение дисплеев пачкой сообщений: при подключении монитора
    /// приходят <c>WM_DEVICECHANGE</c>, <c>WM_DISPLAYCHANGE</c> и иногда <c>WM_SETTINGCHANGE</c>.
    /// Публикация выполняется один раз после паузы, чтобы не перечислять мониторы на каждое сообщение.
    /// </summary>
    private static readonly TimeSpan CoalescingDelay = TimeSpan.FromMilliseconds(300);

    private readonly IEventManager _events;
    private readonly EventCoalescer _coalescer;
    private MonitorEventMonitor? _monitor;
    private bool _disposed;

    public MonitorManager(IEventManager events, Action<Exception>? errorHandler = null)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _coalescer = new EventCoalescer(
            CoalescingDelay,
            PublishDisplayChanged,
            errorHandler ?? EventCoalescer.FallbackErrorHandler("MonitorManager"));
    }

    public bool Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_monitor is not null)
        {
            return true;
        }

        var monitor = new MonitorEventMonitor(_coalescer.Request);

        if (!monitor.Start())
        {
            monitor.Dispose();
            return false;
        }

        _monitor = monitor;
        return true;
    }

    public IReadOnlyList<MonitorInfo> GetMonitors() => NativeMonitorEnumerator.Enumerate();

    public MonitorInfo? GetMonitor(string monitorId)
    {
        if (!NativeMonitorEnumerator.TryParseId(monitorId, out var hmonitor))
        {
            return null;
        }

        return NativeMonitorEnumerator.Read(hmonitor);
    }

    public MonitorInfo? GetPrimaryMonitor() => NativeMonitorEnumerator.GetPrimaryMonitor();

    public MonitorInfo? GetMonitorForWindow(string windowId) => NativeMonitorEnumerator.GetMonitorForWindow(windowId);

    public MonitorInfo? GetMonitorForPoint(int x, int y) => NativeMonitorEnumerator.GetMonitorForPoint(x, y);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Отложенная публикация отменяется, а не выполняется: после остановки источника
        // подписчики не должны получать DisplayChanged.
        _coalescer.Cancel();
        _coalescer.Dispose();
        _monitor?.Dispose();
        _monitor = null;
    }

    private void PublishDisplayChanged() =>
        _events.Publish(new DisplayChanged(NativeMonitorEnumerator.Enumerate()));

    /// <summary>
    /// Имитирует сигнал об изменении конфигурации дисплеев: ставит в очередь
    /// коалесцированную публикацию так же, как это делает оконная процедура.
    /// </summary>
    /// <remarks>
    /// Существует для тестов. Рассылка сообщений по всей системе (<c>HWND_BROADCAST</c>)
    /// влияет на другие процессы и на параллельные тесты окон, поэтому коалесцирование
    /// проверяется через ту же точку входа, не затрагивая ничего, кроме этого менеджера.
    /// </remarks>
    internal void RequestDisplayRefresh() => _coalescer.Request();
}
