using GlazeShell.Windows.SystemEvents;

namespace GlazeShell.Windows.Tests;

[TestClass]
public sealed class ProcessWatchOptionsTests
{
    [TestMethod]
    public void DefaultIntervalIsOneSecond()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(1.0), ProcessWatchOptions.Default.Interval);
    }

    [TestMethod]
    public void DefaultIsValid()
    {
        Assert.AreSame(ProcessWatchOptions.Default, ProcessWatchOptions.Default.Validate());
    }

    [TestMethod]
    public void AcceptsBoundaryIntervals()
    {
        _ = new ProcessWatchOptions { Interval = TimeSpan.FromMilliseconds(500) }.Validate();
        _ = new ProcessWatchOptions { Interval = TimeSpan.FromMinutes(5) }.Validate();
    }

    [TestMethod]
    public void RejectsTooShortInterval()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ProcessWatchOptions { Interval = TimeSpan.FromMilliseconds(499) }.Validate());
    }

    [TestMethod]
    public void RejectsTooLongInterval()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ProcessWatchOptions { Interval = TimeSpan.FromMinutes(5) + TimeSpan.FromMilliseconds(1) }.Validate());
    }
}
