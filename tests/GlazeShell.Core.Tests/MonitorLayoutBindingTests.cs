using GlazeShell.Core.Events;
using GlazeShell.Core.Models;
using GlazeShell.Core.Services;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class MonitorLayoutBindingTests
{
    [TestMethod]
    public void AssignTabToMonitorStoresTheBinding()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Work");

        Assert.IsTrue(manager.AssignTabToMonitor(tab.Id, "00010001"));

        Assert.AreEqual("00010001", manager.GetLayout().Tabs[0].MonitorId);
    }

    [TestMethod]
    public void AssignTabToMonitorCanClearTheBinding()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Work");
        _ = manager.AssignTabToMonitor(tab.Id, "00010001");

        Assert.IsTrue(manager.AssignTabToMonitor(tab.Id, null));

        Assert.IsNull(manager.GetLayout().Tabs[0].MonitorId);
    }

    [TestMethod]
    public void AssigningTheSameBindingIsNotAChange()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Work");
        _ = manager.AssignTabToMonitor(tab.Id, "00010001");

        Assert.IsFalse(manager.AssignTabToMonitor(tab.Id, "00010001"));
        Assert.IsFalse(manager.AssignTabToMonitor(manager.CreateTab("Home").Id, null));
    }

    [TestMethod]
    public void GetTabsForMonitorReturnsBoundTabsInCanonicalOrder()
    {
        var manager = CreateManager(out _);
        var first = manager.CreateTab("First");
        var second = manager.CreateTab("Second");
        var third = manager.CreateTab("Third");
        _ = manager.AssignTabToMonitor(third.Id, "00020002");
        _ = manager.AssignTabToMonitor(first.Id, "00010001");
        _ = manager.AssignTabToMonitor(second.Id, "00010001");

        var tabs = manager.GetTabsForMonitor("00010001");

        Assert.HasCount(2, tabs);
        Assert.AreEqual(first.Id, tabs[0].Id);
        Assert.AreEqual(second.Id, tabs[1].Id);
        Assert.IsEmpty(manager.GetTabsForMonitor("unassigned"));
    }

    [TestMethod]
    public void MonitorAssignmentPublishesDesktopChanged()
    {
        var manager = CreateManager(out var events);
        var tab = manager.CreateTab("Work");
        var published = 0;
        using var subscription = events.Subscribe<DesktopChanged>(_ => published++);

        _ = manager.AssignTabToMonitor(tab.Id, "00010001");

        Assert.AreEqual(1, published);
    }

    [TestMethod]
    public void ReconcileKeepsBindingsToExistingMonitors()
    {
        var layout = new DesktopLayout([new DesktopTab("tab-1", "Work", monitorId: "00010001")], "tab-1");

        var reconciliation = MonitorLayoutBinding.Reconcile(layout, [CreateMonitor("00010001")]);

        Assert.IsFalse(reconciliation.HasChanges);
        Assert.AreSame(layout, reconciliation.Layout);
    }

    [TestMethod]
    public void ReconcileClearsBindingsToMissingMonitors()
    {
        var layout = new DesktopLayout(
            [
                new DesktopTab("tab-1", "Work", monitorId: "00010001"),
                new DesktopTab("tab-2", "Home", monitorId: "00020002")
            ],
            "tab-1");

        var reconciliation = MonitorLayoutBinding.Reconcile(layout, [CreateMonitor("00010001")]);

        Assert.IsTrue(reconciliation.HasChanges);
        CollectionAssert.AreEqual(new List<string> { "tab-2" }, reconciliation.ClearedTabIds.ToList());
        Assert.AreEqual("00010001", reconciliation.Layout.Tabs[0].MonitorId);
        Assert.IsNull(reconciliation.Layout.Tabs[1].MonitorId);
        Assert.HasCount(2, reconciliation.Layout.Tabs);
        Assert.AreEqual("tab-1", reconciliation.Layout.ActiveTabId);
    }

    [TestMethod]
    public void ReconcileWithoutMonitorsKeepsLayoutUnchanged()
    {
        var layout = new DesktopLayout([new DesktopTab("tab-1", "Work", monitorId: "00010001")], "tab-1");

        Assert.IsFalse(MonitorLayoutBinding.Reconcile(layout, null).HasChanges);
        Assert.IsFalse(MonitorLayoutBinding.Reconcile(layout, []).HasChanges);
    }

    [TestMethod]
    public void ReconcileRequiresLayout()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => MonitorLayoutBinding.Reconcile(null!, []));
    }

    [TestMethod]
    public void ReconcilePreservesTabOrder()
    {
        var layout = new DesktopLayout(
            [
                new DesktopTab("tab-2", "Second", order: 1, monitorId: "00020002"),
                new DesktopTab("tab-1", "First", order: 0)
            ],
            "tab-1");

        var reconciliation = MonitorLayoutBinding.Reconcile(layout, [CreateMonitor("00010001")]);

        Assert.IsTrue(reconciliation.HasChanges);
        Assert.AreEqual("tab-2", reconciliation.Layout.Tabs[0].Id);
        Assert.AreEqual(1, reconciliation.Layout.Tabs[0].Order);
        Assert.IsNull(reconciliation.Layout.Tabs[0].MonitorId);
        Assert.AreEqual("tab-1", reconciliation.Layout.Tabs[1].Id);
        Assert.AreEqual(0, reconciliation.Layout.Tabs[1].Order);
        Assert.AreEqual("tab-1", reconciliation.Layout.ActiveTabId);
        Assert.AreEqual(layout.LayoutVersion, reconciliation.Layout.LayoutVersion);
    }

    private static DesktopManager CreateManager(out EventManager events)
    {
        events = new EventManager();
        return new DesktopManager(events);
    }

    private static MonitorInfo CreateMonitor(string id) =>
        new(id, $@"\\.\DISPLAY{id}", new MonitorBounds(0, 0, 1920, 1080));
}
