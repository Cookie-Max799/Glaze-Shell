using GlazeShell.Core.Configuration;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Core.Persistence;
using GlazeShell.Core.Services;
using GlazeShell.Data.Persistence;

namespace GlazeShell.Data.Tests;

[TestClass]
public sealed class PersistingSettingsManagerTests
{
    [TestMethod]
    public void LoadReturnsSavedSettings()
    {
        using var data = new TempUserData();
        var store = data.CreateStore();
        store.SaveSettings(TempUserData.CreateSettings());

        var manager = new PersistingSettingsManager(data.CreateStore());

        var result = manager.Load();

        Assert.AreEqual(PersistenceStatus.Loaded, result.Status);
        Assert.AreEqual("en-US", manager.GetSettings().Language);
    }

    [TestMethod]
    public void SetSettingsWritesTheDocument()
    {
        using var data = new TempUserData();
        var manager = new PersistingSettingsManager(data.CreateStore());
        _ = manager.Load();

        manager.SetSettings(TempUserData.CreateSettings(language: "de-DE", themeId: null));

        var reloaded = data.CreateStore().LoadSettings();

        Assert.AreEqual("de-DE", reloaded.Value.Language);
        Assert.IsNull(reloaded.Value.ActiveThemeId);
    }

    [TestMethod]
    public void SaveFailureIsReportedAndSettingsStillApplied()
    {
        using var data = new TempUserData();
        var reported = new List<string>();
        var manager = new PersistingSettingsManager(
            new FailingStore(),
            errorReporter: (message, _) => reported.Add(message));
        _ = manager.Load();

        manager.SetSettings(TempUserData.CreateSettings());

        Assert.AreEqual("en-US", manager.GetSettings().Language);
        Assert.IsTrue(reported.Any(static message => message.Contains("could not be saved", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void SetSettingsPublishesSettingsChanged()
    {
        using var data = new TempUserData();
        var events = new EventManager();
        var published = 0;
        using var subscription = events.Subscribe<SettingsChanged>(_ => published++);
        var manager = new PersistingSettingsManager(data.CreateStore(), events);
        _ = manager.Load();

        var changed = TempUserData.CreateSettings(language: "de-DE");
        manager.SetSettings(changed);
        manager.SetSettings(changed);

        Assert.AreEqual(1, published);
    }

    [TestMethod]
    public void NullSettingsAreRejected()
    {
        using var data = new TempUserData();
        var manager = new PersistingSettingsManager(data.CreateStore());

        Assert.ThrowsExactly<ArgumentNullException>(() => manager.SetSettings(null!));
    }

    private sealed class FailingStore : IUserDataStore
    {
        public string RootDirectory => "test";

        public PersistenceLoadResult<UserSettings> LoadSettings() =>
            new(UserSettings.CreateDefault(), PersistenceStatus.Created);

        public void SaveSettings(UserSettings settings) =>
            throw new PersistenceException("The settings cannot be written.");

        public PersistenceLoadResult<GlazeShellConfiguration> LoadConfiguration() =>
            throw new NotSupportedException();

        public void SaveConfiguration(GlazeShellConfiguration configuration) =>
            throw new NotSupportedException();

        public PersistenceLoadResult<DesktopLayout> LoadLayout() =>
            throw new NotSupportedException();

        public void SaveLayout(DesktopLayout layout) =>
            throw new NotSupportedException();

        public void Flush()
        {
        }
    }
}

[TestClass]
public sealed class DesktopLayoutPersistenceWriterTests
{
    [TestMethod]
    public void FlushWritesTheLatestLayoutOnly()
    {
        using var data = new TempUserData();
        var store = new CountingStore(data.CreateStore());
        var events = new EventManager();

        using var writer = new DesktopLayoutPersistenceWriter(store, events, errorReporter: null, delay: TimeSpan.FromMinutes(5));

        events.Publish(new DesktopChanged(CreateLayout("tab-1")));
        events.Publish(new DesktopChanged(CreateLayout("tab-2")));
        events.Publish(new DesktopChanged(CreateLayout("tab-3")));

        Assert.AreEqual(0, store.SaveCount);

        writer.Flush();

        Assert.AreEqual(1, store.SaveCount);
        Assert.AreEqual("tab-3", store.LastLayout!.ActiveTabId);
    }

    [TestMethod]
    public void ChangesAfterFlushAreNotWrittenTwice()
    {
        using var data = new TempUserData();
        var store = new CountingStore(data.CreateStore());
        var events = new EventManager();

        using var writer = new DesktopLayoutPersistenceWriter(store, events, errorReporter: null, delay: TimeSpan.FromMinutes(5));

        events.Publish(new DesktopChanged(CreateLayout("tab-1")));
        writer.Flush();
        writer.Flush();

        Assert.AreEqual(1, store.SaveCount);
    }

    [TestMethod]
    public void DisposeFlushesAndUnsubscribes()
    {
        using var data = new TempUserData();
        var store = new CountingStore(data.CreateStore());
        var events = new EventManager();

        var writer = new DesktopLayoutPersistenceWriter(store, events, errorReporter: null, delay: TimeSpan.FromMinutes(5));
        events.Publish(new DesktopChanged(CreateLayout("tab-1")));

        writer.Dispose();

        Assert.AreEqual(1, store.SaveCount);

        events.Publish(new DesktopChanged(CreateLayout("tab-2")));
        writer.Dispose();

        Assert.AreEqual(1, store.SaveCount);
    }

    [TestMethod]
    public void SaveFailureIsReported()
    {
        var reported = new List<string>();
        var events = new EventManager();

        using var writer = new DesktopLayoutPersistenceWriter(
            new FailingLayoutStore(),
            events,
            (message, _) => reported.Add(message),
            TimeSpan.FromMinutes(5));

        events.Publish(new DesktopChanged(CreateLayout("tab-1")));
        writer.Flush();

        Assert.HasCount(1, reported);
    }

    [TestMethod]
    public void NegativeDelayIsRejected()
    {
        using var data = new TempUserData();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new DesktopLayoutPersistenceWriter(data.CreateStore(), new EventManager(), null, TimeSpan.FromSeconds(-1)));
    }

    private static DesktopLayout CreateLayout(string tabId) => new([new DesktopTab(tabId, "Home")], tabId);

    private sealed class CountingStore(IUserDataStore inner) : IUserDataStore
    {
        public int SaveCount { get; private set; }

        public DesktopLayout? LastLayout { get; private set; }

        public string RootDirectory => inner.RootDirectory;

        public PersistenceLoadResult<UserSettings> LoadSettings() => inner.LoadSettings();

        public void SaveSettings(UserSettings settings) => inner.SaveSettings(settings);

        public PersistenceLoadResult<GlazeShellConfiguration> LoadConfiguration() => inner.LoadConfiguration();

        public void SaveConfiguration(GlazeShellConfiguration configuration) => inner.SaveConfiguration(configuration);

        public PersistenceLoadResult<DesktopLayout> LoadLayout() => inner.LoadLayout();

        public void SaveLayout(DesktopLayout layout)
        {
            SaveCount++;
            LastLayout = layout;
        }

        public void Flush()
        {
        }
    }

    private sealed class FailingLayoutStore : IUserDataStore
    {
        public string RootDirectory => "test";

        public PersistenceLoadResult<UserSettings> LoadSettings() =>
            throw new NotSupportedException();

        public void SaveSettings(UserSettings settings) =>
            throw new NotSupportedException();

        public PersistenceLoadResult<GlazeShellConfiguration> LoadConfiguration() =>
            throw new NotSupportedException();

        public void SaveConfiguration(GlazeShellConfiguration configuration) =>
            throw new NotSupportedException();

        public PersistenceLoadResult<DesktopLayout> LoadLayout() =>
            throw new NotSupportedException();

        public void SaveLayout(DesktopLayout layout) =>
            throw new PersistenceException("The layout cannot be written.");

        public void Flush()
        {
        }
    }
}
