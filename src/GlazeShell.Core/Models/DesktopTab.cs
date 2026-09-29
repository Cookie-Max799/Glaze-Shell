namespace GlazeShell.Core.Models;

public sealed record DesktopTab
{
    public DesktopTab(
        string id,
        string name,
        IEnumerable<ApplicationCategory>? categories = null,
        int order = 0)
    {
        Id = ModelValidation.Required(id, nameof(id));
        Name = ModelValidation.Required(name, nameof(name));
        Categories = ModelValidation.Copy(categories, nameof(categories));
        ModelValidation.NonNegative(order, nameof(order));
        Order = order;
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<ApplicationCategory> Categories { get; }

    public int Order { get; }

    public DesktopTab With(
        string? name = null,
        IReadOnlyList<ApplicationCategory>? categories = null,
        int? order = null) =>
        new(Id, name ?? Name, categories ?? Categories, order ?? Order);
}
