using GlazeShell.Core.Models;

namespace GlazeShell.Core.Interfaces;

public interface ISettingsManager
{
    UserSettings GetSettings();

    void SetSettings(UserSettings settings);
}
