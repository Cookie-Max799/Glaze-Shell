using System.Globalization;
using GlazeShell.Core.Models;
using GlazeShell.Data.Serialization;

namespace GlazeShell.Data.Tests;

[TestClass]
public sealed class SettingsDocumentMapperTests
{
    [TestMethod]
    public void MapsDefaultDocument()
    {
        var document = new SettingsDocument();

        Assert.IsTrue(SettingsDocumentMapper.TryMap(document, out var settings, out var diagnostics));

        Assert.IsEmpty(diagnostics);
        Assert.IsNotNull(settings);
        Assert.AreEqual(SettingsDocument.DefaultLanguage, settings.Language);
        Assert.IsTrue(settings.ShowDesktopOnStartup);
        Assert.IsEmpty(settings.MonitorNames);
    }

    [TestMethod]
    public void RejectsMissingDocument()
    {
        Assert.IsFalse(SettingsDocumentMapper.TryMap(null, out var settings, out var diagnostics));

        Assert.IsNull(settings);
        StringAssert.Contains(diagnostics.Single(), "missing");
    }

    [TestMethod]
    public void DefaultsLanguageWhenNull()
    {
        var document = new SettingsDocument { Language = null };

        Assert.IsTrue(SettingsDocumentMapper.TryMap(document, out var settings, out _));
        Assert.AreEqual(SettingsDocument.DefaultLanguage, settings!.Language);
    }

    [TestMethod]
    public void RejectsInvalidLanguage()
    {
        var document = new SettingsDocument { Language = "  " };

        Assert.IsFalse(SettingsDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "language");
    }

    [TestMethod]
    public void RejectsInvalidThemeIdentifier()
    {
        var document = new SettingsDocument { ActiveThemeId = "bad\ntheme" };

        Assert.IsFalse(SettingsDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "theme identifier");
    }

    [TestMethod]
    public void RejectsTooManyMonitorNames()
    {
        var document = new SettingsDocument
        {
            MonitorNames = Enumerable.Range(0, PersistenceLimits.MaxMonitorNames + 1)
                .ToDictionary(static index => $"monitor-{index}", static index => $"Display {index}")
        };

        Assert.IsFalse(SettingsDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(
            diagnostics.Single(),
            PersistenceLimits.MaxMonitorNames.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void RejectsInvalidMonitorIdentifier()
    {
        var document = new SettingsDocument
        {
            MonitorNames = new Dictionary<string, string> { ["  "] = "Display" }
        };

        Assert.IsFalse(SettingsDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "monitor identifier");
    }

    [TestMethod]
    public void RejectsInvalidMonitorName()
    {
        var document = new SettingsDocument
        {
            MonitorNames = new Dictionary<string, string> { ["monitor-1"] = "\n" }
        };

        Assert.IsFalse(SettingsDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "monitor name");
    }

    [TestMethod]
    public void MapsMonitorNames()
    {
        var document = new SettingsDocument
        {
            MonitorNames = new Dictionary<string, string> { ["monitor-1"] = "Left", ["monitor-2"] = "Right" }
        };

        Assert.IsTrue(SettingsDocumentMapper.TryMap(document, out var settings, out _));

        Assert.HasCount(2, settings!.MonitorNames);
        Assert.AreEqual("Left", settings.MonitorNames["monitor-1"]);
    }

    [TestMethod]
    public void FromSettingsMapsValues()
    {
        var settings = new UserSettings(
            startWithWindows: true,
            minimizeToTray: true,
            language: "de-DE",
            activeThemeId: "midnight",
            monitorNames: new Dictionary<string, string> { ["monitor-1"] = "Left" });

        var document = SettingsDocument.FromSettings(settings);

        Assert.IsTrue(document.StartWithWindows);
        Assert.IsTrue(document.MinimizeToTray);
        Assert.AreEqual("de-DE", document.Language);
        Assert.AreEqual("midnight", document.ActiveThemeId);
        Assert.HasCount(1, document.MonitorNames!);
    }

    [TestMethod]
    public void FromSettingsRejectsNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => SettingsDocument.FromSettings(null!));
    }
}
