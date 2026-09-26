namespace GlazeShell.Core.Models;

public sealed record ApplicationCategory
{
    public ApplicationCategory(
        string id,
        string name,
        IEnumerable<DesktopItem>? items = null,
        string? description = null)
    {
        Id = ModelValidation.Required(id, nameof(id));
        Name = ModelValidation.Required(name, nameof(name));
        Items = ModelValidation.Copy(items, nameof(items));
        Description = ModelValidation.Optional(description, nameof(description));
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<DesktopItem> Items { get; }

    public string? Description { get; }
}
