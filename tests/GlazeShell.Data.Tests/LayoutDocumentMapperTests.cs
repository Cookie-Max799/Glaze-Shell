using System.Globalization;
using GlazeShell.Data.Serialization;

namespace GlazeShell.Data.Tests;

[TestClass]
public sealed class LayoutDocumentMapperTests
{
    [TestMethod]
    public void MapsMinimalLayout()
    {
        var document = new LayoutDocument
        {
            ActiveTabId = "home",
            Tabs = [Tab("home", "Home")]
        };

        Assert.IsTrue(LayoutDocumentMapper.TryMap(document, out var layout, out var diagnostics));

        Assert.IsEmpty(diagnostics);
        Assert.IsNotNull(layout);
        Assert.AreEqual("home", layout.ActiveTabId);
        Assert.HasCount(1, layout.Tabs);
        Assert.HasCount(1, layout.Tabs[0].Categories);
        Assert.HasCount(1, layout.Tabs[0].Categories[0].Items);
    }

    [TestMethod]
    public void RejectsMissingDocument()
    {
        Assert.IsFalse(LayoutDocumentMapper.TryMap(null, out var layout, out var diagnostics));

        Assert.IsNull(layout);
        StringAssert.Contains(diagnostics.Single(), "missing");
    }

    [TestMethod]
    public void AcceptsNullActiveTab()
    {
        var document = new LayoutDocument { ActiveTabId = null, Tabs = [Tab("home", "Home")] };

        Assert.IsTrue(LayoutDocumentMapper.TryMap(document, out var layout, out _));
        Assert.IsNull(layout!.ActiveTabId);
    }

    [TestMethod]
    public void RejectsTooManyTabs()
    {
        var document = new LayoutDocument
        {
            Tabs = Enumerable.Range(0, PersistenceLimits.MaxTabs + 1)
                .Select(static index => Tab($"tab-{index}", "Tab"))
                .ToList()
        };

        Assert.IsFalse(LayoutDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(
            diagnostics.Single(),
            PersistenceLimits.MaxTabs.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void RejectsNonPositiveLayoutVersion()
    {
        var document = new LayoutDocument { LayoutVersion = 0, Tabs = [Tab("home", "Home")] };

        Assert.IsFalse(LayoutDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "version");
    }

    [TestMethod]
    public void RejectsDuplicateTabIdentifier()
    {
        var document = new LayoutDocument { Tabs = [Tab("home", "Home"), Tab("home", "Other")] };

        Assert.IsFalse(LayoutDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "duplicated");
    }

    [TestMethod]
    public void RejectsNullTab()
    {
        var document = new LayoutDocument { Tabs = [null!, Tab("home", "Home")] };

        Assert.IsFalse(LayoutDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "is null");
    }

    [TestMethod]
    public void RejectsInvalidTabName()
    {
        var document = new LayoutDocument { Tabs = [Tab("home", "  ")] };

        Assert.IsFalse(LayoutDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "invalid name");
    }

    [TestMethod]
    public void RejectsNegativeTabOrder()
    {
        var tab = Tab("home", "Home");
        tab.Order = -1;
        var document = new LayoutDocument { Tabs = [tab] };

        Assert.IsFalse(LayoutDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "negative order");
    }

    [TestMethod]
    public void RejectsInvalidMonitorBinding()
    {
        var tab = Tab("home", "Home");
        tab.MonitorId = "bad\nid";
        var document = new LayoutDocument { Tabs = [tab] };

        Assert.IsFalse(LayoutDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "invalid monitor binding");
    }

    [TestMethod]
    public void RejectsUnknownActiveTab()
    {
        var document = new LayoutDocument { ActiveTabId = "missing", Tabs = [Tab("home", "Home")] };

        Assert.IsFalse(LayoutDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "does not exist");
    }

    [TestMethod]
    public void RejectsTooManyCategories()
    {
        var tab = Tab("home", "Home");
        tab.Categories = Enumerable.Range(0, PersistenceLimits.MaxCategoriesPerTab + 1)
            .Select(static index => Category($"cat-{index}", "Category"))
            .ToList();
        var document = new LayoutDocument { Tabs = [tab] };

        Assert.IsFalse(LayoutDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(
            diagnostics.Single(),
            PersistenceLimits.MaxCategoriesPerTab.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void RejectsDuplicateCategoryIdentifier()
    {
        var tab = Tab("home", "Home");
        tab.Categories = [Category("cat", "One"), Category("cat", "Two")];
        var document = new LayoutDocument { Tabs = [tab] };

        Assert.IsFalse(LayoutDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "duplicated category");
    }

    [TestMethod]
    public void RejectsNullCategory()
    {
        var tab = Tab("home", "Home");
        tab.Categories = [null!, Category("cat", "One")];
        var document = new LayoutDocument { Tabs = [tab] };

        Assert.IsFalse(LayoutDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "is null");
    }

    [TestMethod]
    public void RejectsNegativeCategoryOrder()
    {
        var category = Category("cat", "One");
        category.Order = -1;
        var tab = Tab("home", "Home");
        tab.Categories = [category];
        var document = new LayoutDocument { Tabs = [tab] };

        Assert.IsFalse(LayoutDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "negative order");
    }

    [TestMethod]
    public void RejectsItemWithoutApplicationReference()
    {
        var category = Category("cat", "One");
        category.Items = [Item("item-1", " ")];
        var tab = Tab("home", "Home");
        tab.Categories = [category];
        var document = new LayoutDocument { Tabs = [tab] };

        Assert.IsFalse(LayoutDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "invalid application");
    }

    [TestMethod]
    public void RejectsDuplicateItemAcrossCategories()
    {
        var first = Category("cat-1", "One");
        first.Items = [Item("item-1", "app:1")];
        var second = Category("cat-2", "Two");
        second.Items = [Item("item-1", "app:2")];
        var tab = Tab("home", "Home");
        tab.Categories = [first, second];
        var document = new LayoutDocument { Tabs = [tab] };

        Assert.IsFalse(LayoutDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "duplicated item");
    }

    [TestMethod]
    public void RejectsItemWithInvalidLabel()
    {
        var category = Category("cat", "One");
        var item = Item("item-1", "app:1");
        item.Label = "bad\nlabel";
        category.Items = [item];
        var tab = Tab("home", "Home");
        tab.Categories = [category];
        var document = new LayoutDocument { Tabs = [tab] };

        Assert.IsFalse(LayoutDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "invalid label");
    }

    [TestMethod]
    public void RejectsTooManyItems()
    {
        var category = Category("cat", "One");
        category.Items = Enumerable.Range(0, PersistenceLimits.MaxItemsPerCategory + 1)
            .Select(static index => Item($"item-{index}", "app:1"))
            .ToList();
        var tab = Tab("home", "Home");
        tab.Categories = [category];
        var document = new LayoutDocument { Tabs = [tab] };

        Assert.IsFalse(LayoutDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(
            diagnostics.Single(),
            PersistenceLimits.MaxItemsPerCategory.ToString(CultureInfo.InvariantCulture));
    }

    private static LayoutTabDocument Tab(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Categories = [Category("cat-1", "Category")]
    };

    private static LayoutCategoryDocument Category(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Items = [Item("item-1", "app:1")]
    };

    private static LayoutItemDocument Item(string id, string applicationId) => new()
    {
        Id = id,
        ApplicationId = applicationId
    };
}
