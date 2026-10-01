using System;
using System.Collections.Generic;
using GlazeShell.Core.Interfaces;
using GlazeShell.Windows.SystemEvents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlazeShell.Windows.Tests.SystemEvents;

[TestClass]
public sealed class ShellEventCoordinatorTests : IDisposable
{
    private readonly List<Exception> _errors = new();

    private void OnError(Exception exception)
    {
        _errors.Add(exception);
    }

    [TestMethod]
    public void StartReturnsStatusWithAllStartedWhenSourcesOk()
    {
        var events = new DummyEventManager();
        var windows = new DummySource(true);
        var monitors = new DummySource(true);
        using var coordinator = new ShellEventCoordinator(events, windows, monitors, ProcessWatchOptions.Default, OnError);
        var status = coordinator.Start();
        Assert.IsTrue(status.AllSourcesStarted);
        Assert.IsTrue(status.WindowEventsStarted);
        Assert.IsTrue(status.DisplayEventsStarted);
        Assert.IsTrue(status.ProcessEventsStarted);
        Assert.IsEmpty(status.Diagnostics);
    }

    [TestMethod]
    public void StartAddsDiagnosticsWhenSourceFails()
    {
        var events = new DummyEventManager();
        var windows = new DummySource(false);
        var monitors = new DummySource(true);
        using var coordinator = new ShellEventCoordinator(events, windows, monitors, ProcessWatchOptions.Default, OnError);
        var status = coordinator.Start();
        Assert.IsFalse(status.AllSourcesStarted);
        Assert.IsFalse(status.WindowEventsStarted);
        Assert.IsTrue(status.DisplayEventsStarted);
        Assert.IsTrue(status.ProcessEventsStarted);
        Assert.IsNotEmpty(status.Diagnostics);
    }

    [TestMethod]
    public void StartThrowsWhenCalledTwice()
    {
        var events = new DummyEventManager();
        var windows = new DummySource(true);
        var monitors = new DummySource(true);
        using var coordinator = new ShellEventCoordinator(events, windows, monitors, ProcessWatchOptions.Default, OnError);
        coordinator.Start();
        Assert.Throws<InvalidOperationException>(() => coordinator.Start());
    }

    [TestMethod]
    public void DisposeIsIdempotent()
    {
        var events = new DummyEventManager();
        var windows = new DummySource(true);
        var monitors = new DummySource(true);
        var coordinator = new ShellEventCoordinator(events, windows, monitors, ProcessWatchOptions.Default, OnError);
        coordinator.Dispose();
        coordinator.Dispose();
    }

    public void Dispose()
    {
    }

    private sealed class DummyEventManager : IEventManager
    {
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : GlazeShell.Core.Events.GlazeEvent
        {
            return new DummySubscription();
        }

        public void Publish<TEvent>(TEvent e) where TEvent : GlazeShell.Core.Events.GlazeEvent
        {
        }

        public bool HasSubscribers<TEvent>() where TEvent : GlazeShell.Core.Events.GlazeEvent
        {
            return false;
        }

        private sealed class DummySubscription : IDisposable
        {
            public void Dispose() { }
        }
    }

    private sealed class DummySource : IWindowsEventSource
    {
        private readonly bool _result;

        public DummySource(bool result)
        {
            _result = result;
        }

        public bool Start() => _result;

        public void Dispose() { }
    }
}
