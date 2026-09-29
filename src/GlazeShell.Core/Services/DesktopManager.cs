using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;

namespace GlazeShell.Core.Services;

public sealed class DesktopManager : IDesktopManager
{
    private const string DefaultCategoryId = "main";
    private const string DefaultCategoryName = "Основная";

    private readonly IEventManager _eventManager;
    private readonly object _sync = new();
    private DesktopLayout _layout;

    public DesktopManager(IEventManager eventManager, DesktopLayout? initialLayout = null)
    {
        _eventManager = eventManager ?? throw new ArgumentNullException(nameof(eventManager));
        _layout = initialLayout is null
            ? new DesktopLayout(Array.Empty<DesktopTab>())
            : Normalize(initialLayout);
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
        var normalized = Normalize(layout);

        lock (_sync)
        {
            _layout = normalized;
        }

        PublishChanged(normalized);
    }

    public DesktopTab CreateTab(string name, int? order = null)
    {
        if (order is not null)
        {
            ModelValidation.NonNegative(order.Value, nameof(order));
        }

        var tab = new DesktopTab(NewId(), name);

        Mutate(layout =>
        {
            var tabs = layout.Tabs.ToList();
            tabs.Insert(Position(order, tabs.Count), tab);
            return new DesktopLayout(NormalizeTabs(tabs), layout.ActiveTabId ?? tab.Id);
        });

        return tab;
    }

    public bool RemoveTab(string tabId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);

