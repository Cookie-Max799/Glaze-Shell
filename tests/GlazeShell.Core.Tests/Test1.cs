using GlazeShell.Core.Configuration;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class FoundationTests
{
    [TestMethod]
    public void DefaultConfigurationUsesCurrentSchemaVersion()
    {
        var configuration = GlazeShellConfiguration.CreateDefault();

        Assert.AreEqual(GlazeShellConfiguration.CurrentSchemaVersion, configuration.SchemaVersion);
        Assert.AreEqual("Glaze Shell", configuration.ApplicationName);
    }
}
