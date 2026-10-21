using System.Text.Json;
using GlazeShell.Core.Recovery;
using GlazeShell.Data.Persistence;
using GlazeShell.Data.Serialization;

namespace GlazeShell.Data.Recovery;

/// <summary>
/// Хранилище состояния восстановления (функция I2): файл <c>recovery.json</c> в корневом
/// каталоге пользовательских данных.
/// </summary>
/// <remarks>
/// <para>
/// Документ небольшой и пишется целиком, поэтому отдельная схема миграций ему не нужна:
/// при неизвестной версии схемы файл изолируется и состояние считается пустым.
/// </para>
/// <para>
/// Запись атомарна, чтение ограничено по размеру, повреждённый документ изолируется
/// перемещением, а не удаляется — так же, как у остальных пользовательских документов.
/// </para>
/// </remarks>
public sealed class RecoveryStore
{
    /// <summary>Имя файла состояния восстановления.</summary>
    public const string FileName = "recovery.json";

    /// <summary>Максимальное число хранимых моментов падений.</summary>
    public const int MaxStoredCrashes = 32;

    private readonly UserDataDirectory _directory;

    /// <param name="rootDirectory">Корневой каталог пользовательских данных.</param>
    public RecoveryStore(string rootDirectory)
        : this(new UserDataDirectory(rootDirectory))
    {
    }

    internal RecoveryStore(UserDataDirectory directory) =>
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));

    /// <summary>
    /// Читает состояние восстановления. Отсутствующий или повреждённый документ не является
    /// ошибкой запуска: возвращается пустое состояние, а причины попадают в диагностику.
    /// </summary>
    public RecoverySnapshot Load(out IReadOnlyList<string> diagnostics)
    {
        var messages = new List<string>();
        var read = _directory.ReadDocument(FileName, PersistenceLimits.MaxDocumentBytes);

        switch (read.Status)
        {
            case DocumentReadStatus.Missing:
                diagnostics = Array.Empty<string>();
                return RecoverySnapshot.Empty;

            case DocumentReadStatus.TooLarge:
                _directory.QuarantineDocument(FileName);
                diagnostics = [read.Failure ?? "The recovery document is too large."];
                return RecoverySnapshot.Empty;

            case DocumentReadStatus.Unreadable:
                diagnostics = [read.Failure ?? "The recovery document cannot be read."];
                return RecoverySnapshot.Empty;
        }

        RecoveryDocument? document;

        try
        {
            document = JsonSerializer.Deserialize<RecoveryDocument>(read.Content!, GlazeJson.Document);
        }
        catch (JsonException exception)
        {
            _directory.QuarantineDocument(FileName);
            diagnostics = [$"The recovery document does not match the expected JSON structure: {exception.Message}"];
            return RecoverySnapshot.Empty;
        }

        if (document is null)
        {
            diagnostics = ["The recovery document is empty."];
            return RecoverySnapshot.Empty;
        }

        if (!document.IsSupportedSchema)
        {
            _directory.QuarantineDocument(FileName);
            diagnostics = [$"The recovery document has an unsupported schema version {document.SchemaVersion}."];
            return RecoverySnapshot.Empty;
        }

        var crashes = document.Crashes
            .Where(crash => crash != default)
            .OrderBy(crash => crash)
            .TakeLast(MaxStoredCrashes)
            .ToArray();

        diagnostics = Array.Empty<string>();
        return new RecoverySnapshot(document.SessionCompleted, crashes);
    }

    /// <summary>
    /// Сохраняет состояние восстановления. Ошибка записи не должна мешать запуску:
    /// вызывающий код логирует исключение и продолжает работу.
    /// </summary>
    public void Save(RecoverySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var document = new RecoveryDocument
        {
            SessionCompleted = snapshot.SessionCompleted,
            Crashes = [.. snapshot.Crashes.OrderBy(crash => crash).TakeLast(MaxStoredCrashes)],
        };

        var json = JsonSerializer.Serialize(document, GlazeJson.Document);
        _directory.WriteDocument(FileName, json, PersistenceLimits.MaxWritableDocumentBytes);
    }
}
