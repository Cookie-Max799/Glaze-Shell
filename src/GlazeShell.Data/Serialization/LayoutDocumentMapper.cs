using GlazeShell.Core.Models;

namespace GlazeShell.Data.Serialization;

/// <summary>
/// Преобразование документа layout в доменную модель с проверкой структурной целостности.
/// </summary>
/// <remarks>
/// Проверяется то, без чего модель или <see cref="DesktopManager"/> работают некорректно:
/// уникальность идентификаторов (вкладок, категорий внутри вкладки, элементов внутри вкладки),
/// допустимость порядка, допустимость имён и существование активной вкладки.
/// Документ с нарушениями отклоняется целиком: частичное восстановление скрыло бы
/// потерю данных и оставило бы layout в непредсказуемом состоянии.
/// </remarks>
public static class LayoutDocumentMapper
{
    public static bool TryMap(LayoutDocument? document, out DesktopLayout? layout, out IReadOnlyList<string> diagnostics)
    {
        var messages = new Diagnostics(new List<string>());

        if (document is null)
        {
            layout = null;
            diagnostics = ["The layout document is missing."];
            return false;
        }

        if (document.Tabs is null || document.Tabs.Count > PersistenceLimits.MaxTabs)
        {
            layout = null;
            diagnostics = [$"The layout contains more than {PersistenceLimits.MaxTabs} tabs."];
            return false;
        }

        if (document.LayoutVersion < 1)
        {
            messages.Add($"The layout version {document.LayoutVersion} is not valid.");
        }

        var tabs = new List<DesktopTab>(document.Tabs.Count);
        var tabIds = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < document.Tabs.Count; index++)
        {
            var tabDocument = document.Tabs[index];
            if (tabDocument is null)
            {
                messages.Add($"Tab #{index} is null.");
                continue;
            }

            if (!PersistenceLimits.IsValidIdentifier(tabDocument.Id))
            {
                messages.Add($"Tab #{index} has an invalid identifier.");
                continue;
            }

            if (!tabIds.Add(tabDocument.Id!))
            {
                messages.Add($"Tab '{tabDocument.Id}' is duplicated.");
                continue;
            }

            if (!PersistenceLimits.IsValidName(tabDocument.Name))
            {
                messages.Add($"Tab '{tabDocument.Id}' has an invalid name.");
                continue;
            }

            if (tabDocument.Order < 0)
            {
                messages.Add($"Tab '{tabDocument.Id}' has a negative order.");
                continue;
            }

            if (tabDocument.MonitorId is not null && !PersistenceLimits.IsValidIdentifier(tabDocument.MonitorId))
            {
                messages.Add($"Tab '{tabDocument.Id}' has an invalid monitor binding.");
                continue;
            }

            var categories = MapCategories(tabDocument, tabDocument.Id!, messages);
            if (categories is null)
            {
                continue;
            }

            tabs.Add(new DesktopTab(
                tabDocument.Id!,
                tabDocument.Name!,
                categories,
                tabDocument.Order,
                tabDocument.MonitorId));
        }

        var activeTabId = document.ActiveTabId;
        if (activeTabId is not null && !tabIds.Contains(activeTabId))
        {
            messages.Add($"The active tab '{activeTabId}' does not exist in the layout.");
            activeTabId = null;
        }

        if (messages.Count > 0)
        {
            layout = null;
            diagnostics = messages.ToArray();
            return false;
        }

