namespace GlazeShell.Core.Models;

public sealed record DesktopTab
{
    public DesktopTab(
        string id,
        string name,
        IEnumerable<ApplicationCategory>? categories = null,
        int order = 0,
        bool isActive = false)
    {
        Id = ModelValidation.Required(id, nameof(id));
        Name = ModelValidation.Required(name, nameof(name));
        Categories = ModelValidation.Copy(categories, nameof(categories));
        ModelValidation.NonNegative(order, nameof(order));
        Order = order;
        IsActive = isActive;
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<ApplicationCategory> Categories { get; }

    public int Order { get; }

    public bool IsActive { get; }
}
