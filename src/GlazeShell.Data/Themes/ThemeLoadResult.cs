using GlazeShell.Core.Models;

namespace GlazeShell.Data.Themes;

/// <summary>
/// Результат чтения каталога тем.
/// </summary>
/// <remarks>
/// Отличается от <c>PersistenceLoadResult</c> намеренно: темы независимы друг от друга,
/// поэтому непригодная тема отбрасывается, а не отправляет весь набор в recovery.
/// </remarks>
public sealed record ThemeLoadResult
{
    public ThemeLoadResult(IEnumerable<Theme> themes, IEnumerable<string> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(themes);
        ArgumentNullException.ThrowIfNull(diagnostics);

        Themes = themes.ToArray();
        Diagnostics = diagnostics.ToArray();
    }

    /// <summary>
    /// Темы, которые удалось загрузить. Порядок детерминированный и не зависит
    /// от порядка файлов в каталоге.
    /// </summary>
    public IReadOnlyList<Theme> Themes { get; }

    /// <summary>
    /// Понятные описания отклонённых тем и пропущенных файлов. Не содержат secrets.
    /// </summary>
    public IReadOnlyList<string> Diagnostics { get; }

    /// <summary>
    /// <c>true</c>, если каталог отсутствует или в нём не оказалось ни одной пригодной темы.
    /// В этом случае действует встроенная тема по умолчанию.
    /// </summary>
    public bool UsedFallback => Themes.Count == 0;
}
