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
    /// <param name="iconPath">
    /// Путь к файлу значка (исполняемый файл приложения или путь процесса окна) либо пустая
    /// строка, если значка нет. UI решает сам, показывать ли значок, по настройке.
    /// </param>
    public GlanceResult(
        GlanceResultKind kind,
        string id,
        string title,
        string subtitle = "",
        string keywords = "",
        string iconPath = "")
    {
        Kind = kind;
        Id = id ?? string.Empty;
        Title = title ?? string.Empty;
        Subtitle = subtitle ?? string.Empty;
        Keywords = keywords ?? string.Empty;
        IconPath = iconPath ?? string.Empty;
    }

    public GlanceResultKind Kind { get; }

    public string Id { get; }

    public string Title { get; }

    /// <summary>Пояснение под заголовком; никогда не <see langword="null"/>.</summary>
    public string Subtitle { get; }

    /// <summary>Слова-ключи, не влияющие на отображение, но участвующие в ранжировании.</summary>
    public string Keywords { get; }

    /// <summary>
    /// Путь к файлу значка результата (исполняемый файл или путь процесса окна); пустая
    /// строка, если значка нет — для настроек, команд и окон без процесса.
    /// </summary>
    public string IconPath { get; }
}
