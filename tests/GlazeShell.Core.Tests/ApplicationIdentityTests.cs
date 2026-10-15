using GlazeShell.Core.Discovery;
using GlazeShell.Core.Models;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class ApplicationIdentityTests
{
    [TestMethod]
    public void GetLaunchKeyPrefersApplicationUserModelId()
    {
        var candidate = Candidate(aumid: "MyApp_123!App", executablePath: @"C:\Apps\MyApp.exe");

        Assert.AreEqual("aumid:myapp_123!app", ApplicationIdentity.GetLaunchKey(candidate));
    }

    [TestMethod]
    public void GetLaunchKeyUsesNormalizedPathWhenNoPackage()
    {
        var candidate = Candidate(executablePath: @"  C:\Apps\MyApp.exe  ");

        Assert.AreEqual(@"path:c:\apps\myapp.exe", ApplicationIdentity.GetLaunchKey(candidate));
    }

    [TestMethod]
    public void GetLaunchKeyUsesPackageFamilyNameAsLastResort()
    {
        var candidate = Candidate(packageFamilyName: "Contoso.App_123");

        Assert.AreEqual("pfn:contoso.app_123", ApplicationIdentity.GetLaunchKey(candidate));
    }

    [TestMethod]
    public void GetLaunchKeyReturnsNullWithoutLaunchIdentity()
    {
        Assert.IsNull(ApplicationIdentity.GetLaunchKey(Candidate()));
    }

    [TestMethod]
    public void GetLaunchKeyRejectsNullCandidate()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ApplicationIdentity.GetLaunchKey(null!));
    }

    [TestMethod]
    public void TryCreateIdUsesPrefixPerIdentity()
    {
        Assert.IsTrue(ApplicationIdentity.TryCreateId(Candidate(aumid: "MyApp_123!App"), out var msixId));
        Assert.AreEqual("msix:myapp_123!app", msixId);

        Assert.IsTrue(ApplicationIdentity.TryCreateId(Candidate(packageFamilyName: "Contoso.App_123"), out var packageId));
        Assert.AreEqual("pkg:contoso.app_123", packageId);

        Assert.IsTrue(ApplicationIdentity.TryCreateId(Candidate(executablePath: @"C:\Apps\MyApp.exe"), out var pathId));
        StringAssert.StartsWith(pathId, ApplicationIdentity.ExecutableIdPrefix);
    }

    [TestMethod]
    public void TryCreateIdReturnsFalseWithoutLaunchIdentity()
    {
        Assert.IsFalse(ApplicationIdentity.TryCreateId(Candidate(), out var id));
        Assert.AreEqual(string.Empty, id);
    }

    [TestMethod]
    public void PathIdentifierIgnoresCase()
    {
        ApplicationIdentity.TryCreateId(Candidate(executablePath: @"C:\Apps\MyApp.exe"), out var first);
        ApplicationIdentity.TryCreateId(Candidate(executablePath: @"c:\apps\myapp.exe"), out var second);

        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void CreateIdThrowsWithoutLaunchIdentity()
    {
        Assert.ThrowsExactly<ArgumentException>(() => ApplicationIdentity.CreateId(Candidate()));
    }

    [TestMethod]
    public void ToApplicationReturnsNullWithoutLaunchIdentity()
    {
        Assert.IsNull(ApplicationIdentity.ToApplication(Candidate()));
    }

    [TestMethod]
    public void ToApplicationMapsEveryField()
    {
        var candidate = Candidate(
            name: "Contoso",
            packageFamilyName: "Contoso.App_123",
            aumid: "Contoso.App_123!App",
            iconPath: @"C:\Apps\icon.png",
            description: "Example",
            publisher: "Contoso",
            version: "1.2.3",
            isSystemApplication: true,
            arguments: "--fast",
            workingDirectory: @"C:\Apps");

        var application = ApplicationIdentity.ToApplication(candidate);

        Assert.IsNotNull(application);
        Assert.AreEqual("msix:contoso.app_123!app", application.Id);
        Assert.AreEqual("Contoso", application.Name);
        Assert.AreEqual("Contoso.App_123", application.PackageFamilyName);
        Assert.AreEqual("Contoso.App_123!App", application.ApplicationUserModelId);
        Assert.AreEqual(@"C:\Apps\icon.png", application.IconPath);
        Assert.AreEqual("Example", application.Description);
        Assert.AreEqual("Contoso", application.Publisher);
        Assert.AreEqual("1.2.3", application.Version);
        Assert.IsTrue(application.IsSystemApplication);
        Assert.AreEqual("--fast", application.Arguments);
        Assert.AreEqual(@"C:\Apps", application.WorkingDirectory);
    }

    [TestMethod]
    public void ToApplicationRejectsNullCandidate()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ApplicationIdentity.ToApplication(null!));
    }

    [TestMethod]
    public void NormalizePathTrimsAndLowercases()
    {
        Assert.AreEqual(@"c:\apps\tool.exe", ApplicationIdentity.NormalizePath(@"  C:\Apps\Tool.exe "));
    }

    [TestMethod]
    public void NormalizePathRejectsBlankInput()
    {
        Assert.ThrowsExactly<ArgumentException>(() => ApplicationIdentity.NormalizePath("   "));
    }

    private static ApplicationCandidate Candidate(
        string name = "Example",
        string? executablePath = null,
        string? packageFamilyName = null,
        string? aumid = null,
        string? iconPath = null,
        string? description = null,
        string? publisher = null,
        string? version = null,
        bool isSystemApplication = false,
        string? arguments = null,
        string? workingDirectory = null,
        ApplicationType type = ApplicationType.Win32) =>
        new(
            name,
            "test-source",
            type,
            executablePath,
            packageFamilyName,
            aumid,
            iconPath,
            description,
            publisher,
            version,
            isSystemApplication,
            arguments,
            workingDirectory);
}
