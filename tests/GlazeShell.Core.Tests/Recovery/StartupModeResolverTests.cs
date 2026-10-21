using GlazeShell.Core.Recovery;

namespace GlazeShell.Core.Tests.Recovery;

[TestClass]
public sealed class StartupModeResolverTests
{
    [TestMethod]
    public void WithoutReasonsTheShellStartsNormally()
    {
        var decision = StartupModeResolver.Resolve(
            requestedByCommandLine: false,
            enabledInSettings: false,
            repeatedCrashes: false);

        Assert.AreEqual(StartupMode.Normal, decision.Mode);
        Assert.IsFalse(decision.IsSafeMode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [TestMethod]
    public void CommandLineRequestEnablesSafeMode()
    {
        var decision = StartupModeResolver.Resolve(
            requestedByCommandLine: true,
            enabledInSettings: false,
            repeatedCrashes: false);

        Assert.AreEqual(StartupMode.SafeMode, decision.Mode);
        Assert.IsTrue(decision.IsSafeMode);
    }

    [TestMethod]
    public void SettingsEnableSafeMode()
    {
        var decision = StartupModeResolver.Resolve(
            requestedByCommandLine: false,
            enabledInSettings: true,
            repeatedCrashes: false);

        Assert.AreEqual(StartupMode.SafeMode, decision.Mode);
    }

    [TestMethod]
    public void RepeatedCrashesEnableSafeMode()
    {
        var decision = StartupModeResolver.Resolve(
            requestedByCommandLine: false,
            enabledInSettings: false,
            repeatedCrashes: true);

        Assert.AreEqual(StartupMode.SafeMode, decision.Mode);
        Assert.Contains("crashed", decision.Reason);
    }

    [TestMethod]
    public void EveryReasonIsExplained()
    {
        var decisions = new[]
        {
            StartupModeResolver.Resolve(true, false, false),
            StartupModeResolver.Resolve(false, true, false),
            StartupModeResolver.Resolve(false, false, true),
        };

        foreach (var decision in decisions)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(decision.Reason), "Every safe mode decision needs a reason.");
        }
    }
}
