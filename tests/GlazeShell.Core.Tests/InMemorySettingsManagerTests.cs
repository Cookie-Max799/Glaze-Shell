using GlazeShell.Core.Events;
using GlazeShell.Core.Models;
using GlazeShell.Core.Services;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class InMemorySettingsManagerTests
{
    [TestMethod]
    public void SetSettingsStoresValueAndPublishesChange()
    {
        var eventManager = new EventManager();
        var received = new List<SettingsChanged>();
        using var subscription = eventManager.Subscribe<SettingsChanged>(received.Add);
        var settingsManager = new InMemorySettingsManager(eventManager);
        var settings = new UserSettings(language: "ru-RU");

        settingsManager.SetSettings(settings);

        Assert.AreSame(settings, settingsManager.GetSettings());
        Assert.HasCount(1, received);
        Assert.AreSame(settings, received[0].Settings);
    }

    [TestMethod]
    public void SettingEquivalentValueDoesNotPublishDuplicateChange()
    {
        var eventManager = new EventManager();
        var received = new List<SettingsChanged>();
        using var subscription = eventManager.Subscribe<SettingsChanged>(received.Add);
        var settingsManager = new InMemorySettingsManager(eventManager);

        settingsManager.SetSettings(new UserSettings(language: "ru-RU"));
        settingsManager.SetSettings(new UserSettings(language: "ru-RU"));

        Assert.HasCount(1, received);
    }
}
