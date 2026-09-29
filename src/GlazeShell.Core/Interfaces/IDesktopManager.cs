using GlazeShell.Core.Models;

namespace GlazeShell.Core.Interfaces;

public interface IDesktopManager
{
    DesktopLayout GetLayout();

    void SetLayout(DesktopLayout layout);

    DesktopTab CreateTab(string name, int order = 0);

    bool RemoveTab(string tabId);

    bool ActivateTab(string tabId);

    bool AddItem(string tabId, DesktopItem item);

    bool RemoveItem(string tabId, string itemId);

    bool MoveItem(string tabId, string itemId, int newOrder);
}