using GlazeShell.Core.Configuration;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Data.Persistence;
using GlazeShell.Data.Serialization;

namespace GlazeShell.Data.Tests;

/// <summary>
/// Изолированный временный каталог пользовательских данных на время одного теста.
/// </summary>
internal sealed class TempUserData : IDisposable
{
    public TempUserData()
    {
        Root = Path.Combine(Path.GetTempPath(), "GlazeShell.Data.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Reported = new List<string>();
    }

    public string Root { get; }

    public List<string> Reported { get; }

    public JsonUserDataStore CreateStore(IEnumerable<IDataMigration>? migrations = null) =>
        new(Root, (message, _) => Reported.Add(message), migrations);

    /// <summary>
    /// Хранилище с произвольными версиями схемы — используется для проверки миграций.
    /// </summary>
    public JsonUserDataStore CreateStore(SchemaVersionSet versions, IEnumerable<IDataMigration>? migrations = null) =>
        new(Root, (message, _) => Reported.Add(message), versions, migrations);

    public string GetPath(string fileName) => Path.Combine(Root, fileName);

    public bool FileExists(string fileName) => File.Exists(GetPath(fileName));

    public string Read(string fileName) => File.ReadAllText(GetPath(fileName));

    public void Write(string fileName, string content) =>
        File.WriteAllText(GetPath(fileName), content);

    /// <summary>
    /// Занимает имя каталога файлом, чтобы создание каталога завершалось ошибкой.
    /// Так проверяется поведение хранилища при недоступных резервных копиях и изоляции.
    /// </summary>
    public void BlockDirectory(string directory) =>
        File.WriteAllText(Path.Combine(Root, directory), "not a directory");

    public IEnumerable<string> ListFiles(string directory) =>
        Directory.Exists(Path.Combine(Root, directory))
            ? Directory.GetFiles(Path.Combine(Root, directory)).Select(Path.GetFileName).OfType<string>().Order().ToArray()
            : Array.Empty<string>();

    public static DesktopLayout CreateLayout(params DesktopTab[] tabs) =>
        new(tabs, tabs.Length > 0 ? tabs[0].Id : null);

    public static UserSettings CreateSettings(bool startWithWindows = true, string language = "en-US", string? themeId = "midnight") =>
        new(startWithWindows, minimizeToTray: true, showDesktopOnStartup: false, restoreLastLayout: false, language, themeId);

    public static GlazeShellConfiguration CreateConfiguration(string applicationName = "Glaze Shell", string dataDirectoryName = "GlazeShell") =>
        new() { ApplicationName = applicationName, DataDirectoryName = dataDirectoryName };

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
