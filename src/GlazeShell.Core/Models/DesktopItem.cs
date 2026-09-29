namespace GlazeShell.Core.Models;

public sealed record DesktopItem
{
    public DesktopItem(string id, string applicationId, int order = 0, string? label = null)
    {
        Id = ModelValidation.Required(id, nameof(id));
        ApplicationId = ModelValidation.Required(applicationId, nameof(applicationId));
        ModelValidation.NonNegative(order, nameof(order));
        Order = order;
        Label = ModelValidation.Optional(label, nameof(label));
    }

    public string Id { get; }

    public string ApplicationId { get; }

    public int Order { get; }

    public string? Label { get; }

    public DesktopItem WithOrder(int order) => new(Id, ApplicationId, order, Label);
}
