using System.Text.Json;
using System.Text.Json.Nodes;
using GlazeShell.Core.Persistence;
using GlazeShell.Data.Serialization;

namespace GlazeShell.Data.Persistence;

/// <summary>
/// Преобразование узла JSON в доменную модель с диагностикой.
/// </summary>
internal delegate bool DocumentMapper<T>(JsonObject document, out T? value, out IReadOnlyList<string> diagnostics)
    where T : class;

/// <summary>
/// Общий для всех документов путь загрузки: чтение с ограничением размера, разбор,
/// цепочка миграций, преобразование в модель и recovery при любом сбое.
/// </summary>
internal sealed class JsonDocumentPipeline
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        MaxDepth = PersistenceLimits.MaxJsonDepth,
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow
    };

    private readonly UserDataDirectory _directory;
    private readonly PersistenceErrorReporter? _errorReporter;
    private readonly UserDataDocumentKind _kind;
    private readonly SchemaMigrationRunner _runner;

    public JsonDocumentPipeline(
        UserDataDirectory directory,
        PersistenceErrorReporter? errorReporter,
        UserDataDocumentKind kind,
        int currentVersion,
        IEnumerable<IDataMigration>? migrations)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _kind = kind;
        _errorReporter = errorReporter;
        _runner = new SchemaMigrationRunner(currentVersion, kind, migrations);
    }

    public UserDataDocumentKind Kind => _kind;

    public PersistenceLoadResult<T> Load<T>(
        string fileName,
        int defaultSchemaVersion,
        DocumentMapper<T> mapper,
        Func<T, string> serialize,
        Func<T> fallback)
        where T : class
    {
        var read = _directory.ReadDocument(fileName, PersistenceLimits.MaxDocumentBytes);

        switch (read.Status)
        {
            case DocumentReadStatus.Missing:
            {
                var created = fallback();
                TryWrite(fileName, serialize(created));
                return new PersistenceLoadResult<T>(created, PersistenceStatus.Created);
            }

            case DocumentReadStatus.TooLarge:
            case DocumentReadStatus.Unreadable:
                return Recover(fileName, read.Failure ?? "The document is unavailable.", serialize, fallback);
        }

        JsonObject document;
        try
        {
            document = Parse(read.Content!);
        }
        catch (PersistenceException exception)
        {
            return Recover(fileName, exception.Message, serialize, fallback);
        }

        var diagnostics = new List<string>();
        var declaredVersion = SchemaMigrationRunner.ReadSchemaVersion(document);

        if (declaredVersion is null)
        {
            declaredVersion = defaultSchemaVersion;
            diagnostics.Add($"The document '{fileName}' has no schema version and was read as version {defaultSchemaVersion}.");
        }
        else if (declaredVersion < 1)
        {
            return Recover(fileName, $"The document '{fileName}' declares the schema version {declaredVersion}.", serialize, fallback);
        }

        MigrationResult migration;
        try
        {
            (_, migration) = _runner.Migrate(document, declaredVersion.Value);
        }
        catch (UnsupportedSchemaVersionException exception)
        {
            var message =
                $"The document '{fileName}' was created by a newer version of Glaze Shell (schema {exception.DocumentVersion}, supported {exception.CurrentVersion}) and was left untouched.";
            Report(message, null);
            diagnostics.Add(message);

            return new PersistenceLoadResult<T>(fallback(), PersistenceStatus.Unsupported, diagnostics);
        }
        catch (PersistenceException exception)
        {
            return Recover(fileName, exception.Message, serialize, fallback);
        }

        if (!mapper(document, out var value, out var mappingDiagnostics) || value is null)
        {
            var details = mappingDiagnostics.Count > 0
                ? string.Join(" ", mappingDiagnostics)
                : "the document does not match the expected schema";

            return Recover(fileName, $"The document '{fileName}' cannot be used: {details}.", serialize, fallback);
        }

        diagnostics.AddRange(mappingDiagnostics);

        if (!migration.Migrated)
        {
            return new PersistenceLoadResult<T>(value, PersistenceStatus.Loaded, diagnostics);
        }

        // Миграция перезаписывает документ, поэтому без резервной копии она не выполняется:
        // иначе сбой копирования приводил бы к потере исходных данных.
        var backupPath = _directory.CreateBackup(fileName);
        if (backupPath is null && _directory.Exists(fileName))
        {
            return Recover(
                fileName,
                $"A backup of '{fileName}' could not be created before the migration, so the document was not migrated.",
                serialize,
                fallback);
        }

        TryWrite(fileName, document.ToJsonString(GlazeJson.Document));

        diagnostics.Add($"The document '{fileName}' was migrated from schema version {migration.FromVersion} to {migration.ToVersion}.");
        if (backupPath is not null)
        {
            diagnostics.Add($"A backup was created before the migration: {Path.GetFileName(backupPath)}.");
        }

        Report(
            $"The document '{fileName}' was migrated from schema version {migration.FromVersion} to {migration.ToVersion}.",
            null);

        return new PersistenceLoadResult<T>(value, PersistenceStatus.Migrated, diagnostics);
    }

    private static JsonObject Parse(string content)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(content, documentOptions: DocumentOptions);
        }
        catch (JsonException exception)
        {
            throw new PersistenceException("The document is not valid JSON.", exception);
        }

        return root as JsonObject ?? throw new PersistenceException("The document root must be a JSON object.");
    }

    private PersistenceLoadResult<T> Recover<T>(
        string fileName,
        string reason,
        Func<T, string> serialize,
        Func<T> fallback)
        where T : class
    {
        var diagnostics = new List<string> { reason };

        var quarantinePath = _directory.QuarantineDocument(fileName);
        if (quarantinePath is not null)
        {
            diagnostics.Add($"The unusable document was isolated as {Path.GetFileName(quarantinePath)}.");
        }

        var fallbackValue = fallback();

        if (quarantinePath is null && _directory.Exists(fileName))
        {
            // Документ не удалось изолировать: перезапись уничтожила бы исходные данные,
            // поэтому значения по умолчанию применяются только в памяти до конца сессии.
            var preserved = $"The document '{fileName}' could not be isolated and was left unchanged.";
            diagnostics.Add(preserved);
            Report($"The document '{fileName}' was recovered with default values. {reason} {preserved}", null);

            return new PersistenceLoadResult<T>(fallbackValue, PersistenceStatus.Recovered, diagnostics);
        }

        TryWrite(fileName, serialize(fallbackValue));
        Report($"The document '{fileName}' was recovered with default values. {reason}", null);

        return new PersistenceLoadResult<T>(fallbackValue, PersistenceStatus.Recovered, diagnostics);
    }

    private void TryWrite(string fileName, string json)
    {
        try
        {
            _directory.WriteDocument(fileName, json, PersistenceLimits.MaxWritableDocumentBytes);
        }
        catch (Exception exception) when (exception is PersistenceException or IOException or UnauthorizedAccessException)
        {
            Report($"The document '{fileName}' could not be written.", exception);
        }
    }

    private void Report(string message, Exception? exception) => _errorReporter?.Invoke(message, exception);
}
