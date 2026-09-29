using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;

namespace GlazeShell.Core.Services;

public sealed class DesktopManager : IDesktopManager
{
    private const string MainCategoryId = "main";
    private const string MainCategoryName = "Основная";

    private readonly IEventManager _eventManager;
    private readonly object _sync = new();
    private DesktopLayout _layout;

    public DesktopManager(IEventManager eventManager, DesktopLayout? initialLayout = null)
    {
        _eventManager = eventManager ?? throw new ArgumentNullException(nameof(eventManager));
        _layout = initialLayout ?? new DesktopLayout(Array.Empty<DesktopTab>());
    }

    public DesktopLayout GetLayout()
    {
        lock (_sync)
        {
            return _layout;
        }
    }

    public void SetLayout(DesktopLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        lock (_sync)
        {
            _layout = layout;
        }

        PublishChanged(layout);
    }

    public DesktopTab CreateTab(string name, int order = 0)
    {
        var tab = new DesktopTab(Guid.NewGuid().ToString("N"), name, order: order);

        lock (_sync)
        {
            var tabs = _layout.Tabs.Append(tab).ToArray();
            _layout = new DesktopLayout(tabs, _layout.ActiveTabId ?? tab.Id);
        }

        PublishChanged(_layout);
        return tab;
    }

    public bool RemoveTab(string tabId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);

        lock (_sync)
        {
            if (_layout.Tabs.All(tab => !SameId(tab.Id, tabId)))
            {
                return false;
            }

            var tabs = _layout.Tabs.Where(tab => !SameId(tab.Id, tabId)).ToArray();
            var activeTabId = SameId(_layout.ActiveTabId, tabId)
                ? (tabs.Length > 0 ? tabs[0].Id : null)
                : _layout.ActiveTabId;

            _layout = new DesktopLayout(tabs, activeTabId);
        }

        PublishChanged(_layout);
        return true;
    }

    public bool ActivateTab(string tabId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);

        lock (_sync)
        {
            if (SameId(_layout.ActiveTabId, tabId))
            {
                return false;
            }

            if (_layout.Tabs.All(tab => !SameId(tab.Id, tabId)))
            {
                return false;
            }

            _layout = new DesktopLayout(_layout.Tabs, tabId);
        }

        PublishChanged(_layout);
        return true;
    }

    public bool AddItem(string tabId, DesktopItem item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);
        ArgumentNullException.ThrowIfNull(item);

        lock (_sync)
        {
            var tabIndex = FindTab(tabId);
            if (tabIndex < 0 || ContainsItem(_layout.Tabs[tabIndex], item.Id))
            {
                return false;
            }

            var tab = _layout.Tabs[tabIndex];
            var categories = AddToMainCategory(tab, item);
            var tabs = ReplaceTab(tabIndex, RebuildTab(tab, categories));
            _layout = new DesktopLayout(tabs, _layout.ActiveTabId);
        }

        PublishChanged(_layout);
        return true;
    }

    public bool RemoveItem(string tabId, string itemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        lock (_sync)
        {
            var tabIndex = FindTab(tabId);
            if (tabIndex < 0 || !ContainsItem(_layout.Tabs[tabIndex], itemId))
            {
                return false;
            }

            var tab = _layout.Tabs[tabIndex];
            var categories = tab.Categories
                .Select(category => new ApplicationCategory(
                    category.Id,
                    category.Name,
                    category.Items.Where(item => !SameId(item.Id, itemId)).ToArray(),
                    category.Description))
                .ToArray();

            var tabs = ReplaceTab(tabIndex, RebuildTab(tab, categories));
            _layout = new DesktopLayout(tabs, _layout.ActiveTabId);
        }

        PublishChanged(_layout);
        return true;
    }

    public bool MoveItem(string tabId, string itemId, int newOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        if (newOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newOrder), newOrder, "The order cannot be negative.");
        }

        lock (_sync)
        {
            var tabIndex = FindTab(tabId);
            if (tabIndex < 0)
            {
                return false;
            }

            var tab = _layout.Tabs[tabIndex];
            var categoryIndex = FindCategory(tab, itemId);
            if (categoryIndex < 0)
            {
                return false;
            }

            var category = tab.Categories[categoryIndex];
            var reordered = Reorder(category.Items, itemId, newOrder);
            if (reordered is null)
            {
                return false;
            }

            if (SameIdSequence(category.Items, reordered))
            {
                return false;
            }

            var categories = tab.Categories.ToArray();
            categories[categoryIndex] = new ApplicationCategory(
                category.Id,
                category.Name,
                reordered,
                category.Description);

            var tabs = ReplaceTab(tabIndex, RebuildTab(tab, categories));
            _layout = new DesktopLayout(tabs, _layout.ActiveTabId);
        }

        PublishChanged(_layout);
        return true;
    }

    private DesktopTab[] ReplaceTab(int index, DesktopTab replacement)
    {
        var tabs = _layout.Tabs.ToArray();
        tabs[index] = replacement;
        return tabs;
    }

    private int FindTab(string tabId)
    {
        for (var index = 0; index < _layout.Tabs.Count; index++)
        {
            if (SameId(_layout.Tabs[index].Id, tabId))
            {
                return index;
            }
        }

        return -1;
    }

    private static DesktopTab RebuildTab(DesktopTab tab, IReadOnlyList<ApplicationCategory> categories) =>
        new(
            tab.Id,
            tab.Name,
            categories,
            tab.Order,
            tab.IsActive);

    private static ApplicationCategory[] AddToMainCategory(DesktopTab tab, DesktopItem item)
    {
        if (tab.Categories.Count == 0)
        {
            return [new ApplicationCategory(MainCategoryId, MainCategoryName, [item])];
        }

        var categories = tab.Categories.ToArray();
        var main = categories[0];
        categories[0] = new ApplicationCategory(
            main.Id,
            main.Name,
            main.Items.Append(item).ToArray(),
            main.Description);
        return categories;
    }

    private static int FindCategory(DesktopTab tab, string itemId)
    {
        for (var index = 0; index < tab.Categories.Count; index++)
        {
            if (tab.Categories[index].Items.Any(item => SameId(item.Id, itemId)))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool ContainsItem(DesktopTab tab, string itemId) => FindCategory(tab, itemId) >= 0;

    private static DesktopItem[]? Reorder(IReadOnlyList<DesktopItem> items, string itemId, int newOrder)
    {
        DesktopItem? target = null;
        var rest = new List<DesktopItem>(items.Count);

        foreach (var item in items)
        {
            if (SameId(item.Id, itemId))
            {
                target = item;
            }
            else
            {
                rest.Add(item);
            }
        }

        if (target is null)
        {
            return null;
        }

        rest.Insert(Math.Min(newOrder, rest.Count), target);

        var reordered = new DesktopItem[rest.Count];
        for (var index = 0; index < rest.Count; index++)
        {
            var item = rest[index];
            reordered[index] = new DesktopItem(
                item.Id,
                item.ApplicationId,
                index,
                item.Label);
        }

        return reordered;
    }

    private static bool SameIdSequence(IReadOnlyList<DesktopItem> left, DesktopItem[] right)
    {
        if (left.Count != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!SameId(left[index].Id, right[index].Id))
            {
                return false;
            }
        }

        return true;
    }

    private void PublishChanged(DesktopLayout layout) => _eventManager.Publish(new DesktopChanged(layout));

    private static bool SameId(string? left, string? right) =>
        string.Equals(left, right, StringComparison.Ordinal);
}