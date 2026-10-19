using GlazeShell.Windows.Applications;

namespace GlazeShell.Windows.Tests;

[TestClass]
public sealed class ShellIconProviderTests
{
    [TestMethod]
    public void ReturnsNullForMissingTargets()
    {
        Assert.IsNull(ShellIconProvider.TryGetIcon(null));
        Assert.IsNull(ShellIconProvider.TryGetIcon("   "));
        Assert.IsNull(ShellIconProvider.TryGetIcon(
            Path.Combine(Path.GetTempPath(), "glaze-missing-icon-target.exe")));
    }

    [TestMethod]
    public void ReadsIconPixelsForAnExecutable()
    {
        var path = Environment.ProcessPath;

        Assert.IsNotNull(path);

        var pixels = ShellIconProvider.TryGetIcon(path!, 32);

        Assert.IsNotNull(pixels);
        Assert.IsGreaterThan(0, pixels!.Width);
        Assert.IsGreaterThan(0, pixels.Height);
        Assert.HasCount(pixels.Width * pixels.Height * 4, pixels.Bgra);
    }
}
