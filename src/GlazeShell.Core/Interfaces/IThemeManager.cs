using GlazeShell.Core.Models;

namespace GlazeShell.Core.Interfaces;

public interface IThemeManager
{
    /// <summary>
    /// Загруженные темы в детерминированном порядке: по имени, затем по идентификатору.
    /// Порядок не зависит от порядка файлов в каталоге, поэтому список тем стабилен между запусками.
    /// </summary>
    IReadOnlyList<Theme> GetThemes();

    Theme? GetTheme(string themeId);

    /// <summary>
    /// Действующая тема. Всегда не <c>null</c>: пока ни одна тема не выбрана,
    /// действует встроенная тема по умолчанию, поэтому UI не остаётся без значений.
    /// </summary>
    Theme GetActiveTheme();

    /// <summary>
    /// Выбирает тему. <paramref name="themeId"/> из <c>null</c> или пустой строки
    /// возвращает встроенную тему по умолчанию.
    /// </summary>
    /// <returns>
    /// <c>true</c>, если тема выбрана; <c>false</c>, если идентификатор неизвестен
    /// и активная тема не изменилась.
    /// </returns>
    bool SetActiveTheme(string? themeId);
}
