using GlazeShell.Core.Models;

namespace GlazeShell.Data.Serialization;

/// <summary>
/// Документ layout (`layout.json`).
/// </summary>
public sealed class LayoutDocument
{
    public int SchemaVersion { get; set; } = SchemaVersions.Layout;

    public int LayoutVersion { get; set; } = 1;

    public string? ActiveTabId { get; set; }

    public List<LayoutTabDocument> Tabs { get; set; } = [];

    public static LayoutDocument FromLayout(DesktopLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        return new LayoutDocument
        {
            SchemaVersion = SchemaVersions.Layout,
            LayoutVersion = layout.LayoutVersion,
            ActiveTabId = layout.ActiveTabId,
            Tabs = layout.Tabs.Select(static tab => new LayoutTabDocument
            {
                Id = tab.Id,
                Name = tab.Name,
                Order = tab.Order,
                MonitorId = tab.MonitorId,
                Categories = tab.Categories.Select(static category => new LayoutCategoryDocument
                {
                    Id = category.Id,
                    Name = category.Name,
                    Description = category.Description,
                    Order = category.Order,
                    Items = category.Items.Select(static item => new LayoutItemDocument
                    {
                        Id = item.Id,
                        ApplicationId = item.ApplicationId,
                        Order = item.Order,
                        Label = item.Label
                    }).ToList()
                }).ToList()
            }).ToList()
        };
    }
}

/// <summary>
/// Вкладка документа layout.
/// </summary>
public sealed class LayoutTabDocument
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public int Order { get; set; }

    /// <summary>
    /// Привязка вкладки к монитору. Хранится как строка, а не как native handle.
    /// </summary>
    public string? MonitorId { get; set; }

    public List<LayoutCategoryDocument> Categories { get; set; } = [];
}

/// <summary>
/// Категория документа layout.
/// </summary>
public sealed class LayoutCategoryDocument
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public int Order { get; set; }

    public List<LayoutItemDocument> Items { get; set; } = [];
}

/// <summary>
/// Элемент документа layout.
/// </summary>
public sealed class LayoutItemDocument
{
    public string? Id { get; set; }

    public string? ApplicationId { get; set; }

    public int Order { get; set; }

    public string? Label { get; set; }
}
