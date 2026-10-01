using GlazeShell.Core.Discovery;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class PackageIdentityTests
{
    [TestMethod]
    public void DerivesFamilyNameFromPackageFullName()
    {
        Assert.IsTrue(PackageIdentity.TryGetPackageFamilyName(
            "Microsoft.BingWeather_4.54.63045.0_x64__8wekyb3d8bbwe",
            out var familyName));

        Assert.AreEqual("Microsoft.BingWeather_8wekyb3d8bbwe", familyName);
    }

    [TestMethod]
    public void DerivesFamilyNameFromNeutralBundleFullName()
    {
        Assert.IsTrue(PackageIdentity.TryGetPackageFamilyName(
            "Microsoft.BingWeather_4.54.63045.0_neutral_~_8wekyb3d8bbwe",
            out var familyName));

        Assert.AreEqual("Microsoft.BingWeather_8wekyb3d8bbwe", familyName);
    }

    [TestMethod]
    public void RejectsFamilyNameWithoutPublisher()
    {
        Assert.IsFalse(PackageIdentity.TryGetPackageFamilyName("Microsoft.BingWeather", out var familyName));
        Assert.AreEqual(string.Empty, familyName);
    }

    [TestMethod]
    public void ExtractsFamilyNameFromProcessPath()
    {
        Assert.IsTrue(PackageIdentity.TryGetPackageFamilyNameFromPath(
            @"C:\Program Files\WindowsApps\Clipchamp.Clipchamp_3.1.0.0_x64__yxz26nhyzhsrt\Clipchamp.exe",
            out var familyName));

        Assert.AreEqual("Clipchamp.Clipchamp_yxz26nhyzhsrt", familyName);
    }

    [TestMethod]
    public void RejectsProcessPathOutsideWindowsApps()
    {
        Assert.IsFalse(PackageIdentity.TryGetPackageFamilyNameFromPath(
            @"C:\Program Files\Contoso\app.exe",
            out _));
    }

    [TestMethod]
    public void MatchesProcessPathAgainstPackageFamilyName()
    {
        const string path = @"C:\Program Files\WindowsApps\Microsoft.WindowsCalculator_11.0.0.0_x64__8wekyb3d8bbwe\Calculator.exe";

        Assert.IsTrue(PackageIdentity.IsPathOfPackage(path, "Microsoft.WindowsCalculator_8wekyb3d8bbwe"));
        Assert.IsTrue(PackageIdentity.IsPathOfPackage(path, "microsoft.windowscalculator_8wekyb3d8bbwe"));
    }

    [TestMethod]
    public void DoesNotMatchSimilarPackageName()
    {
        const string path = @"C:\Program Files\WindowsApps\Microsoft.Widgets_1.0.0.0_x64__8wekyb3d8bbwe\Widgets.exe";

        Assert.IsFalse(PackageIdentity.IsPathOfPackage(path, "Microsoft.Widget_8wekyb3d8bbwe"));
    }

    [TestMethod]
    public void DoesNotMatchMissingPath()
    {
        Assert.IsFalse(PackageIdentity.IsPathOfPackage(null, "Microsoft.WindowsCalculator_8wekyb3d8bbwe"));
    }
}
