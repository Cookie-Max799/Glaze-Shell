using GlazeShell.Core.Configuration;
using GlazeShell.Data.Serialization;

namespace GlazeShell.Data.Tests;

[TestClass]
public sealed class ConfigurationDocumentMapperTests
{
    [TestMethod]
    public void MapsDefaultDocument()
    {
        var document = new ConfigurationDocument();

        Assert.IsTrue(ConfigurationDocumentMapper.TryMap(document, out var configuration, out var diagnostics));

        Assert.IsEmpty(diagnostics);
        Assert.IsNotNull(configuration);
        Assert.AreEqual(ConfigurationDocument.DefaultApplicationName, configuration.ApplicationName);
        Assert.AreEqual(ConfigurationDocument.DefaultDataDirectoryName, configuration.DataDirectoryName);
    }

    [TestMethod]
    public void RejectsMissingDocument()
    {
        Assert.IsFalse(ConfigurationDocumentMapper.TryMap(null, out var configuration, out var diagnostics));

        Assert.IsNull(configuration);
        StringAssert.Contains(diagnostics.Single(), "missing");
    }

    [TestMethod]
    public void RejectsInvalidApplicationName()
    {
        var document = new ConfigurationDocument { ApplicationName = "  " };

        Assert.IsFalse(ConfigurationDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "application name");
    }

    [TestMethod]
    public void RejectsInvalidDataDirectoryName()
    {
        var document = new ConfigurationDocument { DataDirectoryName = ".." };

        Assert.IsFalse(ConfigurationDocumentMapper.TryMap(document, out _, out var diagnostics));
        StringAssert.Contains(diagnostics.Single(), "data directory name");
    }

    [TestMethod]
    public void FromConfigurationMapsValues()
    {
        var configuration = new GlazeShellConfiguration
        {
            ApplicationName = "Custom",
            DataDirectoryName = "CustomData"
        };

        var document = ConfigurationDocument.FromConfiguration(configuration);

        Assert.AreEqual("Custom", document.ApplicationName);
        Assert.AreEqual("CustomData", document.DataDirectoryName);
    }

    [TestMethod]
    public void FromConfigurationRejectsNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ConfigurationDocument.FromConfiguration(null!));
    }
}
