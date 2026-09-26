using GlazeShell.Infrastructure.Logging;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class FileLoggerTests
{
    private string _directory = string.Empty;
    private string _logFilePath = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), "GlazeShell.Tests", Guid.NewGuid().ToString("N"));
        _logFilePath = Path.Combine(_directory, "glaze-shell.log");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public void WritesLogEntry()
    {
        var logger = new FileLogger(_logFilePath);

        logger.Write(GlazeLogLevel.Information, "App", "Application launched.");

        Assert.IsTrue(File.Exists(_logFilePath));
        StringAssert.Contains(File.ReadAllText(_logFilePath), "Application launched.");
    }

    [TestMethod]
    public void RedactsSecretInPersistedEntry()
    {
        var logger = new FileLogger(_logFilePath);

        logger.Write(GlazeLogLevel.Warning, "App", "startup failed: password=hunter2");

        var content = File.ReadAllText(_logFilePath);
        Assert.DoesNotContain("hunter2", content, StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(content, "[REDACTED]");
    }

    [TestMethod]
    public void RollsFileWhenSizeLimitIsExceeded()
    {
        var logger = new FileLogger(_logFilePath, maxFileSizeBytes: 512, maxFileCount: 2);

        for (var index = 0; index < 40; index++)
        {
            logger.Write(GlazeLogLevel.Information, "App", $"Entry {index} {new string('x', 40)}");
        }

        Assert.IsTrue(File.Exists(_logFilePath + ".1"), "An archived log file is expected.");
        Assert.IsLessThanOrEqualTo(
            512,
            new FileInfo(_logFilePath).Length,
            "The active log file must not exceed the configured limit.");
    }

    [TestMethod]
    public void RetainsOnlyConfiguredNumberOfFiles()
    {
        var logger = new FileLogger(_logFilePath, maxFileSizeBytes: 256, maxFileCount: 2);

        for (var index = 0; index < 200; index++)
        {
            logger.Write(GlazeLogLevel.Information, "App", $"Entry {index} {new string('x', 60)}");
        }

        Assert.IsFalse(
            File.Exists(_logFilePath + ".2"),
            "Older archives beyond the retention limit must be deleted.");
    }

    [TestMethod]
    public void RejectsInvalidRetentionSettings()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new FileLogger(_logFilePath, maxFileCount: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new FileLogger(_logFilePath, maxFileSizeBytes: 0));
    }
}
