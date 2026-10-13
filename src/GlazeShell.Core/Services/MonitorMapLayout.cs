using GlazeShell.Core.Models;

namespace GlazeShell.Core.Services;

/// <summary>
/// Позиция монитора на схематичной карте. Координаты и размеры даны в единицах холста.
/// </summary>
public sealed record MonitorMapPlacement(string MonitorId, double Left, double Top, double Width, double Height);

/// <summary>
/// Раскладывает мониторы на плоскости с сохранением их взаимного расположения,
/// чтобы панель могла нарисовать простую карту без знаний о пикселях рабочих столов.
/// Масштаб единый по обеим осям, поэтому пропорции мониторов сохраняются.
/// </summary>
public static class MonitorMapLayout
{
    public static IReadOnlyList<MonitorMapPlacement> Compute(
        IReadOnlyList<MonitorInfo> monitors,
        double canvasWidth,
        double canvasHeight,
        double padding = 0)
    {
        ArgumentNullException.ThrowIfNull(monitors);

        if (monitors.Count == 0 || canvasWidth <= 0 || canvasHeight <= 0 || padding < 0)
        {
            return Array.Empty<MonitorMapPlacement>();
        }

        var minX = monitors.Min(static monitor => monitor.Bounds.X);
        var minY = monitors.Min(static monitor => monitor.Bounds.Y);
        var maxX = monitors.Max(static monitor => monitor.Bounds.X + monitor.Bounds.Width);
        var maxY = monitors.Max(static monitor => monitor.Bounds.Y + monitor.Bounds.Height);

        var contentWidth = Math.Max(1, maxX - minX);
        var contentHeight = Math.Max(1, maxY - minY);

        var availableWidth = Math.Max(1, canvasWidth - (2 * padding));
        var availableHeight = Math.Max(1, canvasHeight - (2 * padding));
        var scale = Math.Min(availableWidth / contentWidth, availableHeight / contentHeight);

        var offsetX = padding + ((availableWidth - (contentWidth * scale)) / 2);
        var offsetY = padding + ((availableHeight - (contentHeight * scale)) / 2);

        var placements = new List<MonitorMapPlacement>(monitors.Count);

        foreach (var monitor in monitors)
        {
            placements.Add(new MonitorMapPlacement(
                monitor.Id,
                offsetX + ((monitor.Bounds.X - minX) * scale),
                offsetY + ((monitor.Bounds.Y - minY) * scale),
                monitor.Bounds.Width * scale,
                monitor.Bounds.Height * scale));
        }

        return placements;
    }
}
