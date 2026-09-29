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
    public void CreateTabInsertsAtOrder()
    {
        var manager = CreateManager(out _);
        _ = manager.CreateTab("First");
        _ = manager.CreateTab("Last");

        var inserted = manager.CreateTab("Middle", 1);

        var tabs = manager.GetLayout().Tabs;
        Assert.HasCount(3, tabs);
        Assert.AreEqual(inserted.Id, tabs[1].Id);
        Assert.AreEqual(0, tabs[0].Order);
        Assert.AreEqual(1, tabs[1].Order);
        Assert.AreEqual(2, tabs[2].Order);
    }

    [TestMethod]
    public void CreateTabAppendsByDefault()
    {
        var manager = CreateManager(out _);
        var first = manager.CreateTab("First");

        var second = manager.CreateTab("Second");

        var tabs = manager.GetLayout().Tabs;
        Assert.AreEqual(first.Id, tabs[0].Id);
        Assert.AreEqual(second.Id, tabs[1].Id);
        Assert.AreEqual(1, tabs[1].Order);
    }

    [TestMethod]
    public void CreateCategoryAppendsByDefault()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        var first = manager.CreateCategory(tab.Id, "First")!;

        var second = manager.CreateCategory(tab.Id, "Second")!;

        var categories = manager.GetLayout().Tabs[0].Categories;
        Assert.AreEqual(first.Id, categories[0].Id);
        Assert.AreEqual(second.Id, categories[1].Id);
        Assert.AreEqual(1, categories[1].Order);
    }

    [TestMethod]
    public void CreateTabClampsOrderToEnd()
    {
        var manager = CreateManager(out _);
        _ = manager.CreateTab("First");

        var appended = manager.CreateTab("Appended", 99);

        var tabs = manager.GetLayout().Tabs;
        Assert.AreEqual(appended.Id, tabs[1].Id);
        Assert.AreEqual(1, tabs[1].Order);
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
    public void RemoveLastTabClearsActiveTab()
    {
        var manager = CreateManager(out _);
        var only = manager.CreateTab("Only");

        Assert.IsTrue(manager.RemoveTab(only.Id));

        var layout = manager.GetLayout();
        Assert.IsEmpty(layout.Tabs);
        Assert.IsNull(layout.ActiveTabId);
    }

    [TestMethod]
    public void RemoveTabRenumbersRemaining()
    {
        var manager = CreateManager(out _);
        _ = manager.CreateTab("First");
        _ = manager.CreateTab("Second");
        var last = manager.CreateTab("Third");

        Assert.IsTrue(manager.RemoveTab(last.Id));

        var tabs = manager.GetLayout().Tabs;
        Assert.HasCount(2, tabs);
        Assert.AreEqual(0, tabs[0].Order);
        Assert.AreEqual(1, tabs[1].Order);
    }

    [TestMethod]
    public void RemoveUnknownTabReturnsFalse()
    {
        var manager = CreateManager(out _);

        Assert.IsFalse(manager.RemoveTab("unknown"));
    }

    [TestMethod]
    public void RenameTabUpdatesName()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");

        Assert.IsTrue(manager.RenameTab(tab.Id, "Start"));

        Assert.AreEqual("Start", manager.GetLayout().Tabs[0].Name);
    }

    [TestMethod]
    public void RenameTabWithSameNameReturnsFalse()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");

        Assert.IsFalse(manager.RenameTab(tab.Id, "Home"));
        Assert.IsFalse(manager.RenameTab("unknown", "Other"));
    }

    [TestMethod]
    public void RenameTabValidation()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");

        Assert.ThrowsExactly<ArgumentException>(() => manager.RenameTab(tab.Id, " "));
        Assert.ThrowsExactly<ArgumentException>(() => manager.RenameTab(" ", "Home"));
    }

    [TestMethod]
    public void MoveTabReorders()
    {
        var manager = CreateManager(out _);
        var first = manager.CreateTab("First");
        _ = manager.CreateTab("Second");
        var third = manager.CreateTab("Third");

        Assert.IsTrue(manager.MoveTab(third.Id, 0));

        var tabs = manager.GetLayout().Tabs;
        Assert.AreEqual(third.Id, tabs[0].Id);
        Assert.AreEqual(0, tabs[0].Order);
        Assert.AreEqual(first.Id, tabs[1].Id);
        Assert.AreEqual(1, tabs[1].Order);
    }

    [TestMethod]
    public void MoveTabWithUnchangedOrderReturnsFalse()
    {
        var manager = CreateManager(out _);
        var first = manager.CreateTab("First");
        _ = manager.CreateTab("Second");

        Assert.IsFalse(manager.MoveTab(first.Id, 0));
        Assert.IsFalse(manager.MoveTab("unknown", 1));
    }

    [TestMethod]
    public void MoveTabValidation()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => manager.MoveTab(tab.Id, -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => manager.CreateTab("Other", -1));
    }

    [TestMethod]
    public void MoveTabKeepsActiveTab()
    {
        var manager = CreateManager(out _);
        var first = manager.CreateTab("First");
        var second = manager.CreateTab("Second");

        Assert.IsTrue(manager.MoveTab(second.Id, 0));

        var layout = manager.GetLayout();
        Assert.AreEqual(first.Id, layout.ActiveTabId);
        Assert.AreEqual(second.Id, layout.Tabs[0].Id);
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
    public void CreateCategoryAddsToTab()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");

        var category = manager.CreateCategory(tab.Id, "Games")!;

        Assert.IsNotNull(category);
        var categories = manager.GetLayout().Tabs[0].Categories;
        Assert.HasCount(1, categories);
        Assert.AreEqual(category.Id, categories[0].Id);
        Assert.AreEqual(0, categories[0].Order);
    }

    [TestMethod]
    public void CreateCategoryInsertsAtOrder()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        _ = manager.CreateCategory(tab.Id, "First");
        _ = manager.CreateCategory(tab.Id, "Last");

        var inserted = manager.CreateCategory(tab.Id, "Middle", 1)!;

        var categories = manager.GetLayout().Tabs[0].Categories;
        Assert.HasCount(3, categories);
        Assert.AreEqual(inserted.Id, categories[1].Id);
        Assert.AreEqual(1, categories[1].Order);
        Assert.AreEqual(2, categories[2].Order);
    }

    [TestMethod]
    public void CreateCategoryForUnknownTabReturnsNull()
    {
        var manager = CreateManager(out _);

        Assert.IsNull(manager.CreateCategory("unknown", "Games"));
    }

    [TestMethod]
    public void CreateCategoryValidation()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");

        Assert.ThrowsExactly<ArgumentException>(() => manager.CreateCategory(tab.Id, " "));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => manager.CreateCategory(tab.Id, "Games", -1));
    }

    [TestMethod]
    public void RemoveCategoryRemovesCategory()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        var games = manager.CreateCategory(tab.Id, "Games")!;
        _ = manager.CreateCategory(tab.Id, "Work");

        Assert.IsTrue(manager.RemoveCategory(tab.Id, games.Id));
        Assert.IsFalse(manager.RemoveCategory(tab.Id, "missing"));
        Assert.IsFalse(manager.RemoveCategory("unknown", games.Id));

        var categories = manager.GetLayout().Tabs[0].Categories;
        Assert.HasCount(1, categories);
        Assert.AreEqual("Work", categories[0].Name);
        Assert.AreEqual(0, categories[0].Order);
    }

    [TestMethod]
    public void RemoveLastCategoryLeavesTabEmpty()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        var games = manager.CreateCategory(tab.Id, "Games")!;

        Assert.IsTrue(manager.RemoveCategory(tab.Id, games.Id));

        Assert.IsEmpty(manager.GetLayout().Tabs[0].Categories);
    }

    [TestMethod]
    public void RenameCategoryUpdatesName()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        var games = manager.CreateCategory(tab.Id, "Games")!;

        Assert.IsTrue(manager.RenameCategory(tab.Id, games.Id, "Play"));
        Assert.IsFalse(manager.RenameCategory(tab.Id, games.Id, "Play"));
        Assert.IsFalse(manager.RenameCategory(tab.Id, "missing", "Play"));
        Assert.IsFalse(manager.RenameCategory("unknown", games.Id, "Play"));

        Assert.AreEqual("Play", manager.GetLayout().Tabs[0].Categories[0].Name);
    }

    [TestMethod]
    public void MoveCategoryReorders()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        var games = manager.CreateCategory(tab.Id, "Games")!;
        _ = manager.CreateCategory(tab.Id, "Work");
        var media = manager.CreateCategory(tab.Id, "Media")!;

        Assert.IsTrue(manager.MoveCategory(tab.Id, media.Id, 0));

        var categories = manager.GetLayout().Tabs[0].Categories;
        Assert.AreEqual(media.Id, categories[0].Id);
        Assert.AreEqual(0, categories[0].Order);
        Assert.AreEqual(games.Id, categories[1].Id);
        Assert.AreEqual(1, categories[1].Order);
    }

    [TestMethod]
    public void MoveCategoryWithUnchangedOrderReturnsFalse()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        var games = manager.CreateCategory(tab.Id, "Games")!;
        _ = manager.CreateCategory(tab.Id, "Work");

        Assert.IsFalse(manager.MoveCategory(tab.Id, games.Id, 0));
        Assert.IsFalse(manager.MoveCategory(tab.Id, "missing", 1));
    }

    [TestMethod]
    public void AddApplicationAppendsToMainCategory()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        var item = new DesktopItem("item-1", "app-1");

        Assert.IsTrue(manager.AddApplication(tab.Id, item));

        var layout = manager.GetLayout();
        Assert.HasCount(1, layout.Tabs[0].Categories);
        Assert.HasCount(1, layout.Tabs[0].Categories[0].Items);
        Assert.AreEqual("item-1", layout.Tabs[0].Categories[0].Items[0].Id);
    }

    [TestMethod]
    public void AddApplicationCreatesDefaultCategoryOnce()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");

        Assert.IsTrue(manager.AddApplication(tab.Id, new DesktopItem("item-1", "app-1")));
        Assert.IsTrue(manager.AddApplication(tab.Id, new DesktopItem("item-2", "app-2")));

        var categories = manager.GetLayout().Tabs[0].Categories;
        Assert.HasCount(1, categories);
        Assert.AreEqual("main", categories[0].Id);
        Assert.HasCount(2, categories[0].Items);
        Assert.AreEqual(0, categories[0].Items[0].Order);
        Assert.AreEqual(1, categories[0].Items[1].Order);
    }

    [TestMethod]
    public void AddApplicationToSpecificCategory()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        var games = manager.CreateCategory(tab.Id, "Games")!;
        var work = manager.CreateCategory(tab.Id, "Work")!;

        Assert.IsTrue(manager.AddApplication(tab.Id, new DesktopItem("item-1", "steam"), work.Id));
        Assert.IsFalse(manager.AddApplication(tab.Id, new DesktopItem("item-2", "vs"), "missing"));

        var categories = manager.GetLayout().Tabs[0].Categories;
        Assert.IsEmpty(categories[0].Items);
        Assert.HasCount(1, categories[1].Items);
        Assert.AreEqual("item-1", categories[1].Items[0].Id);
        Assert.AreEqual(games.Id, categories[0].Id);
    }

    [TestMethod]
    public void AddApplicationToEmptyTabIgnoresUnknownCategory()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");

        Assert.IsFalse(manager.AddApplication(tab.Id, new DesktopItem("item-1", "app-1"), "missing"));
        Assert.IsEmpty(manager.GetLayout().Tabs[0].Categories);
    }

    [TestMethod]
    public void AddDuplicateItemReturnsFalse()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        var item = new DesktopItem("item-1", "app-1");

        Assert.IsTrue(manager.AddApplication(tab.Id, item));
        Assert.IsFalse(manager.AddApplication(tab.Id, item));
    }

    [TestMethod]
    public void AddApplicationRejectsDuplicateAcrossCategories()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        var games = manager.CreateCategory(tab.Id, "Games")!;
        var work = manager.CreateCategory(tab.Id, "Work")!;
        var item = new DesktopItem("item-1", "app-1");

        Assert.IsTrue(manager.AddApplication(tab.Id, item, games.Id));
        Assert.IsFalse(manager.AddApplication(tab.Id, item, work.Id));
    }

    [TestMethod]
    public void AddApplicationToUnknownTabReturnsFalse()
    {
        var manager = CreateManager(out _);

        Assert.IsFalse(manager.AddApplication("unknown", new DesktopItem("item-1", "app-1")));
    }

    [TestMethod]
    public void AddApplicationValidation()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");

        Assert.ThrowsExactly<ArgumentException>(() => manager.AddApplication(" ", new DesktopItem("item-1", "app-1")));
        Assert.ThrowsExactly<ArgumentNullException>(() => manager.AddApplication(tab.Id, null!));
        Assert.ThrowsExactly<ArgumentException>(() => manager.AddApplication(tab.Id, new DesktopItem("item-1", "app-1"), " "));
    }

    [TestMethod]
    public void RemoveApplicationRemovesFromCategory()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        _ = manager.AddApplication(tab.Id, new DesktopItem("item-1", "app-1"));
        _ = manager.AddApplication(tab.Id, new DesktopItem("item-2", "app-2"));

        Assert.IsTrue(manager.RemoveApplication(tab.Id, "item-1"));
        Assert.IsFalse(manager.RemoveApplication(tab.Id, "missing"));
        Assert.IsFalse(manager.RemoveApplication("unknown", "item-1"));

        var layout = manager.GetLayout();
        Assert.HasCount(1, layout.Tabs[0].Categories[0].Items);
        Assert.AreEqual("item-2", layout.Tabs[0].Categories[0].Items[0].Id);
        Assert.AreEqual(0, layout.Tabs[0].Categories[0].Items[0].Order);
    }

    [TestMethod]
    public void RemoveApplicationRenumbersRemaining()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        _ = manager.AddApplication(tab.Id, new DesktopItem("item-1", "app-1"));
        _ = manager.AddApplication(tab.Id, new DesktopItem("item-2", "app-2"));
        _ = manager.AddApplication(tab.Id, new DesktopItem("item-3", "app-3"));

        Assert.IsTrue(manager.RemoveApplication(tab.Id, "item-1"));

        var items = manager.GetLayout().Tabs[0].Categories[0].Items;
        Assert.HasCount(2, items);
        Assert.AreEqual(0, items[0].Order);
        Assert.AreEqual(1, items[1].Order);
    }

    [TestMethod]
    public void MoveApplicationReordersWithinCategory()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        _ = manager.AddApplication(tab.Id, new DesktopItem("item-1", "app-1"));
        _ = manager.AddApplication(tab.Id, new DesktopItem("item-2", "app-2"));
        _ = manager.AddApplication(tab.Id, new DesktopItem("item-3", "app-3"));

        Assert.IsTrue(manager.MoveApplication(tab.Id, "item-3", null, 0));

        var items = manager.GetLayout().Tabs[0].Categories[0].Items;
        Assert.AreEqual("item-3", items[0].Id);
        Assert.AreEqual(0, items[0].Order);
        Assert.AreEqual("item-1", items[1].Id);
        Assert.AreEqual(1, items[1].Order);
    }

    [TestMethod]
    public void MoveApplicationWithUnchangedOrderReturnsFalse()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        _ = manager.AddApplication(tab.Id, new DesktopItem("item-1", "app-1"));
        _ = manager.AddApplication(tab.Id, new DesktopItem("item-2", "app-2"));

        Assert.IsFalse(manager.MoveApplication(tab.Id, "item-1", null, 0));
    }

    [TestMethod]
    public void MoveApplicationBetweenCategories()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        var games = manager.CreateCategory(tab.Id, "Games")!;
        var work = manager.CreateCategory(tab.Id, "Work")!;
        _ = manager.AddApplication(tab.Id, new DesktopItem("item-1", "steam"), games.Id);
        _ = manager.AddApplication(tab.Id, new DesktopItem("item-2", "vs"), work.Id);

        Assert.IsTrue(manager.MoveApplication(tab.Id, "item-1", work.Id, 0));

        var categories = manager.GetLayout().Tabs[0].Categories;
        Assert.IsEmpty(categories[0].Items);
        Assert.HasCount(2, categories[1].Items);
        Assert.AreEqual("item-1", categories[1].Items[0].Id);
        Assert.AreEqual(0, categories[1].Items[0].Order);
        Assert.AreEqual("item-2", categories[1].Items[1].Id);
        Assert.AreEqual(1, categories[1].Items[1].Order);
    }

    [TestMethod]
    public void MoveApplicationRenumbersSourceCategory()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        var games = manager.CreateCategory(tab.Id, "Games")!;
        var work = manager.CreateCategory(tab.Id, "Work")!;
        _ = manager.AddApplication(tab.Id, new DesktopItem("item-1", "steam"), games.Id);
        _ = manager.AddApplication(tab.Id, new DesktopItem("item-2", "discord"), games.Id);
        _ = manager.AddApplication(tab.Id, new DesktopItem("item-3", "gitlab"), games.Id);

        Assert.IsTrue(manager.MoveApplication(tab.Id, "item-1", work.Id, 0));

        var items = manager.GetLayout().Tabs[0].Categories[0].Items;
        Assert.HasCount(2, items);
        Assert.AreEqual(0, items[0].Order);
        Assert.AreEqual(1, items[1].Order);
    }

    [TestMethod]
    public void MoveApplicationToUnknownCategoryReturnsFalse()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");
        _ = manager.AddApplication(tab.Id, new DesktopItem("item-1", "app-1"));

        Assert.IsFalse(manager.MoveApplication(tab.Id, "item-1", "missing", 0));
        Assert.IsFalse(manager.MoveApplication(tab.Id, "missing", null, 0));
        Assert.IsFalse(manager.MoveApplication("unknown", "item-1", null, 0));
    }

    [TestMethod]
    public void MoveApplicationValidation()
    {
        var manager = CreateManager(out _);
        var tab = manager.CreateTab("Home");

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => manager.MoveApplication(tab.Id, "item-1", null, -1));
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
    public void NoOpDoesNotPublishDesktopChanged()
    {
        var manager = CreateManager(out var events);
        var tab = manager.CreateTab("Home");
        _ = manager.AddApplication(tab.Id, new DesktopItem("item-1", "app-1"));
        var received = new List<DesktopChanged>();
        using var subscription = events.Subscribe<DesktopChanged>(received.Add);

        Assert.IsFalse(manager.RenameTab(tab.Id, "Home"));
        Assert.IsFalse(manager.MoveTab(tab.Id, 0));
        Assert.IsFalse(manager.RemoveApplication(tab.Id, "missing"));

        Assert.IsEmpty(received);
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
        Assert.AreEqual("home", received[0].Layout.Tabs[0].Id);
        Assert.AreSame(manager.GetLayout(), received[0].Layout);
    }

    [TestMethod]
    public void SetLayoutNormalizesOrder()
    {
        var manager = CreateManager(out _);
        var layout = new DesktopLayout(
        [
            new DesktopTab("second", "Second", order: 5),
            new DesktopTab("first", "First", order: 1),
        ],
        activeTabId: "first",
        layoutVersion: 3);

        manager.SetLayout(layout);

        var result = manager.GetLayout();
        Assert.AreEqual("first", result.Tabs[0].Id);
        Assert.AreEqual("second", result.Tabs[1].Id);
        Assert.AreEqual(0, result.Tabs[0].Order);
        Assert.AreEqual(1, result.Tabs[1].Order);
        Assert.AreEqual(3, result.LayoutVersion);
        Assert.AreEqual("first", result.ActiveTabId);
    }

    [TestMethod]
    public void InitialLayoutIsNormalized()
    {
        var events = new EventManager();
        var layout = new DesktopLayout(
        [
            new DesktopTab(
                "home",
                "Home",
                [new ApplicationCategory("later", "Later", [new DesktopItem("a", "app-a", order: 3)], order: 7)]),
        ]);

        var manager = new DesktopManager(events, layout);

        var tab = manager.GetLayout().Tabs[0];
        Assert.AreEqual(0, tab.Order);
        Assert.AreEqual(0, tab.Categories[0].Order);
        Assert.AreEqual(0, tab.Categories[0].Items[0].Order);
    }
}
