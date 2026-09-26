using GlazeShell.Core.Models;

namespace GlazeShell.Core.Interfaces;

public interface IThemeManager
{
    IReadOnlyList<Theme> GetThemes();

    Theme? GetTheme(string themeId);
}
