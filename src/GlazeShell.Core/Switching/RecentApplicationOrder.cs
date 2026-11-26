namespace GlazeShell.Core.Switching;

/// <summary>
/// Порядок недавно использованных приложений (MRU). Обновляется при каждой смене фокуса
/// в системе, а не только через переключатель: иначе Alt+Tab после обычного клика по окну
/// вёл бы не в то приложение, в котором пользователь работал последним.
/// </summary>
/// <remarks>
/// Хранятся только идентификаторы: снимок окон и имена пересобираются при открытии
/// переключателя, поэтому удалённые приложения и запущенные без окон не занимают список
/// навсегда. Предел длины не даёт списку расти бесконечно за долгую сессию.
/// </remarks>
public sealed class RecentApplicationOrder
{
    /// <summary>Сколько последних приложений помнится.</summary>
    public const int MaxItems = 32;

    private readonly List<string> _ids = [];

    /// <summary>Идентификаторы от самого недавнего к самому старому.</summary>
    public IReadOnlyList<string> Ids => _ids;

    /// <summary>
    /// Ставит приложение первым в списке. Возвращает <see langword="true"/>, если порядок
    /// изменился (наведение на то же приложение порядок не трогает).
    /// </summary>
    public bool Touch(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var existing = _ids.FindIndex(value => string.Equals(value, id, StringComparison.Ordinal));

        if (existing == 0)
        {
            return false;
        }

        if (existing > 0)
        {
            _ids.RemoveAt(existing);
        }

        _ids.Insert(0, id);

        if (_ids.Count > MaxItems)
        {
            _ids.RemoveRange(MaxItems, _ids.Count - MaxItems);
        }

        return true;
    }

    /// <summary>Забывает приложение (оно закрылось).</summary>
    public bool Remove(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var index = _ids.FindIndex(value => string.Equals(value, id, StringComparison.Ordinal));

        if (index < 0)
        {
            return false;
        }

        _ids.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Упорядочивает снимок приложений: сначала запомненные по давности работы, затем
    /// остальные в порядке снимка (Z-порядок окон). Неизвестные приложения не теряются.
    /// </summary>
    public IReadOnlyList<T> Order<T>(IReadOnlyList<T> applications, Func<T, string> getId)
    {
        ArgumentNullException.ThrowIfNull(applications);
        ArgumentNullException.ThrowIfNull(getId);

        var remaining = new Dictionary<string, T>(applications.Count, StringComparer.Ordinal);

        foreach (var application in applications)
        {
            remaining.TryAdd(getId(application), application);
        }

        var ordered = new List<T>(applications.Count);

        foreach (var id in _ids)
        {
            if (remaining.Remove(id, out var application))
            {
                ordered.Add(application);
            }
        }

        foreach (var application in applications)
        {
            var id = getId(application);

            if (remaining.Remove(id, out var pending))
            {
                ordered.Add(pending);
            }
        }

        return ordered;
    }
}
