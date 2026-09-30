namespace GlazeShell.Data.Serialization;

/// <summary>
/// Границы, за которые данные не выходят. Все значения заданы один раз и применяются
/// и при чтении, и при записи, чтобы повреждённый или подменённый документ
/// не мог исчерпать память, диск или время разбора.
/// </summary>
public static class PersistenceLimits
{
    /// <summary>
    /// Максимальный размер читаемого JSON-документа. Файлы больше лимита изолируются без разбора.
    /// </summary>
    public const long MaxDocumentBytes = 4L * 1024 * 1024;

    /// <summary>
    /// Максимальная глубина вложенности JSON. Защита от исчерпания стека на глубоко вложенных объектах.
    /// </summary>
    public const int MaxJsonDepth = 32;

    public const int MaxIdentifierLength = 128;

    public const int MaxNameLength = 256;

    public const int MaxTabs = 512;

    public const int MaxCategoriesPerTab = 512;

    public const int MaxItemsPerCategory = 4096;

    /// <summary>
    /// Максимальная длина записываемого JSON-документа в байтах UTF-8.
    /// За превышение запись отклоняется: существующий корректный файл не перезаписывается.
    /// </summary>
    public const long MaxWritableDocumentBytes = 4L * 1024 * 1024;

    public static bool IsValidIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaxIdentifierLength &&
        !ContainsControlCharacters(value);

    public static bool IsValidName(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaxNameLength &&
        !ContainsControlCharacters(value);

    /// <summary>
    /// Отклоняет управляющие символы: они не имеют смысла в идентификаторах и именах,
    /// но позволяют внедрять переводы строк и escape-последовательности в логи и UI.
    /// </summary>
    public static bool ContainsControlCharacters(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }
}
