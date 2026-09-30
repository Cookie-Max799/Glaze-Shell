using GlazeShell.Data.Serialization;

namespace GlazeShell.Data.Persistence;

/// <summary>
/// Версии схемы документов, с которыми работает конкретный экземпляр хранилища.
/// </summary>
internal sealed record SchemaVersionSet(int Configuration, int Settings, int Layout)
{
    public static SchemaVersionSet Current { get; } = new(
        SchemaVersions.Configuration,
        SchemaVersions.Settings,
        SchemaVersions.Layout);

    public int GetVersion(UserDataDocumentKind kind) => kind switch
    {
        UserDataDocumentKind.Configuration => Configuration,
        UserDataDocumentKind.Settings => Settings,
        UserDataDocumentKind.Layout => Layout,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The document kind is not defined.")
    };
}
