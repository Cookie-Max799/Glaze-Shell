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
        result = UserProfilePathPattern().Replace(
            result,
            static match => $"{match.Groups["drive"].Value}{match.Groups["before"].Value}Users{match.Groups["after"].Value}{Placeholder}");
        return Truncate(result);
    }

    private static string Truncate(string value)
    {
        if (value.Length <= MaxTextLength)
        {
            return value;
        }

        // Обрезка ровно на границе суррогатной пары оставила бы в конце текста
        // одинокий high surrogate, который ломает вывод строки.
        var end = char.IsHighSurrogate(value[MaxTextLength - 1]) ? MaxTextLength - 1 : MaxTextLength;
        return value[..end] + "…[TRUNCATED]";
    }

    [GeneratedRegex(@"\bBearer\s+[^\s]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenPattern();

    [GeneratedRegex(
        @"(?<key>\b(access_token|refresh_token|id_token|client_secret|client_assertion|api_key|apikey|x-api-key|secret_key|private_key|access_key|secretaccesskey|session_token|authorization|password|passwd|pwd|passphrase|secret|token|cookie|set-cookie|credentials)\b)(?<separator>[""']?\s*[:=]\s*)(""[^""]*""|'[^']*'|\[[^\]\r\n]*\]|[^\r\n,;}\[\]]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignmentPattern();

    [GeneratedRegex(
        @"(?<drive>[A-Za-z]:)?(?<before>[\\/]+)Users(?<after>[\\/]+)(?<user>[^\\/:*?""<>|\r\n]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UserProfilePathPattern();
}
