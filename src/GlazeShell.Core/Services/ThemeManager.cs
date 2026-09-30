using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;

namespace GlazeShell.Core.Services;

/// <summary>
/// Реестр тем и выбор действующей темы.
/// </summary>
/// <remarks>
/// Свойства слоя:
/// темы хранятся в памяти и доступны только для чтения — тема не меняется после загрузки,
/// поэтому подписчики <see cref="ThemeChanged"/> всегда получают согласованный снимок;
/// неизвестный идентификатор не оставляет приложение без темы, а сохраняет предыдущую;
/// выбор темы без фактического изменения не публикует событие, как и мутации desktop layout.
/// </remarks>
public sealed class ThemeManager : IThemeManager
{
    private readonly IEventManager? _eventManager;
    private readonly object _sync = new();
    private Theme[] _themes;
    private Theme _activeTheme;

    public ThemeManager(
        IEventManager? eventManager = null,
        IEnumerable<Theme>? themes = null,
        string? activeThemeId = null)
    {
        _eventManager = eventManager;
        _themes = Normalize(themes);
        _activeTheme = Resolve(_themes, activeThemeId) ?? Theme.CreateDefault();
    }

    public IReadOnlyList<Theme> GetThemes()
    {
        lock (_sync)
        {
            return _themes;
        }
    }

    public Theme? GetTheme(string themeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themeId);

        lock (_sync)
        {
            return Find(_themes, themeId);
        }
    }

    public Theme GetActiveTheme()
    {
        lock (_sync)
        {
            return _activeTheme;
        }
    }

    public bool SetActiveTheme(string? themeId)
    {
        var requested = string.IsNullOrWhiteSpace(themeId) ? null : themeId;

        Theme? selected;
        string? previousId;

        lock (_sync)
        {
            selected = requested is null ? Theme.CreateDefault() : Find(_themes, requested);
            if (selected is null)
            {
                return false;
            }

            if (selected.Id == _activeTheme.Id)
            {
                return true;
            }

            previousId = _activeTheme.Id;
            _activeTheme = selected;
        }

        _eventManager?.Publish(new ThemeChanged(selected, previousId));
        return true;
    }

    /// <summary>
    /// Заменяет набор тем, сохраняя активную тему, если она осталась в наборе.
    /// </summary>
    /// <param name="themes">Новый набор тем; <c>null</c> равносилен пустому набору.</param>
    /// <param name="activeThemeId">
    /// Идентификатор темы, которую нужно выбрать. Если он не задан, сохраняется текущая тема,
    /// а если она исчезла из набора — выбирается первая доступная.
    /// </param>
    /// <returns><c>true</c>, если активная тема изменилась и событие было опубликовано.</returns>
    public bool ReplaceThemes(IEnumerable<Theme>? themes, string? activeThemeId = null)
    {
        ArgumentNullException.ThrowIfNull(themes);

        var normalized = Normalize(themes);
        Theme? previous;
        Theme selected;

        lock (_sync)
        {
            previous = _activeTheme;
            _themes = normalized;

            var candidate = activeThemeId is not null ? Find(normalized, activeThemeId) : Find(normalized, previous.Id);
            selected = candidate ?? (normalized.Length > 0 ? normalized[0] : Theme.CreateDefault());

            if (selected.Id == previous.Id)
            {
                return false;
            }

            _activeTheme = selected;
        }

        _eventManager?.Publish(new ThemeChanged(selected, previous.Id));
        return true;
    }

    /// <summary>
    /// Убирает повторяющиеся идентификаторы и сортирует темы. Темы без идентификатора
    /// отбрасываются: без идентификатора тему нельзя ни выбрать, ни сохранить в настройках.
    /// </summary>
    private static Theme[] Normalize(IEnumerable<Theme>? themes)
    {
        if (themes is null)
        {
            return [];
        }

        var byId = new Dictionary<string, Theme>(StringComparer.OrdinalIgnoreCase);

        foreach (var theme in themes)
        {
            ArgumentNullException.ThrowIfNull(theme, nameof(themes));

            if (string.IsNullOrWhiteSpace(theme.Id))
            {
                continue;
            }

            // Первая тема с идентификатором выигрывает: порядок каталога не должен
            // менять то, какая тема доступна под этим именем.
            byId.TryAdd(theme.Id, theme);
        }

        return [.. byId.Values.OrderBy(static theme => theme.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static theme => theme.Id, StringComparer.Ordinal)];
    }

    private static Theme? Find(Theme[] themes, string themeId) =>
        themes.FirstOrDefault(theme => string.Equals(theme.Id, themeId, StringComparison.OrdinalIgnoreCase));

    private static Theme? Resolve(Theme[] themes, string? themeId) =>
        themeId is null ? null : Find(themes, themeId);
}
