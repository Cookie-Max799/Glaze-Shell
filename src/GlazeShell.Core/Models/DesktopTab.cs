namespace GlazeShell.Core.Models;

public sealed record DesktopTab
{
    public DesktopTab(
        string id,
        string name,
        IEnumerable<ApplicationCategory>? categories = null,
        int order = 0,
        string? monitorId = null)
    {
        Id = ModelValidation.Required(id, nameof(id));
        Name = ModelValidation.Required(name, nameof(name));
        Categories = ModelValidation.Copy(categories, nameof(categories));
        ModelValidation.NonNegative(order, nameof(order));
        Order = order;
        MonitorId = ModelValidation.Optional(monitorId, nameof(monitorId));
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<ApplicationCategory> Categories { get; }

    public int Order { get; }

    /// <summary>
    /// Идентификатор монитора, которому привязана вкладка, или <c>null</c>, если вкладка не привязана.
    /// Идентификатор — строка, а не <c>HMONITOR</c>: Core не зависит от Win32.
    /// </summary>
    public string? MonitorId { get; }

    public DesktopTab With(
        string? name = null,
        IReadOnlyList<ApplicationCategory>? categories = null,
        int? order = null) =>
        new(Id, name ?? Name, categories ?? Categories, order ?? Order, MonitorId);

    /// <summary>
    /// Создаёт копию вкладки с новой привязкой к монитору. <c>null</c> снимает привязку.
    /// </summary>
    public DesktopTab WithMonitor(string? monitorId) =>
        new(Id, Name, Categories, Order, monitorId);
}
