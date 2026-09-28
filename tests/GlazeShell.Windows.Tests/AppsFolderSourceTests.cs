using GlazeShell.Core.Models;
using GlazeShell.Windows.Shell;

namespace GlazeShell.Windows.Tests;

[TestClass]
public sealed class AppsFolderSourceTests
{
    [TestMethod]
    public void EveryCandidateCarriesApplicationUserModelId()
    {
        var source = new AppsFolderSource();
        var result = source.Discover();

        foreach (var candidate in result.Candidates)
        {
            Assert.AreEqual(ApplicationType.Msix, candidate.Type, candidate.Name);
            Assert.IsFalse(string.IsNullOrWhiteSpace(candidate.ApplicationUserModelId), candidate.Name);
            Assert.IsTrue(candidate.ApplicationUserModelId!.Contains('!', StringComparison.Ordinal), candidate.Name);
            Assert.IsFalse(string.IsNullOrWhiteSpace(candidate.Name), candidate.ApplicationUserModelId);
            Assert.IsTrue(candidate.HasLaunchIdentity, candidate.Name);
        }
    }

    [TestMethod]
    public void EnumeratesTheAppsFolderOrReportsWhyItCannot()
    {
        var source = new AppsFolderSource();
        var result = source.Discover();

        if (result.Candidates.Count == 0)
        {
            Assert.IsNotEmpty(
                result.Warnings,
                "An empty result must be explained by a warning so the failure is diagnosable.");
            return;
        }

        var shellFolder = result.Candidates.FirstOrDefault(static candidate =>
            candidate.ApplicationUserModelId!.StartsWith("Microsoft.Windows.Shell_", StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(shellFolder, string.Join("; ", result.Warnings));
        Assert.AreEqual("Microsoft.Windows.Shell_8wekyb3d8bbwe!App", shellFolder!.ApplicationUserModelId);
    }

    [TestMethod]
    public void HonoursCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var source = new AppsFolderSource();

        Assert.ThrowsExactly<OperationCanceledException>(() => source.Discover(cancellation.Token));
    }
}
