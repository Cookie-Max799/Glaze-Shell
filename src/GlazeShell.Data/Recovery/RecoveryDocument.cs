using System.Text.Json.Serialization;

namespace GlazeShell.Data.Recovery;

/// <summary>
/// Документ состояния восстановления (`recovery.json`).
/// </summary>
/// <remarks>
/// Документ хранит два факта: завершилась ли прошлая сессия штатно и когда оболочка падала.
/// Незавершённая сессия означает падение, поэтому отдельный журнал аварий не нужен.
/// </remarks>
public sealed class RecoveryDocument
{
    /// <summary>Текущая версия схемы документа.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Версия схемы документа.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// Завершилась ли прошлая сессия штатно. Значение <see langword="false"/> означает,
    /// что оболочка была закрыта аварийно.
    /// </summary>
    public bool SessionCompleted { get; set; } = true;

    /// <summary>Моменты известных падений оболочки.</summary>
    public List<DateTimeOffset> Crashes { get; set; } = [];

    /// <summary>
    /// Читает ли документ ожидаемую версию схемы.
    /// </summary>
    [JsonIgnore]
    public bool IsSupportedSchema => SchemaVersion == CurrentSchemaVersion;
}
