using System.Text.Json;
using System.Text.Json.Nodes;
using GlazeShell.Core.Configuration;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Core.Persistence;
using GlazeShell.Data.Serialization;

namespace GlazeShell.Data.Persistence;

/// <summary>
/// Хранилище пользовательских данных в JSON.
/// </summary>
/// <remarks>
/// Файлы: <c>config.json</c>, <c>settings.json</c>, <c>layout.json</c>.
/// Запись атомарна, чтение ограничено по размеру, повреждённые документы изолируются
/// в <c>recovery</c>, перед миграцией создаётся копия в <c>backups</c>.
/// Пути берутся только из кода: значения из документов никогда не используются как пути.
/// </remarks>
public sealed class JsonUserDataStore : IUserDataStore
{
    private readonly UserDataDirectory _directory;

    public JsonUserDataStore(
        string rootDirectory,
        PersistenceErrorReporter? errorReporter = null,
        IEnumerable<IDataMigration>? migrations = null)
        : this(new UserDataDirectory(rootDirectory), errorReporter, SchemaVersionSet.Current, migrations)
    {
    }

    internal JsonUserDataStore(
        string rootDirectory,
        PersistenceErrorReporter? errorReporter,
        SchemaVersionSet versions,
        IEnumerable<IDataMigration>? migrations = null)
        : this(new UserDataDirectory(rootDirectory), errorReporter, versions, migrations)
    {
    }

    internal JsonUserDataStore(
        UserDataDirectory directory,
        PersistenceErrorReporter? errorReporter,
        SchemaVersionSet versions,
        IEnumerable<IDataMigration>? migrations = null)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        ArgumentNullException.ThrowIfNull(versions);

        Configuration = new JsonDocumentPipeline(directory, errorReporter, UserDataDocumentKind.Configuration, versions.Configuration, migrations);
        Settings = new JsonDocumentPipeline(directory, errorReporter, UserDataDocumentKind.Settings, versions.Settings, migrations);
        Layout = new JsonDocumentPipeline(directory, errorReporter, UserDataDocumentKind.Layout, versions.Layout, migrations);
    }

    private JsonDocumentPipeline Configuration { get; }

    private JsonDocumentPipeline Settings { get; }

    private JsonDocumentPipeline Layout { get; }

    public string RootDirectory => _directory.RootDirectory;

    public PersistenceLoadResult<GlazeShellConfiguration> LoadConfiguration() =>
        Configuration.Load(
            UserDataFileNames.Configuration,
            SchemaVersions.Configuration,
            MapConfiguration,
            SerializeConfiguration,
            GlazeShellConfiguration.CreateDefault);

    public void SaveConfiguration(GlazeShellConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        Write(UserDataFileNames.Configuration, SerializeConfiguration(configuration));
    }

    public PersistenceLoadResult<UserSettings> LoadSettings() =>
        Settings.Load(
            UserDataFileNames.Settings,
            SchemaVersions.Settings,
            MapSettings,
            SerializeSettings,
            UserSettings.CreateDefault);

    public void SaveSettings(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Write(UserDataFileNames.Settings, SerializeSettings(settings));
    }

    public PersistenceLoadResult<DesktopLayout> LoadLayout() =>
        Layout.Load(
            UserDataFileNames.Layout,
            SchemaVersions.Layout,
            MapLayout,
            SerializeLayout,
            CreateEmptyLayout);

    public void SaveLayout(DesktopLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        Write(UserDataFileNames.Layout, SerializeLayout(layout));
    }

    /// <summary>
    /// Отложенных записей хранилище не создаёт: каждая запись завершается до возврата.
    /// </summary>
    public void Flush()
    {
    }

    private static DesktopLayout CreateEmptyLayout() => new(Array.Empty<DesktopTab>());

    private static string SerializeConfiguration(GlazeShellConfiguration configuration) =>
        JsonSerializer.Serialize(ConfigurationDocument.FromConfiguration(configuration), GlazeJson.Document);

    private static string SerializeSettings(UserSettings settings) =>
        JsonSerializer.Serialize(SettingsDocument.FromSettings(settings), GlazeJson.Document);

    private static string SerializeLayout(DesktopLayout layout) =>
        JsonSerializer.Serialize(LayoutDocument.FromLayout(layout), GlazeJson.Document);

    private static bool MapConfiguration(JsonObject document, out GlazeShellConfiguration? value, out IReadOnlyList<string> diagnostics)
    {
        var configurationDocument = Deserialize<ConfigurationDocument>(document, "configuration", out diagnostics);
        if (configurationDocument is not null)
        {
            return ConfigurationDocumentMapper.TryMap(configurationDocument, out value, out diagnostics);
        }

        value = null;
        return false;
    }

    private static bool MapSettings(JsonObject document, out UserSettings? value, out IReadOnlyList<string> diagnostics)
    {
        var settingsDocument = Deserialize<SettingsDocument>(document, "settings", out diagnostics);
        if (settingsDocument is not null)
        {
            return SettingsDocumentMapper.TryMap(settingsDocument, out value, out diagnostics);
        }

        value = null;
        return false;
    }

    private static bool MapLayout(JsonObject document, out DesktopLayout? value, out IReadOnlyList<string> diagnostics)
    {
        var layoutDocument = Deserialize<LayoutDocument>(document, "layout", out diagnostics);
        if (layoutDocument is not null)
        {
            return LayoutDocumentMapper.TryMap(layoutDocument, out value, out diagnostics);
        }

        value = null;
        return false;
    }

    private static TDocument? Deserialize<TDocument>(JsonObject document, string kind, out IReadOnlyList<string> diagnostics)
        where TDocument : class
    {
        try
        {
            diagnostics = Array.Empty<string>();
            return document.Deserialize<TDocument>(GlazeJson.Document);
        }
        catch (JsonException exception)
        {
            diagnostics = [$"The {kind} document does not match the expected JSON structure: {exception.Message}"];
            return null;
        }
    }

    private void Write(string fileName, string json) =>
        _directory.WriteDocument(fileName, json, PersistenceLimits.MaxWritableDocumentBytes);
}