        return Mutate(layout =>
        {
            var index = FindTab(layout, tabId);
            if (index < 0)
            {
                return null;
            }

            var remaining = NormalizeTabs(layout.Tabs.Where((_, position) => position != index));
            var active = SameId(layout.ActiveTabId, tabId)
                ? (remaining.Count > 0 ? remaining[0].Id : null)
                : layout.ActiveTabId;

            return new DesktopLayout(remaining, active);
        });
    }

    public bool RenameTab(string tabId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);
        var newName = ModelValidation.Required(name, nameof(name));

        return Mutate(layout =>
        {
            var index = FindTab(layout, tabId);
            if (index < 0)
            {
                return null;
            }

            var tab = layout.Tabs[index];
            return SameName(tab.Name, newName)
                ? null
                : ReplaceTab(layout, index, tab.With(name: newName));
        });
    }

    public bool MoveTab(string tabId, int newOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);
        ModelValidation.NonNegative(newOrder, nameof(newOrder));

        return Mutate(layout =>
        {
            var index = FindTab(layout, tabId);
            if (index < 0)
            {
                return null;
            }

            var (tabs, changed) = MoveWithin(layout.Tabs, index, newOrder, static (tab, order) => tab.With(order: order));
            return changed ? new DesktopLayout(tabs, layout.ActiveTabId) : null;
        });
    }

    public bool ActivateTab(string tabId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);

        return Mutate(layout =>
            SameId(layout.ActiveTabId, tabId) || FindTab(layout, tabId) < 0
                ? null
                : new DesktopLayout(layout.Tabs, tabId));
    }

    public ApplicationCategory? CreateCategory(string tabId, string name, int? order = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);
        if (order is not null)
        {
            ModelValidation.NonNegative(order.Value, nameof(order));
        }

        var category = new ApplicationCategory(NewId(), name);
        var created = false;

        Mutate(layout =>
        {
            var tabIndex = FindTab(layout, tabId);
            if (tabIndex < 0)
            {
                return null;
            }

            var tab = layout.Tabs[tabIndex];
            var categories = tab.Categories.ToList();
            categories.Insert(Position(order, categories.Count), category);
            created = true;

            return ReplaceTab(layout, tabIndex, tab.With(categories: NormalizeCategories(categories)));
        });

        return created ? category : null;
    }

    public bool RemoveCategory(string tabId, string categoryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryId);

        return Mutate(layout =>
        {
            var tabIndex = FindTab(layout, tabId);
            if (tabIndex < 0)
            {
                return null;
            }

            var tab = layout.Tabs[tabIndex];
            var categoryIndex = FindCategory(tab, categoryId);
            if (categoryIndex < 0)
            {
                return null;
            }

            var categories = tab.Categories.Where((_, position) => position != categoryIndex);
            return ReplaceTab(layout, tabIndex, tab.With(categories: NormalizeCategories(categories)));
        });
    }

    public bool RenameCategory(string tabId, string categoryId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryId);
        var newName = ModelValidation.Required(name, nameof(name));

        return Mutate(layout =>
        {
            var tabIndex = FindTab(layout, tabId);
            if (tabIndex < 0)
            {
                return null;
            }

            var tab = layout.Tabs[tabIndex];
            var categoryIndex = FindCategory(tab, categoryId);
            if (categoryIndex < 0)
            {
                return null;
            }

            var category = tab.Categories[categoryIndex];
            if (SameName(category.Name, newName))
            {
                return null;
            }

            var categories = tab.Categories.ToList();
            categories[categoryIndex] = category.With(name: newName);
            return ReplaceTab(layout, tabIndex, tab.With(categories: categories));
        });
    }

    public bool MoveCategory(string tabId, string categoryId, int newOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryId);
        ModelValidation.NonNegative(newOrder, nameof(newOrder));

        return Mutate(layout =>
        {
            var tabIndex = FindTab(layout, tabId);
            if (tabIndex < 0)
            {
                return null;
            }

            var tab = layout.Tabs[tabIndex];
            var categoryIndex = FindCategory(tab, categoryId);
            if (categoryIndex < 0)
            {
                return null;
            }

            var (categories, changed) = MoveWithin(
                tab.Categories,
                categoryIndex,
                newOrder,
                static (category, order) => category.With(order: order));

            return changed ? ReplaceTab(layout, tabIndex, tab.With(categories: categories)) : null;
        });
    }

    public bool AddApplication(string tabId, DesktopItem item, string? categoryId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);
        ArgumentNullException.ThrowIfNull(item);
        if (categoryId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(categoryId);
        }

        return Mutate(layout =>
        {
            var tabIndex = FindTab(layout, tabId);
            if (tabIndex < 0)
            {
                return null;
            }

            var tab = layout.Tabs[tabIndex];
            if (FindCategoryByItem(tab, item.Id) >= 0)
            {
                return null;
            }

            var categories = tab.Categories.ToList();

            if (categories.Count == 0)
            {
                if (categoryId is not null)
                {
                    return null;
                }

                categories.Add(new ApplicationCategory(DefaultCategoryId, DefaultCategoryName, [item]));
            }
            else
            {
                var categoryIndex = categoryId is null ? 0 : FindCategory(tab, categoryId);
                if (categoryIndex < 0)
                {
                    return null;
                }

                var category = categories[categoryIndex];
                categories[categoryIndex] = category.With(items: NormalizeItems(category.Items.Append(item.WithOrder(category.Items.Count))));
            }

            return ReplaceTab(layout, tabIndex, tab.With(categories: NormalizeCategories(categories)));
        });
    }

    public bool RemoveApplication(string tabId, string itemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        return Mutate(layout =>
        {
            var tabIndex = FindTab(layout, tabId);
            if (tabIndex < 0)
            {
                return null;
            }

            var tab = layout.Tabs[tabIndex];
            var categoryIndex = FindCategoryByItem(tab, itemId);
            if (categoryIndex < 0)
            {
                return null;
            }

            var category = tab.Categories[categoryIndex];
            var categories = tab.Categories.ToList();
            categories[categoryIndex] = category.With(items: NormalizeItems(category.Items.Where(candidate => !SameId(candidate.Id, itemId))));

            return ReplaceTab(layout, tabIndex, tab.With(categories: NormalizeCategories(categories)));
        });
    }

    public bool MoveApplication(string tabId, string itemId, string? targetCategoryId = null, int newOrder = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        if (targetCategoryId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(targetCategoryId);
        }

        ModelValidation.NonNegative(newOrder, nameof(newOrder));

        return Mutate(layout =>
        {
            var tabIndex = FindTab(layout, tabId);
            if (tabIndex < 0)
            {
                return null;
            }

            var tab = layout.Tabs[tabIndex];
            var sourceIndex = FindCategoryByItem(tab, itemId);
            if (sourceIndex < 0)
            {
                return null;
            }

            var targetIndex = targetCategoryId is null ? sourceIndex : FindCategory(tab, targetCategoryId);
            if (targetIndex < 0)
            {
                return null;
            }

            var source = tab.Categories[sourceIndex];
            var categories = tab.Categories.ToList();

            if (sourceIndex == targetIndex)
            {
                var (items, changed) = MoveWithin(
                    source.Items,
                    IndexOfItem(source, itemId),
                    newOrder,
                    static (item, order) => item.WithOrder(order));

                if (!changed)
                {
                    return null;
                }

                categories[sourceIndex] = source.With(items: items);
            }
            else
            {
                var target = tab.Categories[targetIndex];
                var item = source.Items[IndexOfItem(source, itemId)];

                categories[sourceIndex] = source.With(items: NormalizeItems(source.Items.Where(candidate => !SameId(candidate.Id, itemId))));

                categories[targetIndex] = target.With(items: NormalizeItems(InsertAt(target.Items, item, newOrder)));
            }

            return ReplaceTab(layout, tabIndex, tab.With(categories: NormalizeCategories(categories)));
        });
    }

    private bool Mutate(Func<DesktopLayout, DesktopLayout?> mutation)
    {
        DesktopLayout updated;
        lock (_sync)
        {
            var next = mutation(_layout);
            if (next is null)
            {
                return false;
            }

            _layout = updated = next;
        }

        PublishChanged(updated);
        return true;
    }

    private void PublishChanged(DesktopLayout layout) => _eventManager.Publish(new DesktopChanged(layout));

    private static DesktopLayout Normalize(DesktopLayout layout) =>
        new(NormalizeTabs(layout.Tabs), layout.ActiveTabId, layout.LayoutVersion);

    private static List<DesktopTab> NormalizeTabs(IEnumerable<DesktopTab> tabs)
    {
        var ordered = StableOrder(tabs, static tab => tab.Order);
        var result = new List<DesktopTab>(ordered.Length);
        for (var index = 0; index < ordered.Length; index++)
        {
            result.Add(ordered[index].With(order: index, categories: NormalizeCategories(ordered[index].Categories)));
        }

        return result;
    }

    private static List<ApplicationCategory> NormalizeCategories(IEnumerable<ApplicationCategory> categories)
    {
        var ordered = StableOrder(categories, static category => category.Order);
        var result = new List<ApplicationCategory>(ordered.Length);
        for (var index = 0; index < ordered.Length; index++)
        {
            result.Add(ordered[index].With(order: index, items: NormalizeItems(ordered[index].Items)));
        }

        return result;
    }

    private static List<DesktopItem> NormalizeItems(IEnumerable<DesktopItem> items)
    {
        var ordered = StableOrder(items, static item => item.Order);
        var result = new List<DesktopItem>(ordered.Length);
        for (var index = 0; index < ordered.Length; index++)
        {
            result.Add(ordered[index].WithOrder(index));
        }

        return result;
    }

    private static T[] StableOrder<T>(IEnumerable<T> source, Func<T, int> selector) =>
        source
            .Select((value, index) => (value, index))
            .OrderBy(entry => selector(entry.value))
            .ThenBy(entry => entry.index)
            .Select(entry => entry.value)
            .ToArray();

    private static (T[] Items, bool Changed) MoveWithin<T>(
        IReadOnlyList<T> source,
        int currentIndex,
        int newOrder,
        Func<T, int, T> rebuild)
    {
        var target = Math.Min(newOrder, source.Count - 1);
        var rest = new List<T>(source.Count - 1);
        for (var index = 0; index < source.Count; index++)
        {
            if (index != currentIndex)
            {
                rest.Add(source[index]);
            }
        }

        rest.Insert(target, source[currentIndex]);

        var items = new T[rest.Count];
        for (var index = 0; index < rest.Count; index++)
        {
            items[index] = rebuild(rest[index], index);
        }

        return (items, target != currentIndex);
    }

    private static List<DesktopItem> InsertAt(IReadOnlyList<DesktopItem> items, DesktopItem item, int newOrder)
    {
        var result = items.ToList();
        var position = Position(newOrder, result.Count);
        result.Insert(position, item.WithOrder(position));
        return result;
    }

    private static int Position(int? order, int count) =>
        order is null ? count : Math.Min(order.Value, count);

    private static DesktopLayout ReplaceTab(DesktopLayout layout, int index, DesktopTab replacement)
    {
        var tabs = layout.Tabs.ToArray();
        tabs[index] = replacement;
        return new DesktopLayout(tabs, layout.ActiveTabId);
    }

    private static int FindTab(DesktopLayout layout, string tabId)
    {
        for (var index = 0; index < layout.Tabs.Count; index++)
        {
            if (SameId(layout.Tabs[index].Id, tabId))
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindCategory(DesktopTab tab, string categoryId)
    {
        for (var index = 0; index < tab.Categories.Count; index++)
        {
            if (SameId(tab.Categories[index].Id, categoryId))
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindCategoryByItem(DesktopTab tab, string itemId)
    {
        for (var index = 0; index < tab.Categories.Count; index++)
        {
            if (IndexOfItem(tab.Categories[index], itemId) >= 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static int IndexOfItem(ApplicationCategory category, string itemId)
    {
        for (var index = 0; index < category.Items.Count; index++)
        {
            if (SameId(category.Items[index].Id, itemId))
            {
                return index;
            }
        }

        return -1;
    }

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static bool SameId(string? left, string? right) =>
        string.Equals(left, right, StringComparison.Ordinal);

    private static bool SameName(string left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal);
}
