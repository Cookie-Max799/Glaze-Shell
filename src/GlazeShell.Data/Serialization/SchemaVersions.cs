namespace GlazeShell.Data.Serialization;

/// <summary>
/// Текущие версии схемы пользовательских документов.
/// Увеличение версии требует регистрации цепочки миграций в
/// <see cref="Persistence.SchemaMigrationRunner"/>.
/// </summary>
public static class SchemaVersions
{
    public const int Configuration = 1;

    public const int Settings = 1;

    public const int Layout = 1;
}
