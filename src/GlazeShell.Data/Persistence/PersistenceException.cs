namespace GlazeShell.Data.Persistence;

/// <summary>
/// Ошибка операции хранения, означающая, что данные не были сохранены.
/// Исключение не содержит содержимого документов: диагностика формируется отдельно.
/// </summary>
public class PersistenceException : Exception
{
    public PersistenceException(string message)
        : base(message)
    {
    }

    public PersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Документ превысил допустимый размер или количество элементов и не был сохранён.
/// </summary>
public sealed class PersistenceLimitExceededException : PersistenceException
{
    public PersistenceLimitExceededException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Документ создан более новой версией приложения. Такой документ не изменяется:
/// иначе понижение версии уничтожило бы данные, которые новая версия записала осмысленно.
/// </summary>
public sealed class UnsupportedSchemaVersionException : PersistenceException
{
    public UnsupportedSchemaVersionException(string message, int documentVersion, int currentVersion)
        : base(message)
    {
        DocumentVersion = documentVersion;
        CurrentVersion = currentVersion;
    }

    public int DocumentVersion { get; }

    public int CurrentVersion { get; }
}
