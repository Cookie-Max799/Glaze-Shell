using GlazeShell.Core.Models;

namespace GlazeShell.Core.Settings;

/// <summary>
/// Описание одной настройки для страницы настроек Glaze (I1). Настройка не хранит значение:
/// она лишь описывает, где и как оно показывается, и умеет прочитать его из
/// <see cref="UserSettings"/> и записать в новую копию. Так каталог остаётся декларативным
/// и тестируемым: описание не знает про WinUI, а применяется через <see cref="ISettingsManager"/>.
/// </summary>
/// <param name="Id">Устойчивый идентификатор настройки (латиницей, в стиле остальных ключей).</param>
/// <param name="PageId">Идентификатор страницы, на которой показывается настройка.</param>
/// <param name="Title">Отображаемое имя.</param>
/// <param name="Description">Пояснение под именем.</param>
/// <param name="Keywords">Дополнительные слова для поиска (синонимы, английские названия).</param>
/// <param name="Kind">Вид контрола.</param>
/// <param name="Read">Читает текущее значение из настроек.</param>
/// <param name="Write">Возвращает копию настроек с новым значением.</param>
/// <param name="Choices">Варианты для <see cref="SettingKind.Choice"/>, иначе пусто.</param>
public sealed record SettingDefinition(
    string Id,
    string PageId,
    string Title,
    string Description,
    IReadOnlyList<string> Keywords,
    SettingKind Kind,
    Func<UserSettings, object?> Read,
    Func<UserSettings, object, UserSettings> Write,
    IReadOnlyList<SettingChoice> Choices)
{
    /// <summary>
    /// Совпадает ли настройка с поисковым запросом. Совпадение ищется по имени, пояснению,
    /// идентификатору и ключевым словам, без учёта регистра; пустой запрос совпадает со всем.
    /// </summary>
    public bool Matches(string? query)
    {
        var trimmed = query?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return true;
        }

        if (Contains(Title, trimmed) || Contains(Description, trimmed) || Contains(Id, trimmed))
        {
            return true;
        }

        foreach (var keyword in Keywords)
        {
            if (Contains(keyword, trimmed))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
