using GlazeShell.Core.Display;
using GlazeShell.Core.Models;

namespace GlazeShell.Core.Tests.Display;

[TestClass]
public sealed class ShellPlacementResolverTests
{
    private static MonitorInfo Monitor(
        int x = 0,
        int y = 0,
        int width = 1920,
        int height = 1080,
        double scale = 1.0,
        bool isPrimary = true,
        bool isConnected = true,
        string id = "\\\\.\\DISPLAY1")
    {
        return new MonitorInfo(
            id,
            id,
            new MonitorBounds(x, y, width, height),
            scaleFactor: scale,
            isPrimary: isPrimary,
            isConnected: isConnected);
    }

    [TestMethod]
    public void BottomAnchorSitsOnTheBottomEdge()
    {
        var placement = ShellPlacementResolver.Resolve(
            Monitor(),
            ShellAnchor.Bottom,
            ShellAlignment.Center,
            widthDip: 400,
            heightDip: 60);

        Assert.AreEqual(1080 - 60, placement.Y);
        Assert.AreEqual(60, placement.Height);
    }

    [TestMethod]
    public void TopAnchorSitsOnTheTopEdge()
    {
        var placement = ShellPlacementResolver.Resolve(
            Monitor(),
            ShellAnchor.Top,
            ShellAlignment.Center,
            widthDip: 400,
            heightDip: 60);

        Assert.AreEqual(0, placement.Y);
    }

    [TestMethod]
    public void CenterAlignmentCentersThePanel()
    {
        var placement = ShellPlacementResolver.Resolve(
            Monitor(),
            ShellAnchor.Bottom,
            ShellAlignment.Center,
            widthDip: 400,
            heightDip: 60);

        Assert.AreEqual((1920 - 400) / 2, placement.X);
        Assert.AreEqual(400, placement.Width);
    }

    [TestMethod]
    public void LeftAlignmentKeepsTheMargin()
    {
        var placement = ShellPlacementResolver.Resolve(
            Monitor(),
            ShellAnchor.Bottom,
            ShellAlignment.Left,
            widthDip: 400,
            heightDip: 60,
            marginDip: 24);

        Assert.AreEqual(24, placement.X);
    }

    [TestMethod]
    public void RightAlignmentCountsTheMarginFromTheRightEdge()
    {
        var placement = ShellPlacementResolver.Resolve(
            Monitor(),
            ShellAnchor.Bottom,
            ShellAlignment.Right,
            widthDip: 400,
            heightDip: 60,
            marginDip: 24);

        Assert.AreEqual(1920 - 400 - 24, placement.X);
    }

    [TestMethod]
    public void ScaleFactorConvertsLogicalUnitsToPixels()
    {
        var placement = ShellPlacementResolver.Resolve(
            Monitor(scale: 1.5),
            ShellAnchor.Top,
            ShellAlignment.Center,
            widthDip: 400,
            heightDip: 60,
            marginDip: 10);

        Assert.AreEqual(600, placement.Width);
        Assert.AreEqual(90, placement.Height);
        Assert.AreEqual(15, placement.Y);
    }

    [TestMethod]
    public void NegativeMonitorOriginIsKept()    {
        var placement = ShellPlacementResolver.Resolve(
            Monitor(x: -2560, y: -200),
            ShellAnchor.Bottom,
            ShellAlignment.Center,
            widthDip: 400,
            heightDip: 60);

        Assert.AreEqual(-2560 + ((1920 - 400) / 2), placement.X);
        Assert.AreEqual(-200 + 1080 - 60, placement.Y);
    }

    [TestMethod]
    public void TinyPanelStillHasAtLeastOnePixel()
    {
        var placement = ShellPlacementResolver.Resolve(
            Monitor(),
            ShellAnchor.Top,
            ShellAlignment.Center,
            widthDip: 0.01,
            heightDip: 0.01);

        Assert.AreEqual(1, placement.Width);
        Assert.AreEqual(1, placement.Height);
    }

    [TestMethod]
    public void StretchFillsTheMonitorWidthMinusMargins()
    {
        var placement = ShellPlacementResolver.Resolve(
            Monitor(),
            ShellAnchor.Top,
            ShellAlignment.Stretch,
            widthDip: 0,
            heightDip: 60,
            marginDip: 24,
            verticalMarginDip: 18);

        Assert.AreEqual(24, placement.X);
        Assert.AreEqual(1920 - 48, placement.Width);
        Assert.AreEqual(18, placement.Y);
    }

    [TestMethod]
    public void StretchUsesTheMonitorScaleForMargins()
    {
        var placement = ShellPlacementResolver.Resolve(
            Monitor(scale: 1.5),
            ShellAnchor.Top,
            ShellAlignment.Stretch,
            widthDip: 0,
            heightDip: 60,
            marginDip: 24);

        Assert.AreEqual(36, placement.X);
        Assert.AreEqual(1920 - 72, placement.Width);
        Assert.AreEqual(90, placement.Height);
    }

    [TestMethod]
    public void TargetIsThePrimaryMonitor()
    {
        var monitors = new[]
        {
            Monitor(id: "secondary", isPrimary: false),
            Monitor(id: "primary", isPrimary: true),
        };

        var target = ShellPlacementResolver.SelectTarget(monitors);

        Assert.IsNotNull(target);
        Assert.AreEqual("primary", target.Id);
    }

    [TestMethod]
    public void DisconnectedPrimaryFallsBackToTheFirstConnectedMonitor()
    {
        var monitors = new[]
        {
            Monitor(id: "primary", isPrimary: true, isConnected: false),
            Monitor(id: "secondary", isPrimary: false),
        };

        var target = ShellPlacementResolver.SelectTarget(monitors);

        Assert.IsNotNull(target);
        Assert.AreEqual("secondary", target.Id);
    }

    [TestMethod]
    public void NoMonitorsMeansNoTarget()
    {
        Assert.IsNull(ShellPlacementResolver.SelectTarget([]));
    }
}