        layout = new DesktopLayout(tabs, activeTabId, document.LayoutVersion);
        diagnostics = Array.Empty<string>();
        return true;
    }
    private static List<ApplicationCategory>? MapCategories(LayoutTabDocument tab, string tabId, Diagnostics messages)
    {
        var categories = tab.Categories ?? [];

        if (categories.Count > PersistenceLimits.MaxCategoriesPerTab)
        {
            messages.Add($"Tab '{tabId}' contains more than {PersistenceLimits.MaxCategoriesPerTab} categories.");
            return null;
        }

        var result = new List<ApplicationCategory>(categories.Count);
        var categoryIds = new HashSet<string>(StringComparer.Ordinal);
        var itemIds = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < categories.Count; index++)
        {
            var categoryDocument = categories[index];
            if (categoryDocument is null)
            {
                messages.Add($"Tab '{tabId}', category #{index} is null.");
                return null;
            }

            if (!PersistenceLimits.IsValidIdentifier(categoryDocument.Id))
            {
                messages.Add($"Tab '{tabId}', category #{index} has an invalid identifier.");
                return null;
            }

            if (!categoryIds.Add(categoryDocument.Id!))
            {
                messages.Add($"Tab '{tabId}' contains the duplicated category '{categoryDocument.Id}'.");
                return null;
            }

            if (!PersistenceLimits.IsValidName(categoryDocument.Name))
            {
                messages.Add($"Tab '{tabId}', category '{categoryDocument.Id}' has an invalid name.");
                return null;
            }

            if (categoryDocument.Description is not null && !PersistenceLimits.IsValidName(categoryDocument.Description))
            {
                messages.Add($"Tab '{tabId}', category '{categoryDocument.Id}' has an invalid description.");
                return null;
            }

            if (categoryDocument.Order < 0)
            {
                messages.Add($"Tab '{tabId}', category '{categoryDocument.Id}' has a negative order.");
                return null;
            }

            var items = MapItems(categoryDocument, tabId, itemIds, messages);
            if (items is null)
            {
                return null;
            }

            result.Add(new ApplicationCategory(
                categoryDocument.Id!,
                categoryDocument.Name!,
                items,
                categoryDocument.Description,
                categoryDocument.Order));
        }

        return result;
    }

    private static List<DesktopItem>? MapItems(LayoutCategoryDocument category, string tabId, HashSet<string> itemIds, Diagnostics messages)
    {
        var items = category.Items ?? [];

        if (items.Count > PersistenceLimits.MaxItemsPerCategory)
        {
            messages.Add($"Tab '{tabId}', category '{category.Id}' contains more than {PersistenceLimits.MaxItemsPerCategory} items.");
            return null;
        }

        var result = new List<DesktopItem>(items.Count);

        for (var index = 0; index < items.Count; index++)
        {
            var itemDocument = items[index];
            if (itemDocument is null)
            {
                messages.Add($"Tab '{tabId}', category '{category.Id}', item #{index} is null.");
                return null;
            }

            if (!PersistenceLimits.IsValidIdentifier(itemDocument.Id))
            {
                messages.Add($"Tab '{tabId}', category '{category.Id}', item #{index} has an invalid identifier.");
                return null;
            }

            if (!itemIds.Add(itemDocument.Id!))
            {
                messages.Add($"Tab '{tabId}' contains the duplicated item '{itemDocument.Id}'.");
                return null;
            }

            if (!PersistenceLimits.IsValidIdentifier(itemDocument.ApplicationId))
            {
                messages.Add($"Item '{itemDocument.Id}' references an invalid application.");
                return null;
            }

            if (itemDocument.Label is not null && !PersistenceLimits.IsValidName(itemDocument.Label))
            {
                messages.Add($"Item '{itemDocument.Id}' has an invalid label.");
                return null;
            }

            if (itemDocument.Order < 0)
            {
                messages.Add($"Item '{itemDocument.Id}' has a negative order.");
                return null;
            }

            result.Add(new DesktopItem(
                itemDocument.Id!,
                itemDocument.ApplicationId!,
                itemDocument.Order,
                itemDocument.Label));
        }

        return result;
    }

    /// <summary>
    /// Список диагностик с ограничением длины: повреждённый документ не должен
    /// порождать тысячи сообщений, которые затем попадут в лог и память вызывающей стороны.
    /// </summary>
    private sealed class Diagnostics(List<string> messages)
    {
        private const int MaxMessages = 20;

        private int _suppressed;

        public int Count => messages.Count;

        public void Add(string message)
        {
            if (messages.Count < MaxMessages)
            {
                messages.Add(message);
            }
            else
            {
                _suppressed++;
            }
        }

        public List<string> ToArray()
        {
            if (_suppressed == 0)
            {
                return messages;
            }

            messages.Add($"And {_suppressed} more problems were omitted.");
            return messages;
        }
    }
}
