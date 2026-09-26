using System.Text.RegularExpressions;

namespace GlazeShell.Infrastructure.Logging;

public static partial class LogRedactor
{
    public const string Placeholder = "[REDACTED]";
    public const int MaxTextLength = 4096;

    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var result = BearerTokenPattern().Replace(text, Placeholder);
        result = SecretAssignmentPattern().Replace(
            result,
            static match => $"{match.Groups["key"].Value}{match.Groups["separator"].Value}{Placeholder}");
        result = UserProfilePathPattern().Replace(result, @"\Users\[REDACTED]");
        return Truncate(result);
    }

    private static string Truncate(string value) =>
        value.Length <= MaxTextLength ? value : value[..MaxTextLength] + "…[TRUNCATED]";

    [GeneratedRegex(@"\bBearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenPattern();

    [GeneratedRegex(
        @"(?<key>\b(access_token|refresh_token|client_secret|api_key|apikey|authorization|password|passwd|pwd|secret|token)\b)(?<separator>[""']?\s*[:=]\s*)(""[^""]*""|'[^']*'|[^\s,;}\]]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignmentPattern();

    [GeneratedRegex(@"\\Users\\[^\\/\:*?""<>|\r\n]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UserProfilePathPattern();
}
