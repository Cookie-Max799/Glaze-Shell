using GlazeShell.Core.Models;
using GlazeShell.Core.Services;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class MonitorMapLayoutTests
{
    [TestMethod]
    public void ComputeReturnsEmptyForNoMonitors()
    {
        var placements = MonitorMapLayout.Compute(Array.Empty<MonitorInfo>(), 260, 118);

        Assert.IsEmpty(placements);
    }

    [TestMethod]
    public void ComputeReturnsEmptyForNonPositiveCanvas()
    {
        var monitors = new[] { Monitor("left", 0, 0, 1920, 1080) };

        Assert.IsEmpty(MonitorMapLayout.Compute(monitors, 0, 118));
        Assert.IsEmpty(MonitorMapLayout.Compute(monitors, 260, 0));
    }

    [TestMethod]
    public void ComputePlacesSideBySideMonitorsInOrder()
    {
        var left = Monitor("left", 0, 0, 1920, 1080);
        var right = Monitor("right", 1920, 0, 1920, 1080);

        var placements = MonitorMapLayout.Compute(new[] { left, right }, 260, 118, padding: 4);

        Assert.HasCount(2, placements);

        var leftPlacement = placements.Single(placement => placement.MonitorId == "left");
        var rightPlacement = placements.Single(placement => placement.MonitorId == "right");

        // Одинаковые мониторы получают одинаковый размер, правый сдвинут вправо.
        Assert.AreEqual(leftPlacement.Width, rightPlacement.Width);
        Assert.AreEqual(leftPlacement.Height, rightPlacement.Height);
        Assert.IsGreaterThan(leftPlacement.Left, rightPlacement.Left);
        Assert.AreEqual(leftPlacement.Top, rightPlacement.Top);
    }

    [TestMethod]
    public void ComputePreservesAspectRatioAndFitsCanvas()
    {
        var monitors = new[] { Monitor("wide", 0, 0, 3840, 1080) };

        var placements = MonitorMapLayout.Compute(monitors, 260, 118, padding: 4);

        var placement = placements.Single();

        // Доступно 252 в ширину и 110 в высоту. Единый масштаб по меньшему измерению,
        // поэтому итог помещается в холст, а соотношение сторон сохраняется.
        Assert.IsLessThanOrEqualTo(260, placement.Width);
        Assert.IsLessThanOrEqualTo(118, placement.Height);
        Assert.AreEqual(3840.0 / 1080.0, placement.Width / placement.Height, 0.0001);
    }

    [TestMethod]
    public void ComputeCentersContentWithinCanvas()
    {
        var monitors = new[] { Monitor("single", 0, 0, 1000, 1000) };

        var placements = MonitorMapLayout.Compute(monitors, 260, 118, padding: 4);

        var placement = placements.Single();
        var leftGap = placement.Left;
        var rightGap = 260 - (placement.Left + placement.Width);
        var topGap = placement.Top;
        var bottomGap = 118 - (placement.Top + placement.Height);

        Assert.AreEqual(leftGap, rightGap, 0.0001);
        Assert.AreEqual(topGap, bottomGap, 0.0001);
    }

    private static MonitorInfo Monitor(string id, int x, int y, int width, int height) =>
        new(id, $"\\\\.\\{id}", new MonitorBounds(x, y, width, height));
}
