using GlazeShell.Infrastructure.Logging;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class LogRedactorTests
{
    [TestMethod]
    public void RedactsPasswordAssignment()
    {
        var result = LogRedactor.Redact("connecting with password=hunter2 done");

        Assert.Contains("password=[REDACTED]", result, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", result, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void RedactsJsonStyleSecretAssignment()
    {
        var result = LogRedactor.Redact("{\"api_key\": \"abc123\"}");

        Assert.DoesNotContain("abc123", result, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void RedactsBearerToken()
    {
        var result = LogRedactor.Redact("Authorization: Bearer eyJhbGciOi.J9.payload");

        Assert.DoesNotContain("eyJhbGciOi.J9.payload", result, StringComparison.Ordinal);
        Assert.Contains(LogRedactor.Placeholder, result, StringComparison.Ordinal);
    }

    [TestMethod]
    public void RedactsUserProfilePath()
    {
        var result = LogRedactor.Redact(@"failed to open C:\Users\ivanov\Documents\config.json");

        Assert.DoesNotContain("ivanov", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", result, StringComparison.Ordinal);
    }

    [TestMethod]
    public void RedactsSecretInsideExceptionText()
    {
        var result = LogRedactor.Redact(new InvalidOperationException("token=zzz-secret").ToString());

        Assert.DoesNotContain("zzz-secret", result, StringComparison.Ordinal);
    }

    [TestMethod]
    public void KeepsOrdinaryMessageIntact()
    {
        const string message = "Application launched.";

        Assert.AreEqual(message, LogRedactor.Redact(message));
    }

    [TestMethod]
    public void TruncatesOversizedText()
    {
        var result = LogRedactor.Redact(new string('a', LogRedactor.MaxTextLength * 2));

        Assert.IsLessThan(
            LogRedactor.MaxTextLength * 2,
            result.Length,
            "Redacted text should be truncated.");
        Assert.Contains("[TRUNCATED]", result, StringComparison.Ordinal);
    }
}
