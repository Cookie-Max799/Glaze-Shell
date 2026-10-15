using GlazeShell.Core.Discovery;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class ApplicationDiscoveryOptionsTests
{
    [TestMethod]
    public void DefaultOptionsAreValid()
    {
        Assert.AreSame(ApplicationDiscoveryOptions.Default, ApplicationDiscoveryOptions.Default.Validate());
    }

    [TestMethod]
    public void NegativeCacheDurationIsRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ApplicationDiscoveryOptions { CacheDuration = TimeSpan.FromSeconds(-1) }.Validate());
    }

    [TestMethod]
    public void ZeroCacheDurationIsAllowed()
    {
        var options = new ApplicationDiscoveryOptions { CacheDuration = TimeSpan.Zero }.Validate();

        Assert.AreEqual(TimeSpan.Zero, options.CacheDuration);
    }

    [TestMethod]
    public void NonPositiveStartMenuDepthIsRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ApplicationDiscoveryOptions { MaxStartMenuDepth = 0 }.Validate());
    }

    [TestMethod]
    public void NonPositiveWarningLimitIsRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ApplicationDiscoveryOptions { MaxWarningsPerSource = 0 }.Validate());
    }

    [TestMethod]
    public void ValidCustomOptionsPass()
    {
        var options = new ApplicationDiscoveryOptions
        {
            CacheDuration = TimeSpan.FromSeconds(1),
            MaxStartMenuDepth = 1,
            MaxWarningsPerSource = 1,
            IncludeSystemApplications = false,
            RequireExistingTarget = false
        }.Validate();

        Assert.AreEqual(TimeSpan.FromSeconds(1), options.CacheDuration);
        Assert.AreEqual(1, options.MaxStartMenuDepth);
        Assert.IsFalse(options.IncludeSystemApplications);
        Assert.IsFalse(options.RequireExistingTarget);
    }
}
