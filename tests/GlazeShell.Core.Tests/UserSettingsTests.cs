using GlazeShell.Core.Models;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class UserSettingsTests
{
    [TestMethod]
    public void DefaultSettingsHaveNoMonitorNames()
    {
        var settings = UserSettings.CreateDefault();

        Assert.IsEmpty(settings.MonitorNames);
    }

    [TestMethod]
    public void MonitorNamesAreTrimmedAndMergedCaseInsensitively()
    {
        var settings = new UserSettings(monitorNames: new Dictionary<string, string>
        {
            ["  MON-1  "] = "  Left  ",
            ["mon-1"] = "Right",
            ["MON-2"] = "   ",
            ["   "] = "Ignored"
        });

        Assert.HasCount(1, settings.MonitorNames);
        Assert.AreEqual("Right", settings.MonitorNames["MON-1"]);
    }

    [TestMethod]
    public void EmptyMonitorNamesNormalizeToEmptyMap()
    {
        var fromNull = new UserSettings(monitorNames: null);
        var fromEmpty = new UserSettings(monitorNames: new Dictionary<string, string>());
        var fromBlank = new UserSettings(monitorNames: new Dictionary<string, string> { [" "] = " " });

        Assert.IsEmpty(fromNull.MonitorNames);
        Assert.IsEmpty(fromEmpty.MonitorNames);
        Assert.IsEmpty(fromBlank.MonitorNames);
    }
}
