using GlazeShell.Core.Events;
using GlazeShell.Core.Models;
using GlazeShell.Core.Services;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class DesktopManagerTests
{
    private static DesktopManager CreateManager(out EventManager events)
    {
        events = new EventManager();
        return new DesktopManager(events);
    }

    [TestMethod]
    public void CreateTabMakesItActive()
    {
        var manager = CreateManager(out _);

        var tab = manager.CreateTab("Home");

        var layout = manager.GetLayout();
        Assert.IsNotNull(tab);
        Assert.HasCount(1, layout.Tabs);
        Assert.AreEqual(tab.Id, layout.ActiveTabId);
    }

    [TestMethod]
    public void CreateTabKeepsExistingActiveTab()
    {
        var manager = CreateManager(out _);
        var first = manager.CreateTab("First");

        var second = manager.CreateTab("Second");

        var layout = manager.GetLayout();
        Assert.AreEqual(first.Id, layout.ActiveTabId);
        Assert.AreNotEqual(first.Id, second.Id);
    }

    [TestMethod]
    public void RemoveActiveTabActivatesFirstRemaining()
    {
        var manager = CreateManager(out _);
        var first = manager.CreateTab("First");
        _ = manager.CreateTab("Second");

        Assert.IsTrue(manager.RemoveTab(first.Id));

        var layout = manager.GetLayout();
        Assert.HasCount(1, layout.Tabs);
        Assert.AreEqual(layout.Tabs[0].Id, layout.ActiveTabId);
    }

    [TestMethod]
    public void RemoveUnknownTabReturnsFalse()
    {
        var manager = CreateManager(out _);

        Assert.IsFalse(manager.RemoveTab("unknown"));
    }

    [TestMethod]
    public void ActivateTabSwitchesActiveTab()
    {
        var manager = CreateManager(out _);
        var first = manager.CreateTab("First");
        var second = manager.CreateTab("Second");

        Assert.IsTrue(manager.ActivateTab(second.Id));
        Assert.AreEqual(second.Id, manager.GetLayout().ActiveTabId);
        Assert.IsFalse(manager.ActivateTab("unknown"));
        Assert.IsFalse(manager.ActivateTab(second.Id));
        Assert.AreEqual(first.Id, manager.GetLayout().Tabs[0].Id);
    }

    [TestMethod]
    public void AddItemAppendsToMainCategory()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        var item = new DesktopItem("item-1", "app-1");

        Assert.IsTrue(manager.AddItem(tab.Id, item));

        var layout = manager.GetLayout();
        Assert.HasCount(1, layout.Tabs[0].Categories);
        Assert.HasCount(1, layout.Tabs[0].Categories[0].Items);
        Assert.AreEqual("item-1", layout.Tabs[0].Categories[0].Items[0].Id);
    }

    [TestMethod]
    public void AddDuplicateItemReturnsFalse()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        var item = new DesktopItem("item-1", "app-1");

        Assert.IsTrue(manager.AddItem(tab.Id, item));
        Assert.IsFalse(manager.AddItem(tab.Id, item));
    }

    [TestMethod]
    public void AddItemToUnknownTabReturnsFalse()
    {
        var manager = CreateManager(out _);

        Assert.IsFalse(manager.AddItem("unknown", new DesktopItem("item-1", "app-1")));
    }

    [TestMethod]
    public void RemoveItemRemovesFromCategory()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        _ = manager.AddItem(tab.Id, new DesktopItem("item-1", "app-1"));
        _ = manager.AddItem(tab.Id, new DesktopItem("item-2", "app-2"));

        Assert.IsTrue(manager.RemoveItem(tab.Id, "item-1"));
        Assert.IsFalse(manager.RemoveItem(tab.Id, "missing"));
        Assert.IsFalse(manager.RemoveItem("unknown", "item-1"));

        var layout = manager.GetLayout();
        Assert.HasCount(1, layout.Tabs[0].Categories[0].Items);
        Assert.AreEqual("item-2", layout.Tabs[0].Categories[0].Items[0].Id);
    }

    [TestMethod]
    public void MoveItemReordersWithinCategory()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        _ = manager.AddItem(tab.Id, new DesktopItem("item-1", "app-1"));
        _ = manager.AddItem(tab.Id, new DesktopItem("item-2", "app-2"));
        _ = manager.AddItem(tab.Id, new DesktopItem("item-3", "app-3"));

        Assert.IsTrue(manager.MoveItem(tab.Id, "item-3", 0));

        var items = manager.GetLayout().Tabs[0].Categories[0].Items;
        Assert.AreEqual("item-3", items[0].Id);
        Assert.AreEqual(0, items[0].Order);
        Assert.AreEqual("item-1", items[1].Id);
        Assert.AreEqual(1, items[1].Order);
    }

    [TestMethod]
    public void MoveItemWithUnchangedOrderReturnsFalse()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        _ = manager.AddItem(tab.Id, new DesktopItem("item-1", "app-1"));
        _ = manager.AddItem(tab.Id, new DesktopItem("item-2", "app-2"));

        Assert.IsFalse(manager.MoveItem(tab.Id, "item-1", 0));
    }

    [TestMethod]
    public void MoveItemValidation()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => manager.MoveItem(tab.Id, "item-1", -1));
    }

    [TestMethod]
    public void PublishDesktopChangedOnMutation()
    {
        var manager = CreateManager(out var events);
        var layout = new List<DesktopLayout>();
        using var subscription = events.Subscribe<DesktopChanged>(changed => layout.Add(changed.Layout));

        var tab = manager.CreateTab("Home");

        Assert.HasCount(1, layout);
        Assert.AreEqual(tab.Id, layout[0].ActiveTabId);
    }

    [TestMethod]
    public void SetLayoutPublishesChangedEvent()
    {
        var manager = CreateManager(out var events);
        var received = new List<DesktopChanged>();
        using var subscription = events.Subscribe<DesktopChanged>(received.Add);
        var layout = new DesktopLayout([new DesktopTab("home", "Home")]);

        manager.SetLayout(layout);

        Assert.HasCount(1, received);
        Assert.AreSame(layout, received[0].Layout);
    }
}