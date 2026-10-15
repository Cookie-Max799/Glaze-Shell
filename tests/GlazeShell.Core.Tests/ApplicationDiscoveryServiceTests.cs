using GlazeShell.Core.Discovery;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Core.Services;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class ApplicationDiscoveryServiceTests
{
    private static readonly string[] SortedNames = ["Alpha", "Zed"];

    [TestMethod]
    public async Task DiscoversApplicationsSortedByName()
    {
        var source = new FakeSource("start-menu", Result(
            Candidate("Zed", @"C:\Apps\zed.exe"),
            Candidate("Alpha", @"C:\Apps\alpha.exe")));
        using var service = CreateService(source);

        var applications = await service.DiscoverAsync();

        CollectionAssert.AreEqual(
            SortedNames,
            applications.Select(static application => application.Name).ToArray());
    }

    [TestMethod]
    public async Task CachesSnapshotWithinCacheDuration()
    {
        var source = new FakeSource("start-menu", Result(Candidate("Alpha", @"C:\Apps\alpha.exe")));
        using var service = CreateService(source);

        await service.DiscoverAsync();
        await service.DiscoverAsync();

        Assert.AreEqual(1, source.Calls);
    }

    [TestMethod]
    public async Task ExpiredCacheTriggersRescan()
    {
        var source = new FakeSource("start-menu", Result(Candidate("Alpha", @"C:\Apps\alpha.exe")));
        using var service = CreateService(source, new ApplicationDiscoveryOptions { CacheDuration = TimeSpan.Zero });

        await service.DiscoverAsync();
        await service.DiscoverAsync();

        Assert.AreEqual(2, source.Calls);
    }

    [TestMethod]
    public async Task RefreshAlwaysRescans()
    {
        var source = new FakeSource("start-menu", Result(Candidate("Alpha", @"C:\Apps\alpha.exe")));
        using var service = CreateService(source);

        await service.DiscoverAsync();
        await service.RefreshAsync();

        Assert.AreEqual(2, source.Calls);
    }

    [TestMethod]
    public async Task GetReturnsApplicationByIdCaseInsensitively()
    {
        var source = new FakeSource("apps-folder", Result(Candidate("Contoso", aumid: "Contoso.App_123!App")));
        using var service = CreateService(source);

        await service.DiscoverAsync();

        var found = await service.GetAsync("MSIX:CONTOSO.APP_123!APP");

        Assert.IsNotNull(found);
        Assert.AreEqual("Contoso", found.Name);
        Assert.IsNull(await service.GetAsync("msix:missing"));
    }

    [TestMethod]
    public void GetRejectsBlankIdentifier()
    {
        using var service = CreateService(new FakeSource("start-menu", ApplicationDiscoveryResult.Empty));

        Assert.ThrowsExactly<ArgumentException>(() => service.GetAsync("   "));
    }

    [TestMethod]
    public async Task SkipsCandidatesWithoutLaunchIdentity()
    {
        var source = new FakeSource("start-menu", Result(
            new ApplicationCandidate("No Target", "start-menu"),
            Candidate("Alpha", @"C:\Apps\alpha.exe")));
        using var service = CreateService(source);

        var applications = await service.DiscoverAsync();

        Assert.HasCount(1, applications);
        Assert.AreEqual("Alpha", applications[0].Name);
    }

    [TestMethod]
    public async Task ExcludesSystemApplicationsWhenDisabled()
    {
        var source = new FakeSource("start-menu", Result(
            Candidate("System", @"C:\Apps\system.exe", isSystemApplication: true),
            Candidate("User", @"C:\Apps\user.exe")));
        using var service = CreateService(source, new ApplicationDiscoveryOptions { IncludeSystemApplications = false });

        var applications = await service.DiscoverAsync();

        Assert.HasCount(1, applications);
        Assert.AreEqual("User", applications[0].Name);
    }

    [TestMethod]
    public async Task IncludesSystemApplicationsByDefault()
    {
        var source = new FakeSource("start-menu", Result(Candidate("System", @"C:\Apps\system.exe", isSystemApplication: true)));
        using var service = CreateService(source);

        var applications = await service.DiscoverAsync();

        Assert.HasCount(1, applications);
        Assert.IsTrue(applications[0].IsSystemApplication);
    }

    [TestMethod]
    public async Task MergesCandidatesWithTheSameLaunchKey()
    {
        var source = new FakeSource("start-menu", Result(
            Candidate("Contoso", @"C:\Apps\contoso.exe"),
            Candidate("Contoso", @"c:\apps\CONTOSO.exe", description: "From secondary")));
        using var service = CreateService(source);

        var applications = await service.DiscoverAsync();

        Assert.HasCount(1, applications);
        Assert.AreEqual("From secondary", applications[0].Description);
    }

    [TestMethod]
    public async Task MergedSystemFlagIsTheIntersection()
    {
        var source = new FakeSource("start-menu", Result(
            Candidate("Contoso", @"C:\Apps\contoso.exe", isSystemApplication: true),
            Candidate("Contoso", @"C:\Apps\contoso.exe")));
        using var service = CreateService(source);

        var applications = await service.DiscoverAsync();

        Assert.HasCount(1, applications);
        Assert.IsFalse(applications[0].IsSystemApplication);
    }

    [TestMethod]
    public async Task PublishesApplicationChangedOnlyWhenSnapshotChanges()
    {
        var events = new EventManager();
        var published = 0;
        using var subscription = events.Subscribe<ApplicationChanged>(_ => published++);
        var source = new FakeSource("start-menu", Result(Candidate("Alpha", @"C:\Apps\alpha.exe")));
        using var service = CreateService(source, eventManager: events);

        await service.DiscoverAsync();
        await service.RefreshAsync();

        Assert.AreEqual(1, published);
    }

    [TestMethod]
    public async Task RaisesDiscoveryCompletedWhenWarningsArePresent()
    {
        var source = new FakeSource("start-menu", new ApplicationDiscoveryResult(
            [Candidate("Alpha", @"C:\Apps\alpha.exe")],
            ["The shortcut has no target."]));
        using var service = CreateService(source);

        DiscoveryReport? report = null;
        service.DiscoveryCompleted += (_, value) => report = value;

        await service.DiscoverAsync();

        Assert.IsNotNull(report);
        Assert.AreEqual(1, report.ApplicationCount);
        Assert.AreEqual(0, report.SkippedCount);
        StringAssert.Contains(report.Warnings.Single(), "The shortcut has no target.");
        StringAssert.Contains(report.Warnings.Single(), "start-menu");
    }

    [TestMethod]
    public async Task DoesNotRaiseDiscoveryCompletedWithoutWarnings()
    {
        var source = new FakeSource("start-menu", Result(Candidate("Alpha", @"C:\Apps\alpha.exe")));
        using var service = CreateService(source);

        var raised = false;
        service.DiscoveryCompleted += (_, _) => raised = true;

        await service.DiscoverAsync();

        Assert.IsFalse(raised);
    }

    [TestMethod]
    public async Task SourceFailureIsIsolatedAsWarning()
    {
        var failing = new ThrowingSource("broken");
        var working = new FakeSource("start-menu", Result(Candidate("Alpha", @"C:\Apps\alpha.exe")));
        using var service = CreateService(failing, working);

        DiscoveryReport? report = null;
        service.DiscoveryCompleted += (_, value) => report = value;

        var applications = await service.DiscoverAsync();

        Assert.HasCount(1, applications);
        Assert.IsNotNull(report);
        Assert.IsTrue(report.Warnings.Any(static warning => warning.Contains("broken", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task LimitsWarningsPerSource()
    {
        var source = new FakeSource("start-menu", new ApplicationDiscoveryResult(
            [Candidate("Alpha", @"C:\Apps\alpha.exe")],
            ["one", "two", "three", "four"]));
        using var service = CreateService(source, new ApplicationDiscoveryOptions { MaxWarningsPerSource = 2 });

        DiscoveryReport? report = null;
        service.DiscoveryCompleted += (_, value) => report = value;

        await service.DiscoverAsync();

        Assert.IsNotNull(report);
        Assert.HasCount(2, report.Warnings);
    }

    [TestMethod]
    public async Task DisposeRejectsFurtherUse()
    {
        var service = CreateService(new FakeSource("start-menu", ApplicationDiscoveryResult.Empty));
        service.Dispose();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => service.DiscoverAsync());
        Assert.ThrowsExactly<ObjectDisposedException>(() => service.RefreshAsync());
        Assert.ThrowsExactly<ObjectDisposedException>(() => service.GetAsync("app:1"));
    }

    [TestMethod]
    public void RejectsNullSources()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ApplicationDiscoveryService(null!, new EventManager()));
    }

    [TestMethod]
    public void RejectsNullEventManager()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ApplicationDiscoveryService([], null!));
    }

    [TestMethod]
    public void ValidatesOptionsOnConstruction()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ApplicationDiscoveryService(
            [],
            new EventManager(),
            new ApplicationDiscoveryOptions { MaxWarningsPerSource = 0 }));
    }

    private static ApplicationDiscoveryService CreateService(
        params IApplicationDiscoverySource[] sources) =>
        CreateService(sources, eventManager: new EventManager(), options: null);

    private static ApplicationDiscoveryService CreateService(
        IApplicationDiscoverySource source,
        ApplicationDiscoveryOptions options) =>
        CreateService([source], new EventManager(), options);

    private static ApplicationDiscoveryService CreateService(
        IApplicationDiscoverySource source,
        EventManager? eventManager = null) =>
        CreateService([source], eventManager ?? new EventManager(), options: null);

    private static ApplicationDiscoveryService CreateService(
        IEnumerable<IApplicationDiscoverySource> sources,
        EventManager eventManager,
        ApplicationDiscoveryOptions? options) =>
        new(sources, eventManager, options);

    private static ApplicationDiscoveryResult Result(params ApplicationCandidate[] candidates) => new(candidates);

    private static ApplicationCandidate Candidate(
        string name,
        string? executablePath = null,
        string? aumid = null,
        string? description = null,
        bool isSystemApplication = false) =>
        new(name, "start-menu", ApplicationType.Win32, executablePath, applicationUserModelId: aumid, description: description, isSystemApplication: isSystemApplication);

    private sealed class FakeSource(string name, ApplicationDiscoveryResult result) : IApplicationDiscoverySource
    {
        public int Calls { get; private set; }

        public string Name => name;

        public ApplicationDiscoveryResult Discover(CancellationToken cancellationToken = default)
        {
            Calls++;
            return result;
        }
    }

    private sealed class ThrowingSource(string name) : IApplicationDiscoverySource
    {
        public string Name => name;

        public ApplicationDiscoveryResult Discover(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
    }
}
