using GlazeShell.Core.Recovery;

namespace GlazeShell.Core.Tests.Recovery;

[TestClass]
public sealed class RecoveryCommandLineTests
{
    [TestMethod]
    public void EmptyArgumentsGiveEmptyOptions()
    {
        var options = RecoveryCommandLine.Parse([]);

        Assert.IsFalse(options.SafeMode);
        Assert.IsFalse(options.RestoreUserInterface);
        Assert.IsFalse(options.Help);
        Assert.IsNull(options.WatchdogProcessId);
        Assert.IsEmpty(options.UnknownArguments);
    }

    [TestMethod]
    public void ParsesSafeModeFlag()
    {
        var options = RecoveryCommandLine.Parse(["--safe-mode"]);

        Assert.IsTrue(options.SafeMode);
    }

    [TestMethod]
    public void ParsesRestoreFlag()
    {
        var options = RecoveryCommandLine.Parse(["--restore-ui"]);

        Assert.IsTrue(options.RestoreUserInterface);
    }

    [TestMethod]
    public void ParsesHelpFlag()
    {
        var options = RecoveryCommandLine.Parse(["--help"]);

        Assert.IsTrue(options.Help);
    }

    [TestMethod]
    public void ParsesWatchdogWithProcessId()
    {
        var options = RecoveryCommandLine.Parse(["--watchdog", "4321"]);

        Assert.AreEqual(4321, options.WatchdogProcessId);
        Assert.IsEmpty(options.UnknownArguments);
    }

    [TestMethod]
    public void ParsesWatchdogWithInlineProcessId()
    {
        var options = RecoveryCommandLine.Parse(["--watchdog=4321"]);

        Assert.AreEqual(4321, options.WatchdogProcessId);
    }

    [TestMethod]
    public void WatchdogWithoutProcessIdIsNotAnUnknownArgument()
    {
        var options = RecoveryCommandLine.Parse(["--watchdog"]);

        Assert.IsNull(options.WatchdogProcessId);
        Assert.IsEmpty(options.UnknownArguments);
    }

    [TestMethod]
    public void UnknownArgumentsAreCollected()
    {
        var options = RecoveryCommandLine.Parse(["--safe-mode", "--что-то", "--another"]);

        Assert.IsTrue(options.SafeMode);
        Assert.HasCount(2, options.UnknownArguments);
    }

    [TestMethod]
    public void FlagsAreRecognizedIgnoringCase()
    {
        var options = RecoveryCommandLine.Parse(["--SAFE-MODE"]);

        Assert.IsTrue(options.SafeMode);
    }

    [TestMethod]
    public void HelpTextMentionsEveryKey()
    {
        var text = RecoveryCommandLine.HelpText;

        Assert.Contains(RecoveryCommandLine.SafeModeArgument, text);
        Assert.Contains(RecoveryCommandLine.RestoreArgument, text);
        Assert.Contains(RecoveryCommandLine.WatchdogArgument, text);
    }
}
