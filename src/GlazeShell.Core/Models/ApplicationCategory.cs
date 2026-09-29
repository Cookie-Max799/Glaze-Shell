namespace GlazeShell.Core.Models;

public sealed record ApplicationCategory
{
    public ApplicationCategory(
        string id,
        string name,
        IEnumerable<DesktopItem>? items = null,
        string? description = null,
        int order = 0)
    {
        Id = ModelValidation.Required(id, nameof(id));
        Name = ModelValidation.Required(name, nameof(name));
        Items = ModelValidation.Copy(items, nameof(items));
        Description = ModelValidation.Optional(description, nameof(description));
        ModelValidation.NonNegative(order, nameof(order));
        Order = order;
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<DesktopItem> Items { get; }

    public string? Description { get; }

    public int Order { get; }

    public ApplicationCategory With(
        string? name = null,
        IReadOnlyList<DesktopItem>? items = null,
        int? order = null) =>
        new(Id, name ?? Name, items ?? Items, Description, order ?? Order);
}
