using System.Text.Json.Nodes;

namespace GlazeShell.Data.Persistence;

/// <summary>
/// Тип пользовательского документа, к которому относится миграция.
/// </summary>
public enum UserDataDocumentKind
{
    Configuration = 0,
    Settings = 1,
    Layout = 2
}

/// <summary>
/// Одна миграция схемы документа.
/// </summary>
public interface IDataMigration
{
    /// <summary>
    /// Тип документа, к которому относится миграция. Миграция никогда не применяется к другому документу.
    /// </summary>
    UserDataDocumentKind Document { get; }

    /// <summary>
    /// Версия документа, к которой относится миграция.
    /// </summary>
    int FromVersion { get; }

    /// <summary>
    /// Приводит документ к следующей версии. Вызывается только когда
    /// <see cref="SchemaMigrationRunner.SchemaVersionProperty"/> документа равен <see cref="FromVersion"/>.
    /// </summary>
    void Apply(JsonObject document);
}

/// <summary>
/// Результат выполнения цепочки миграций.
/// </summary>
public sealed record MigrationResult
{
    public MigrationResult(int fromVersion, int toVersion, IEnumerable<int>? appliedVersions = null)
    {
        if (fromVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(fromVersion), fromVersion, "The schema version must be positive.");
        }

        if (toVersion < fromVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(toVersion), toVersion, "The target version cannot be lower than the source version.");
        }

        FromVersion = fromVersion;
        ToVersion = toVersion;
        AppliedVersions = appliedVersions is null ? Array.Empty<int>() : appliedVersions.ToArray();
    }

    public int FromVersion { get; }

    public int ToVersion { get; }

    /// <summary>
    /// Версии, из которых документ мигрирован, по порядку применения.
    /// </summary>
    public IReadOnlyList<int> AppliedVersions { get; }

    public bool Migrated => AppliedVersions.Count > 0;
}
