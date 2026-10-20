namespace GlazeShell.Core.Glance;

/// <summary>
/// Ранжирование результатов Glance (B1). Сначала кандидаты фильтруются по совпадению всех
/// слов запроса, затем сортируются по качеству совпадения: точное имя важнее префикса,
/// префикс — совпадения с началом слова, оно — совпадения где угодно, а совпадение только
/// в пояснении или ключевых словах — слабее всего. Никогда не выполняет код и не делает
/// сетевых запросов: результат детерминирован и полностью офлайн.
/// </summary>
public static class GlanceRanker
{
    /// <summary>Сколько результатов возвращается по умолчанию.</summary>
    public const int DefaultLimit = 8;

    /// <summary>
    /// Фильтрует и сортирует кандидатов. Пустой запрос даёт пустой список: Glance не
    /// показывает «всё подряд», пока пользователь ничего не ввёл.
    /// </summary>
    public static IReadOnlyList<GlanceResult> Rank(
        IEnumerable<GlanceResult> candidates,
        string? query,
        int limit = DefaultLimit)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (limit <= 0)
        {
            return [];
        }

        var terms = Split(query);

        if (terms.Length == 0)
        {
            return [];
        }

        var scored = new List<(GlanceResult Result, int Score)>();

        foreach (var candidate in candidates)
        {
            var score = Score(candidate, terms);

            if (score > 0)
            {
                scored.Add((candidate, score));
            }
        }

        return scored
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Result.Title, StringComparer.CurrentCultureIgnoreCase)
            .Take(limit)
            .Select(entry => entry.Result)
            .ToArray();
    }

    /// <summary>Оценка кандидата по всем словам запроса; 0 означает «не подходит».</summary>
    internal static int Score(GlanceResult candidate, IReadOnlyList<string> terms)
    {
        var total = 0;

        foreach (var term in terms)
        {
            var value = ScoreTerm(candidate, term);

            if (value == 0)
            {
                return 0;
            }

            total += value;
        }

        return total;
    }

    private static int ScoreTerm(GlanceResult candidate, string term)
    {
        var title = candidate.Title;

        if (title.Equals(term, StringComparison.OrdinalIgnoreCase))
        {
            return 1000;
        }

        if (title.StartsWith(term, StringComparison.OrdinalIgnoreCase))
        {
            return 800;
        }

        if (StartsWithWord(title, term))
        {
            return 600;
        }

        if (title.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return 400;
        }

        if (candidate.Subtitle.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return 250;
        }

        return candidate.Keywords.Contains(term, StringComparison.OrdinalIgnoreCase) ? 150 : 0;
    }

    private static bool StartsWithWord(string text, string term)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (index > 0 && char.IsLetterOrDigit(text[index - 1]))
            {
                continue;
            }

            if (index + term.Length <= text.Length &&
                string.Compare(text, index, term, 0, term.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string[] Split(string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? []
            : query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
