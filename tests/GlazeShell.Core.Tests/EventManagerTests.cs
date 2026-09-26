using GlazeShell.Core.Events;
using GlazeShell.Core.Models;
using GlazeShell.Core.Services;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class EventManagerTests
{
    [TestMethod]
    public void PublishedEventReachesSubscriber()
    {
        var eventManager = new EventManager();
        var received = new List<WindowOpened>();
        using var subscription = eventManager.Subscribe<WindowOpened>(received.Add);
        var window = new WindowInfo("window-1", "Glaze Shell", 1234);

        eventManager.Publish(new WindowOpened(window));

        Assert.HasCount(1, received);
        Assert.AreSame(window, received[0].Window);
    }

    [TestMethod]
    public void DisposedSubscriptionStopsDelivery()
    {
        var eventManager = new EventManager();
        var received = new List<SettingsChanged>();
        var subscription = eventManager.Subscribe<SettingsChanged>(received.Add);

        subscription.Dispose();
        eventManager.Publish(new SettingsChanged(UserSettings.CreateDefault()));

        Assert.IsEmpty(received);
    }

    [TestMethod]
    public void SubscriberFailureIsRoutedToExceptionHandler()
    {
        var failures = new List<Exception>();
        var eventManager = new EventManager(failures.Add);
        using var subscription = eventManager.Subscribe<SettingsChanged>(
            _ => throw new InvalidOperationException("subscriber failure"));

        eventManager.Publish(new SettingsChanged(UserSettings.CreateDefault()));

        Assert.HasCount(1, failures);
        Assert.AreEqual("subscriber failure", failures[0].Message);
    }
}
