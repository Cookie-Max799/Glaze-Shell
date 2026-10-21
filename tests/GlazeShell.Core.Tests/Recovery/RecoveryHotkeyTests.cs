using GlazeShell.Core.Recovery;

namespace GlazeShell.Core.Tests.Recovery;

[TestClass]
public sealed class RecoveryHotkeyTests
{
    [TestMethod]
    public void DefaultCombinationIsRare()
    {
        Assert.AreEqual("Ctrl+Alt+Shift+F12", RecoveryHotkey.Default.Id);
        Assert.IsGreaterThanOrEqualTo(
            RecoveryHotkey.CountModifiers(RecoveryHotkey.Default.Modifiers),
            RecoveryHotkey.MinimumModifierCount,
            "The recovery hotkey must be hard to press by accident.");
    }

    [TestMethod]
    public void ParsesKnownCombination()
    {
        Assert.IsTrue(RecoveryHotkey.TryParse("Ctrl+Alt+Shift+F12", out var hotkey));
        Assert.IsNotNull(hotkey);
        Assert.AreEqual(RecoveryHotkey.Default.Id, hotkey!.Id);
    }

    [TestMethod]
    public void ParsesIgnoringCaseAndOrder()
    {
        Assert.IsTrue(RecoveryHotkey.TryParse("shift+ctrl+alt+f11", out var hotkey));
        Assert.IsNotNull(hotkey);
        Assert.AreEqual("Ctrl+Alt+Shift+F11", hotkey!.Id);
    }

    [TestMethod]
    public void RejectsCombinationsWithoutEnoughModifiers()
    {
        Assert.IsFalse(RecoveryHotkey.TryParse("Ctrl+F12", out _));
        Assert.IsFalse(RecoveryHotkey.TryParse("F12", out _));
    }

    [TestMethod]
    public void RejectsUnsupportedKeys()
    {
        Assert.IsFalse(RecoveryHotkey.TryParse("Ctrl+Alt+Shift+A", out _));
        Assert.IsFalse(RecoveryHotkey.TryParse("Ctrl+Alt+Shift+F13", out _));
    }

    [TestMethod]
    public void RejectsEmptyText()
    {
        Assert.IsFalse(RecoveryHotkey.TryParse(null, out _));
        Assert.IsFalse(RecoveryHotkey.TryParse("   ", out _));
    }

    [TestMethod]
    public void ParseOrDefaultFallsBackToTheDefaultCombination()
    {
        Assert.AreEqual(RecoveryHotkey.Default.Id, RecoveryHotkey.ParseOrDefault("Ctrl+K").Id);
        Assert.AreEqual(RecoveryHotkey.Default.Id, RecoveryHotkey.ParseOrDefault(null).Id);
    }

    [TestMethod]
    public void ConstructorRejectsFewModifiers()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => new RecoveryHotkey(HotkeyModifiers.Control, RecoveryHotkey.VirtualKeyF12));
    }

    [TestMethod]
    public void ConstructorRejectsUnsupportedKey()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new RecoveryHotkey(
                HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift,
                (uint)'K'));
    }

    [TestMethod]
    public void PresetsAreDistinctAndRare()
    {
        var ids = RecoveryHotkey.Presets.Select(hotkey => hotkey.Id).ToArray();

        Assert.HasCount(ids.Length, ids.Distinct(StringComparer.Ordinal).ToArray());
        Assert.IsTrue(
            RecoveryHotkey.Presets.All(hotkey =>
                RecoveryHotkey.CountModifiers(hotkey.Modifiers) >= RecoveryHotkey.MinimumModifierCount));
    }
}
