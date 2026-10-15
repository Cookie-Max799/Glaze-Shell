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
    public void RedactsUserProfilePathWithForwardSlashes()
    {
        var result = LogRedactor.Redact("failed to open C:/Users/ivanov/Documents/config.json");

        Assert.DoesNotContain("ivanov", result, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void RedactsUserProfilePathInJsonEscapedForm()
    {
        var result = LogRedactor.Redact(@"{""path"":""C:\\Users\\ivanov\\cfg.json""}");

        Assert.DoesNotContain("ivanov", result, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void RedactsWholeUnquotedSecretValue()
    {
        var result = LogRedactor.Redact("connection password=my secret word");

        Assert.AreEqual("connection password=[REDACTED]", result);
    }

    [TestMethod]
    public void RedactsExtendedCredentialKeys()
    {
        (string Sample, string Value)[] samples =
        [
            ("private_key=AAAA", "AAAA"),
            ("session_token: BBBB", "BBBB"),
            ("passphrase=CCCC", "CCCC"),
            ("cookie: DDDD", "DDDD"),
            ("set-cookie: EEEE", "EEEE"),
            ("access_key=FFFF", "FFFF")
        ];

        foreach (var (sample, value) in samples)
        {
            var result = LogRedactor.Redact(sample);

            Assert.Contains(LogRedactor.Placeholder, result, StringComparison.Ordinal);
            Assert.DoesNotContain(value, result, StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public void RedactsBearerTokenWithSymbolTail()
    {
        var result = LogRedactor.Redact("Authorization: Bearer Abc!123#xyz");

        Assert.DoesNotContain("Abc!123#xyz", result, StringComparison.Ordinal);
        Assert.Contains(LogRedactor.Placeholder, result, StringComparison.Ordinal);
    }

    [TestMethod]
    public void RedactionIsIdempotent()
    {
        var once = LogRedactor.Redact("authorization=Bearer abc.def");

        Assert.AreEqual(once, LogRedactor.Redact(once));
    }

    [TestMethod]
    public void TruncationDoesNotSplitSurrogatePair()
    {
        const string marker = "…[TRUNCATED]";
        var emoji = char.ConvertFromUtf32(0x1F600);
        var text = new string('a', LogRedactor.MaxTextLength - 1) + emoji + new string('b', 16);

        var result = LogRedactor.Redact(text);

        Assert.Contains(marker, result, StringComparison.Ordinal);
        Assert.DoesNotContain(emoji, result, StringComparison.Ordinal);

        var body = result[..(result.Length - marker.Length)];
        Assert.IsFalse(
            char.IsHighSurrogate(body[^1]),
            "Truncation must not leave a lone high surrogate at the cut position.");
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
