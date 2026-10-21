using GlazeShell.Core.Models;
using GlazeShell.Core.Services;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class AccessibilityResolverTests
{
    [TestMethod]
    public void DefaultStateKeepsMotionAndContrast()
    {
        var state = AccessibilityResolver.Resolve(UserSettings.CreateDefault(), systemHighContrast: false, systemAnimationsEnabled: true);

        Assert.IsFalse(state.ReduceMotion);
        Assert.IsFalse(state.HighContrast);
    }

    [TestMethod]
    public void UserPreferenceEnablesReducedMotion()
    {
        var settings = new UserSettings(reduceMotion: true);

        var state = AccessibilityResolver.Resolve(settings, systemHighContrast: false, systemAnimationsEnabled: true);

        Assert.IsTrue(state.ReduceMotion);
    }

    [TestMethod]
    public void UserPreferenceEnablesHighContrast()
    {
        var settings = new UserSettings(highContrast: true);

        var state = AccessibilityResolver.Resolve(settings, systemHighContrast: false, systemAnimationsEnabled: true);

        Assert.IsTrue(state.HighContrast);
    }

    [TestMethod]
    public void DisabledSystemAnimationsForceReducedMotion()
    {
        var state = AccessibilityResolver.Resolve(UserSettings.CreateDefault(), systemHighContrast: false, systemAnimationsEnabled: false);

        Assert.IsTrue(state.ReduceMotion, "The system switch must win over the user preference.");
    }

    [TestMethod]
    public void SystemHighContrastForcesContrastMode()
    {
        var state = AccessibilityResolver.Resolve(UserSettings.CreateDefault(), systemHighContrast: true, systemAnimationsEnabled: true);

        Assert.IsTrue(state.HighContrast, "The system theme must win over the user preference.");
    }

    [TestMethod]
    public void SystemRestrictionsAreNeverCancelledByTheUser()
    {
        var settings = new UserSettings(reduceMotion: false, highContrast: false);

        var state = AccessibilityResolver.Resolve(settings, systemHighContrast: true, systemAnimationsEnabled: false);

        Assert.IsTrue(state.ReduceMotion);
        Assert.IsTrue(state.HighContrast);
    }

    [TestMethod]
    public void DefaultStateMatchesTheNeutralSnapshot()
    {
        Assert.AreEqual(AccessibilityState.Default, new AccessibilityState(false, false));
    }

    [TestMethod]
    public void RejectsMissingSettings()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            AccessibilityResolver.Resolve(null!, systemHighContrast: false, systemAnimationsEnabled: true));
    }
}
