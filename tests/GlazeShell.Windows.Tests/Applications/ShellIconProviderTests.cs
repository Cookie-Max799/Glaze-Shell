using GlazeShell.Windows.Applications;

namespace GlazeShell.Windows.Tests.Applications;

[TestClass]
public sealed class ShellIconProviderTests
{
    [TestMethod]
    public void TryGetIconReturnsNullForMissingPath()
    {
        Assert.IsNull(ShellIconProvider.TryGetIcon(null));
        Assert.IsNull(ShellIconProvider.TryGetIcon("   "));
        Assert.IsNull(ShellIconProvider.TryGetIcon(
            Path.Combine(Path.GetTempPath(), $"glaze-missing-{Guid.NewGuid():N}", "child.bin")));
        Assert.IsNull(ShellIconProvider.TryGetIcon(Path.GetTempPath(), size: 0));
    }

    [TestMethod]
    public void TryGetIconReturnsPixelsForFileAndFolder()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"glaze-icons-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var file = Path.Combine(directory, "sample.txt");
            File.WriteAllText(file, "x");

            var folderPixels = ShellIconProvider.TryGetIcon(directory, size: 48);
            var filePixels = ShellIconProvider.TryGetIcon(file, size: 48);

            Assert.IsNotNull(folderPixels);
            Assert.IsNotNull(filePixels);

            if (folderPixels is null || filePixels is null)
            {
                return;
            }

            Assert.AreNotEqual(0, folderPixels.Width);
            Assert.AreNotEqual(0, folderPixels.Height);
            Assert.HasCount(folderPixels.Width * folderPixels.Height * 4, folderPixels.Bgra);
            Assert.IsNotEmpty(filePixels.Bgra);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
