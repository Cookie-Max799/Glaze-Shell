using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using GlazeShell.Core.Events;
using GlazeShell.Core.Services;
using GlazeShell.Windows.DisplayManagement;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Tests.DisplayManagement;

[TestClass]
public sealed class MonitorManagerTests
{
    private const int EventTimeoutMilliseconds = 6000;

    private readonly ConcurrentQueue<Exception> _errors = new();

    private EventManager CreateEvents() => new(exception => _errors.Enqueue(exception));

    [TestCleanup]
    public void Cleanup()
    {
        if (_errors.TryDequeue(out var failure))
        {
            Assert.Fail($"An event dispatch failed: {failure.Message}");
        }
    }

    [TestMethod]
    public void EnumeratesAtLeastOneMonitor()
    {
        using var manager = new MonitorManager(CreateEvents());

        var monitors = manager.GetMonitors();

        Assert.IsGreaterThanOrEqualTo(1, monitors.Count, "A desktop environment must expose at least one monitor.");
    }

    [TestMethod]
    public void PrimaryMonitorIsInEnumeration()
    {
        using var manager = new MonitorManager(CreateEvents());

        var monitors = manager.GetMonitors();
        var primary = manager.GetPrimaryMonitor();

        Assert.IsNotNull(primary, "A desktop environment must expose a primary monitor.");
        Assert.IsTrue(monitors.Any(monitor => monitor.IsPrimary), "The primary monitor must be included in the enumeration.");
        Assert.IsNotNull(monitors.FirstOrDefault(monitor => monitor.Id == primary!.Id));
    }

    [TestMethod]
    public void EnumeratedMonitorsHaveValidGeometry()
    {
        using var manager = new MonitorManager(CreateEvents());

        foreach (var monitor in manager.GetMonitors())
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(monitor.Id));
            Assert.IsFalse(string.IsNullOrWhiteSpace(monitor.DeviceName));
            Assert.IsGreaterThan(0, monitor.Bounds.Width);
            Assert.IsGreaterThan(0, monitor.Bounds.Height);
            Assert.IsGreaterThan(0.0, monitor.ScaleFactor);
        }
    }

    [TestMethod]
    public async Task DisplayChangeBroadcastRaisesDisplayChanged()
    {
        var events = CreateEvents();
        using var manager = new MonitorManager(events);

        if (!manager.Start())
        {
            Assert.Inconclusive("The monitor event window could not be created in this environment.");
            return;
        }

        var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = events.Subscribe<DisplayChanged>(changed => received.TrySetResult(changed.Monitors.Count));
        await Task.Delay(200);

        Broadcast(User32.WmDisplayChange, 0);

        var done = await Task.WhenAny(received.Task, Task.Delay(EventTimeoutMilliseconds));
        Assert.AreEqual(received.Task, done, "The DisplayChanged event was not raised after WM_DISPLAYCHANGE broadcast.");
        Assert.IsTrue(received.Task.IsCompletedSuccessfully && received.Task.Result >= 1);
    }

    /// <summary>
    /// Рассылает сообщение всем верхнеуровневым окнам.
    /// </summary>
    /// <remarks>
    /// Используется только <c>WM_DISPLAYCHANGE</c>: он не меняет системные настройки
    /// и безопасен для других процессов. <c>WM_SETTINGCHANGE</c> и <c>WM_DEVICECHANGE</c>
    /// рассылать нельзя — они влияют на поведение чужих окон и делают параллельные тесты
    /// недетерминированными; отбор этих сигналов проверяется в <see cref="DisplaySignalTests"/>.
    /// </remarks>
    private static void Broadcast(uint message, nint wParam)
    {
        _ = SendMessageTimeout(
            (nint)0xFFFF, // HWND_BROADCAST
            message,
            wParam,
            0,
            2, // SMTO_ABORTIFHUNG
            2000,
            out _);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessageTimeout(
        nint window,
        uint message,
        nint wParam,
        nint lParam,
        uint flags,
        uint timeout,
        out nint result);

    [TestMethod]
    public async Task DisplaySignalIsCoalescedIntoASingleEvent()
    {
        var events = CreateEvents();
        using var manager = new MonitorManager(events);

        var raised = 0;
        using var subscription = events.Subscribe<DisplayChanged>(_ => Interlocked.Increment(ref raised));

        // Windows шлёт пачку сообщений при смене конфигурации: без коалесцирования
        // каждое привело бы к отдельному перечислению мониторов.
        for (var index = 0; index < 20; index++)
        {
            manager.RequestDisplayRefresh();
        }

        Assert.IsTrue(WaitFor(() => Volatile.Read(ref raised) > 0, EventTimeoutMilliseconds), "The coalesced display event was never published.");

        // Даём запас времени: без коалесцирования здесь было бы 20 публикаций.
        await Task.Delay(1500);

        Assert.AreEqual(1, Volatile.Read(ref raised), "The display signals were not coalesced into a single event.");
    }

    [TestMethod]
    public async Task SequentialDisplaySignalsAreEachPublished()
    {
        var events = CreateEvents();
        using var manager = new MonitorManager(events);

        var raised = 0;
        using var subscription = events.Subscribe<DisplayChanged>(_ => Interlocked.Increment(ref raised));

        // Сигналы, разнесённые во времени, коалесцировать нельзя: каждый из них означает
        // отдельное изменение, и подписчик должен увидеть их все.
        for (var index = 0; index < 2; index++)
        {
            manager.RequestDisplayRefresh();
            Assert.IsTrue(WaitFor(() => Volatile.Read(ref raised) == index + 1, EventTimeoutMilliseconds));
            await Task.Delay(400);
        }
    }

    [TestMethod]
    public void DisposeCancelsPendingDisplayPublication()
    {
        var events = CreateEvents();
        var manager = new MonitorManager(events);

        var raised = 0;
        using var subscription = events.Subscribe<DisplayChanged>(_ => Interlocked.Increment(ref raised));

        // Источник остановлен до истечения задержки коалесцирования: подписчик не должен
        // получить событие от уже остановленного источника.
        manager.RequestDisplayRefresh();
        manager.Dispose();
        Thread.Sleep(1500);

        Assert.AreEqual(0, Volatile.Read(ref raised), "A stopped source must not publish display events.");
    }

    [TestMethod]
    public void DisposeIsIdempotent()
    {
        var manager = new MonitorManager(CreateEvents());

        manager.Dispose();
        manager.Dispose();
    }

    [TestMethod]
    public void StartAfterDisposeThrows()
    {
        var manager = new MonitorManager(CreateEvents());
        manager.Dispose();

        Assert.Throws<ObjectDisposedException>(() => manager.Start());
    }

    private static bool WaitFor(Func<bool> condition, int timeoutMilliseconds)
    {
        var deadline = Environment.TickCount64 + timeoutMilliseconds;

        while (Environment.TickCount64 < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(20);
        }

        return condition();
    }
}
