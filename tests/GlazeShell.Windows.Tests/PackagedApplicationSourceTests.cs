using System.Runtime.Versioning;
using GlazeShell.Core.Models;
using GlazeShell.Windows.Shell;
using Windows.Management.Deployment;

namespace GlazeShell.Windows.Tests;

[TestClass]
public sealed class PackagedApplicationSourceTests
{
    [TestMethod]
    public void EveryCandidateCarriesApplicationUserModelId()
    {
        var source = new PackagedApplicationSource();
        var result = source.Discover();

        foreach (var candidate in result.Candidates)
        {
            Assert.AreEqual(ApplicationType.Msix, candidate.Type, candidate.Name);
            Assert.IsFalse(string.IsNullOrWhiteSpace(candidate.ApplicationUserModelId), candidate.Name);
            Assert.IsTrue(candidate.ApplicationUserModelId!.Contains('!', StringComparison.Ordinal), candidate.Name);
            Assert.IsFalse(string.IsNullOrWhiteSpace(candidate.Name), candidate.ApplicationUserModelId);
            Assert.IsTrue(candidate.HasLaunchIdentity, candidate.Name);
            Assert.IsNotNull(candidate.PackageFamilyName, candidate.Name);
            StringAssert.StartsWith(candidate.ApplicationUserModelId!, candidate.PackageFamilyName + "!", candidate.Name);
        }
    }

    [TestMethod]
    [SupportedOSPlatform("windows10.0.19041.0")]
    public void EnumeratesPackagedApplicationsOrReportsWhyItCannot()
    {
        var source = new PackagedApplicationSource();
        var result = source.Discover();

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            Assert.IsNotEmpty(result.Warnings, "An unsupported system must be explained by a warning.");
            return;
        }

        var packagesWithEntries = CountPackagesWithEntries();

        if (packagesWithEntries < 0)
        {
            Assert.Inconclusive("Package enumeration is unavailable in this environment.");
            return;
        }

        if (packagesWithEntries == 0)
        {
            Assert.IsEmpty(result.Candidates, string.Join("; ", result.Warnings));
            Assert.IsNotEmpty(
                result.Warnings,
                "An empty result must be explained by a warning so the failure is diagnosable.");
            return;
        }

        Assert.IsGreaterThanOrEqualTo(
            packagesWithEntries,
            result.Candidates.Count,
            "Every package with app list entries must produce at least one candidate.");
    }

    [TestMethod]
    public void HonoursCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var source = new PackagedApplicationSource();

        Assert.ThrowsExactly<OperationCanceledException>(() => source.Discover(cancellation.Token));
    }

    [SupportedOSPlatform("windows10.0.19041.0")]
    private static int CountPackagesWithEntries()
    {
        try
        {
            var packages = new PackageManager()
                .FindPackagesForUserWithPackageTypes(string.Empty, PackageTypes.Main);

            var count = 0;

            foreach (var package in packages)
            {
                try
                {
                    if (package.GetAppListEntries().Count > 0)
                    {
                        count++;
                    }
                }
                catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
                {
                    // Повреждённый пакет пропускается так же, как и в источнике.
                }
            }

            return count;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return -1;
        }
    }
}
