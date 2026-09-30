using GlazeShell.Core.Configuration;
using GlazeShell.Core.Models;
using GlazeShell.Core.Persistence;

namespace GlazeShell.Core.Interfaces;

/// <summary>
/// Хранилище пользовательских данных Glaze Shell в каталоге пользователя.
/// Реализация обязана быть атомарной, ограничивать размер читаемых документов
/// и изолировать повреждённые данные вместо их удаления.
/// </summary>
public interface IUserDataStore
{
    /// <summary>
    /// Абсолютный путь корневого каталога пользовательских данных.
    /// </summary>
    string RootDirectory { get; }

    PersistenceLoadResult<GlazeShellConfiguration> LoadConfiguration();

    void SaveConfiguration(GlazeShellConfiguration configuration);

    PersistenceLoadResult<UserSettings> LoadSettings();

    void SaveSettings(UserSettings settings);

    PersistenceLoadResult<DesktopLayout> LoadLayout();

    void SaveLayout(DesktopLayout layout);

    /// <summary>
    /// Принудительно завершает отложенные записи. Реализация без отложенных записей ничего не делает.
    /// </summary>
    void Flush();
}
