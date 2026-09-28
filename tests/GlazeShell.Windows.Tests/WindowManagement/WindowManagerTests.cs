using System.Collections.Concurrent;
using System.Diagnostics;
using GlazeShell.Core.Events;
using GlazeShell.Core.Services;
using GlazeShell.Windows.WindowManagement;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Tests.WindowManagement;

[TestClass]
public sealed class WindowManagerTests
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
    public void EnumeratesOwnTopLevelWindow()
    {
        using var manager = new WindowManager(CreateEvents());
        using var window = new Win32TestWindow("GlazeShell enumeration window");

        var windows = manager.GetWindows();
        var actual = windows.FirstOrDefault(w => w.Id == window.Id);

        Assert.IsNotNull(actual);
        Assert.AreEqual("GlazeShell enumeration window", actual.Title);
        Assert.AreEqual(Environment.ProcessId, actual.ProcessId);
        Assert.AreEqual(Core.Models.WindowState.Normal, actual.State);
    }

    [TestMethod]
    public void ReadsWindowByIdentifier()
    {
        using var manager = new WindowManager(CreateEvents());
        using var window = new Win32TestWindow("GlazeShell lookup window");

        var actual = manager.GetWindow(window.Id);

        Assert.IsNotNull(actual);
        Assert.AreEqual(window.Id, actual!.Id);
    }

    [TestMethod]
    public async Task MinimizeAndRestoreWindow()
    {
        using var manager = new WindowManager(CreateEvents());
        using var window = new Win32TestWindow("GlazeShell minimize window");

        Assert.IsTrue(manager.MinimizeWindow(window.Id));
        await WaitUntilAsync(() => User32.IsIconic(window.Handle));

        Assert.IsTrue(manager.RestoreWindow(window.Id));
        await WaitUntilAsync(() => !User32.IsIconic(window.Handle));

        Assert.IsFalse(User32.IsIconic(window.Handle));
    }

    [TestMethod]
    public async Task MaximizeAndRestoreWindow()
    {
        using var manager = new WindowManager(CreateEvents());
        using var window = new Win32TestWindow("GlazeShell maximize window");

        Assert.IsTrue(manager.MaximizeWindow(window.Id));
        await WaitUntilAsync(() => User32.IsZoomed(window.Handle));

        Assert.IsTrue(manager.RestoreWindow(window.Id));
        await WaitUntilAsync(() => !User32.IsZoomed(window.Handle));

        Assert.IsFalse(User32.IsZoomed(window.Handle));
    }

    [TestMethod]
    public void FocusBringsWindowToForegroundOrIsDeniedBySystem()
    {
        using var manager = new WindowManager(CreateEvents());
        using var window = new Win32TestWindow("GlazeShell focus window");

        var focused = manager.FocusWindow(window.Id);

        if (!focused && User32.GetForegroundWindow() != window.Handle)
        {
            Assert.Inconclusive("The operating system denied the foreground activation (foreground lock).");
            return;
        }

        Assert.IsTrue(focused);
        Assert.AreEqual(window.Handle, User32.GetForegroundWindow());
    }

    [TestMethod]
    public async Task CloseWindowDestroysIt()
    {
        using var manager = new WindowManager(CreateEvents());
        using var window = new Win32TestWindow("GlazeShell close window");

        Assert.IsTrue(manager.CloseWindow(window.Id));
        await WaitUntilAsync(() => !User32.IsWindow(window.Handle));

        Assert.IsFalse(User32.IsWindow(window.Handle));
    }

    [TestMethod]
    public async Task RaisesWindowOpenedAndClosedEvents()
    {
        const string title = "GlazeShell events window";

        var events = CreateEvents();
        using var manager = new WindowManager(events, skipOwnProcess: false);

        var opened = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var seen = new ConcurrentQueue<string>();
        using var openedSubscription = events.Subscribe<WindowOpened>(window =>
        {
            seen.Enqueue($"O:{window.Window.Title}");
            if (string.Equals(window.Window.Title, title, StringComparison.Ordinal))
            {
                opened.TrySetResult(window.Window.Id);
            }
        });
        using var closedSubscription = events.Subscribe<WindowClosed>(window =>
        {
            seen.Enqueue($"C:{window.Window.Title}");
            if (string.Equals(window.Window.Title, title, StringComparison.Ordinal))
            {
                closed.TrySetResult(window.Window.Id);
            }
        });

        if (!manager.Start())
        {
            Assert.Inconclusive("WinEvent hooks are unavailable in this environment.");
            return;
        }

        using (var window = new Win32TestWindow(title))
        {
            var done = await Task.WhenAny(opened.Task, Task.Delay(EventTimeoutMilliseconds));
            Assert.AreEqual(opened.Task, done, $"The WindowOpened event was not raised in time. Seen: {string.Join(" | ", seen)}");
        }

        var closedDone = await Task.WhenAny(closed.Task, Task.Delay(EventTimeoutMilliseconds));
        Assert.AreEqual(closed.Task, closedDone, $"The WindowClosed event was not raised in time. Seen: {string.Join(" | ", seen)}");
    }

    [TestMethod]
    public async Task RaisesStateChangedOnMinimize()
    {
        const string title = "GlazeShell state window";

        var events = CreateEvents();
        using var manager = new WindowManager(events, skipOwnProcess: false);

        var minimized = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = events.Subscribe<WindowStateChanged>(changed =>
        {
            if (string.Equals(changed.Window.Title, title, StringComparison.Ordinal))
            {
                minimized.TrySetResult(changed.Window.Id);
            }
        });

        if (!manager.Start())
        {
            Assert.Inconclusive("WinEvent hooks are unavailable in this environment.");
            return;
        }

        using var window = new Win32TestWindow(title);

        Assert.IsTrue(manager.MinimizeWindow(window.Id));

        var done = await Task.WhenAny(minimized.Task, Task.Delay(EventTimeoutMilliseconds));
        Assert.AreEqual(minimized.Task, done, "The minimized window state event was not raised in time.");
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMilliseconds = 3000)
    {
        var deadline = Stopwatch.StartNew();

        while (!condition() && deadline.ElapsedMilliseconds < timeoutMilliseconds)
        {
            await Task.Delay(15);
        }
    }
}