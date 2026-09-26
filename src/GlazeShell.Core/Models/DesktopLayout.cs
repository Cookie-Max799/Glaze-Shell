namespace GlazeShell.Core.Models;

public sealed record DesktopLayout
{
    public DesktopLayout(
        IEnumerable<DesktopTab> tabs,
        string? activeTabId = null,
        int layoutVersion = 1)
    {
        Tabs = ModelValidation.Copy(tabs, nameof(tabs));
        ModelValidation.Positive(layoutVersion, nameof(layoutVersion));
        LayoutVersion = layoutVersion;

        var normalizedActiveTabId = ModelValidation.Optional(activeTabId, nameof(activeTabId));
        if (normalizedActiveTabId is not null && Tabs.All(tab => !string.Equals(tab.Id, normalizedActiveTabId, StringComparison.Ordinal)))
        {
            throw new ArgumentException("The active tab must exist in the layout.", nameof(activeTabId));
        }

        ActiveTabId = normalizedActiveTabId;
    }

    public IReadOnlyList<DesktopTab> Tabs { get; }

    public string? ActiveTabId { get; }

    public int LayoutVersion { get; }
}
