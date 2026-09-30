using GlazeShell.Core.Models;

namespace GlazeShell.Core.Interfaces;

public interface IDesktopManager
{
    DesktopLayout GetLayout();

    void SetLayout(DesktopLayout layout);

    DesktopTab CreateTab(string name, int? order = null);

    bool RemoveTab(string tabId);

    bool RenameTab(string tabId, string name);

    bool MoveTab(string tabId, int newOrder);

    bool ActivateTab(string tabId);

    ApplicationCategory? CreateCategory(string tabId, string name, int? order = null);

    bool RemoveCategory(string tabId, string categoryId);

    bool RenameCategory(string tabId, string categoryId, string name);

    bool MoveCategory(string tabId, string categoryId, int newOrder);

    bool AddApplication(string tabId, DesktopItem item, string? categoryId = null);

    bool RemoveApplication(string tabId, string itemId);

    bool MoveApplication(string tabId, string itemId, string? targetCategoryId = null, int newOrder = 0);

    bool AssignTabToMonitor(string tabId, string? monitorId);

    IReadOnlyList<DesktopTab> GetTabsForMonitor(string monitorId);
}
