using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;

namespace GlazeShell.Core.Services;

public sealed class InMemorySettingsManager : ISettingsManager
{
    private readonly IEventManager _eventManager;
    private readonly object _sync = new();
    private UserSettings _settings;

    public InMemorySettingsManager(IEventManager eventManager, UserSettings? initialSettings = null)
    {
        _eventManager = eventManager ?? throw new ArgumentNullException(nameof(eventManager));
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

        _eventManager.Publish(new SettingsChanged(settings));
    }
}
