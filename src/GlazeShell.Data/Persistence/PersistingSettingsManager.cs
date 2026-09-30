using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Core.Persistence;

namespace GlazeShell.Data.Persistence;

/// <summary>
/// <see cref="ISettingsManager"/>, который загружает настройки при создании и сохраняет их при изменении.
/// </summary>
/// <remarks>
/// Ошибка записи не отменяет изменение: настройки живут в памяти до конца сессии, а сбой сохранения
/// передаётся обработчику и попадает в лог. Иначе временно недоступный диск сделал бы
/// настройки только для чтения и потерял бы их до конца сессии.
/// </remarks>
public sealed class PersistingSettingsManager : ISettingsManager
{
    private readonly IUserDataStore _store;
    private readonly IEventManager? _eventManager;
    private readonly PersistenceErrorReporter? _errorReporter;
    private readonly object _sync = new();
    private UserSettings _settings;

    public PersistingSettingsManager(
        IUserDataStore store,
        IEventManager? eventManager = null,
        PersistenceErrorReporter? errorReporter = null,
        UserSettings? initialSettings = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _eventManager = eventManager;
        _errorReporter = errorReporter;
        _settings = initialSettings ?? UserSettings.CreateDefault();
    }

    public UserSettings GetSettings()
    {
        lock (_sync)
        {
            return _settings;
        }
    }

    public void SetSettings(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_sync)
        {
            if (_settings == settings)
            {
                return;
            }

            _settings = settings;
        }

        _eventManager?.Publish(new SettingsChanged(settings));
        TrySave(settings);
    }

    /// <summary>
    /// Загружает сохранённые настройки в текущий экземпляр.
    /// </summary>
    /// <returns>Результат загрузки для диагностики при запуске.</returns>
    public PersistenceLoadResult<UserSettings> Load()
    {
        var result = _store.LoadSettings();

        lock (_sync)
        {
            _settings = result.Value;
        }

        return result;
    }

    private void TrySave(UserSettings settings)
    {
        try
        {
            _store.SaveSettings(settings);
        }
        catch (Exception exception) when (exception is PersistenceException or IOException or UnauthorizedAccessException)
        {
            _errorReporter?.Invoke("The user settings could not be saved.", exception);
        }
    }
}
