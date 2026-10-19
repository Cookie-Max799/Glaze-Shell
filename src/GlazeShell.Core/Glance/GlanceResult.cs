namespace GlazeShell.Core.Glance;

/// <summary>
/// Результат глобального поиска Glance (B1): то, что показывается строкой в списке и может
/// быть открыто. Модель framework-free — UI добавляет значок и оформление, а действие
/// выбирается по <see cref="Kind"/> и <see cref="Id"/>.
/// </summary>
public sealed record GlanceResult
{
    /// <param name="kind">Категория результата.</param>
    /// <param name="id">
    /// Идентификатор открытия: id приложения, id окна, URI настроек (<c>ms-settings:</c>) или
    /// имя команды. Формат зависит от <paramref name="kind"/>.
    /// </param>
    /// <param name="title">Основная строка — имя, которое ищет пользователь.</param>
    /// <param name="subtitle">Пояснение под заголовком (издатель, заголовок окна, категория).</param>
    /// <param name="keywords">Дополнительные слова для поиска, не показываемые в списке.</param>
    public GlanceResult(
        GlanceResultKind kind,
        string id,
        string title,
        string subtitle = "",
        string keywords = "")
    {
        Kind = kind;
        Id = id ?? string.Empty;
        Title = title ?? string.Empty;
        Subtitle = subtitle ?? string.Empty;
        Keywords = keywords ?? string.Empty;
    }

    public GlanceResultKind Kind { get; }

    public string Id { get; }

    public string Title { get; }

    /// <summary>Пояснение под заголовком; никогда не <see langword="null"/>.</summary>
    public string Subtitle { get; }

    /// <summary>Слова-ключи, не влияющие на отображение, но участвующие в ранжировании.</summary>
    public string Keywords { get; }
}
