using System.Text.Json;
using System.Text.Json.Serialization;

namespace GlazeShell.Data.Serialization;

/// <summary>
/// Единые параметры сериализации пользовательских документов.
/// </summary>
public static class GlazeJson
{
    /// <summary>
    /// Параметры для чтения и записи документов пользовательских данных.
    /// </summary>
    /// <remarks>
    /// Решения по безопасности:
    /// имена свойств сравниваются с учётом регистра, поэтому `schemaVersion` и `SchemaVersion`
    /// не могут быть приняты за одно и то же поле; полим с именем `schemaVersion` соответствует
    /// одно свойство C#, поэтому ключ не может быть продублирован с другим регистром;
    /// `MaxDepth`, запрет trailing commas и комментариев ограничивают стоимость разбора;
    /// типы известны на этапе компиляции, полим с типом `object` и полим `$type` нет,
    /// поэтому выбрать другой тип при десериализации невозможно.
    /// </remarks>
    public static JsonSerializerOptions Document { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        MaxDepth = PersistenceLimits.MaxJsonDepth,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        IndentCharacter = ' ',
        IndentSize = 2,
        NewLine = Environment.NewLine
    };
}
