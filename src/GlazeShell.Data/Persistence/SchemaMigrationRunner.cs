using System.Text.Json;
using System.Text.Json.Nodes;
using GlazeShell.Data.Serialization;

namespace GlazeShell.Data.Persistence;

/// <summary>
/// Цепочка миграций схемы одного документа.
/// </summary>
/// <remarks>
/// Миграции применяются строго последовательно: <c>v1 → v2 → v3</c>. Пропуск версии невозможен,
/// поэтому документ не может оказаться в промежуточном, неизвестном текущему коду состоянии.
/// Миграции других типов документов игнорируются: цепочка строится для одного типа документа.
/// </remarks>
public sealed class SchemaMigrationRunner
{
    /// <summary>
    /// Имя поля с версией схемы во всех пользовательских документах.
    /// </summary>
    public const string SchemaVersionProperty = "schemaVersion";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        MaxDepth = PersistenceLimits.MaxJsonDepth,
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow
    };

    private readonly Dictionary<int, IDataMigration> _migrations;

    public SchemaMigrationRunner(
        int currentVersion,
        UserDataDocumentKind document,
        IEnumerable<IDataMigration>? migrations = null)
    {
        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion), currentVersion, "The current schema version must be positive.");
        }

        if (!Enum.IsDefined(document))
        {
            throw new ArgumentOutOfRangeException(nameof(document), document, "The document kind is not defined.");
        }

        var map = new Dictionary<int, IDataMigration>();
        foreach (var migration in migrations ?? Array.Empty<IDataMigration>())
        {
            ArgumentNullException.ThrowIfNull(migration);

            if (migration.Document != document)
            {
                continue;
            }

            if (migration.FromVersion < 1 || migration.FromVersion >= currentVersion)
            {
                throw new ArgumentException(
                    $"The {document} migration from version {migration.FromVersion} is outside the range 1..{currentVersion - 1}.",
                    nameof(migrations));
            }

            if (!map.TryAdd(migration.FromVersion, migration))
            {
                throw new ArgumentException(
                    $"More than one {document} migration is registered for version {migration.FromVersion}.",
                    nameof(migrations));
            }
        }

        _migrations = map;
        CurrentVersion = currentVersion;
        Document = document;
    }

    public int CurrentVersion { get; }

    public UserDataDocumentKind Document { get; }

    /// <summary>
    /// Применяет все недостающие миграции к документу.
    /// </summary>
    /// <param name="document">Разобранный документ.</param>
    /// <param name="schemaVersion">
    /// Версия, прочитанная из документа. Должна быть в диапазоне <c>1..<see cref="CurrentVersion"/></c>.
    /// </param>
    /// <returns>Документ после миграций и результат миграции.</returns>
    /// <exception cref="PersistenceException">
    /// Для какой-то версии отсутствует миграция.
    /// </exception>
    /// <exception cref="UnsupportedSchemaVersionException">
    /// Документ новее, чем поддерживает приложение.
    /// </exception>
    public (JsonObject Document, MigrationResult Result) Migrate(JsonObject document, int schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (schemaVersion < 1)
        {
            throw new PersistenceException($"The schema version {schemaVersion} is not valid.");
        }

        if (schemaVersion > CurrentVersion)
        {
            throw new UnsupportedSchemaVersionException(
                $"The {Document} schema version {schemaVersion} is newer than the supported version {CurrentVersion}.",
                schemaVersion,
                CurrentVersion);
        }

        var applied = new List<int>();
        var version = schemaVersion;
        while (version < CurrentVersion)
        {
            if (!_migrations.TryGetValue(version, out var migration))
            {
                throw new PersistenceException(
                    $"No migration is registered from schema version {version} to {version + 1} for {Document}.");
            }

            try
            {
                migration.Apply(document);
            }
            catch (PersistenceException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new PersistenceException($"The {Document} migration from schema version {version} failed.", exception);
            }

            document[SchemaVersionProperty] = version + 1;
            applied.Add(version);
            version++;
        }

        return (document, new MigrationResult(schemaVersion, version, applied));
    }

    /// <summary>
    /// Разбирает документ и применяет миграции.
    /// </summary>
    /// <exception cref="PersistenceException">Документ не является объектом JSON.</exception>
    public (JsonObject Document, MigrationResult Result) ParseAndMigrate(string content, int schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(content);

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(content, documentOptions: DocumentOptions);
        }
        catch (JsonException exception)
        {
            throw new PersistenceException("The document is not valid JSON.", exception);
        }

        if (root is not JsonObject document)
        {
            throw new PersistenceException("The document root must be a JSON object.");
        }

        return Migrate(document, schemaVersion);
    }

    /// <summary>
    /// Читает версию схемы из узла документа. Отсутствующее или нечисловое поле даёт <c>null</c>.
    /// </summary>
    public static int? ReadSchemaVersion(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!document.TryGetPropertyValue(SchemaVersionProperty, out var node) || node is null)
        {
            return null;
        }

        return node is JsonValue value && value.TryGetValue(out int version) ? version : null;
    }
}
