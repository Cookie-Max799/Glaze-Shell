namespace GlazeShell.Core.Persistence;

/// <summary>
/// Результат загрузки документа пользовательских данных.
/// </summary>
public enum PersistenceStatus
{
    /// <summary>
    /// Документ отсутствовал и был создан со значениями по умолчанию.
    /// </summary>
    Created = 0,

    /// <summary>
    /// Документ загружен без изменений.
    /// </summary>
    Loaded = 1,

    /// <summary>
    /// Документ загружен и обновлён цепочкой миграций схемы.
    /// </summary>
    Migrated = 2,

    /// <summary>
    /// Документ оказался непригоден: он изолирован, а вместо него применены значения по умолчанию.
    /// </summary>
    Recovered = 3,

    /// <summary>
    /// Документ создан более новой версией приложения и оставлен нетронутым.
    /// </summary>
    Unsupported = 4
}

/// <summary>
/// Значение, загруженное из хранилища, вместе с результатом загрузки и диагностикой.
/// </summary>
public sealed record PersistenceLoadResult<T>
{
    public PersistenceLoadResult(T value, PersistenceStatus status, IEnumerable<string>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "The persistence status is not defined.");
        }

        Value = value;
        Status = status;
        Diagnostics = diagnostics is null ? Array.Empty<string>() : diagnostics.ToArray();
    }

    public T Value { get; }

    public PersistenceStatus Status { get; }

    /// <summary>
    /// Понятные описания проблем, обнаруженных при загрузке. Не содержат secrets.
    /// </summary>
    public IReadOnlyList<string> Diagnostics { get; }

    /// <summary>
    /// <c>true</c>, если сохранённые данные не были использованы.
    /// </summary>
    public bool UsedDefaults => Status is PersistenceStatus.Created or PersistenceStatus.Recovered or PersistenceStatus.Unsupported;
}
