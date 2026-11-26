namespace GlazeShell.Core.Switching;

/// <summary>
/// Состояние одного показа переключателя приложений: снимок списка, выделенный элемент и
/// раскрытый список окон. Снимок не меняется до выхода из режима, поэтому новые окна,
/// появившиеся во время переключения, не сдвигают выбор.
/// </summary>
/// <remarks>
/// Выделение начинается со второго элемента: первый — приложение, из которого переключатель
/// открыли, а Cmd+Tab сразу выделяет предыдущее. Если приложений меньше двух, выделяется
/// единственное. Перемещение зациклено, поэтому удержание Tab проходит по кругу.
/// </remarks>
public sealed class ApplicationSwitchSession
{
    private readonly List<SwitchableApplication> _items;
    private int _selectedIndex;
    private int _selectedWindowIndex = -1;

    /// <param name="items">Снимок приложений в порядке MRU.</param>
    /// <param name="selectNext">
    /// Выделять второй элемент при открытии. Выключается, когда текущее приложение
    /// не попало в снимок: тогда выделять нечего, кроме первого.
    /// </param>
    public ApplicationSwitchSession(IReadOnlyList<SwitchableApplication> items, bool selectNext = true)
    {
        ArgumentNullException.ThrowIfNull(items);

        _items = [.. items];
        _selectedIndex = _items.Count > 1 && selectNext ? 1 : 0;
    }

    /// <summary>Снимок приложений в порядке MRU.</summary>
    public IReadOnlyList<SwitchableApplication> Items => _items;

    /// <summary>Сколько приложений в снимке.</summary>
    public int Count => _items.Count;

    /// <summary>В снимке нет ни одного приложения.</summary>
    public bool IsEmpty => _items.Count == 0;

    /// <summary>Индекс выделенного приложения.</summary>
    public int SelectedIndex => _selectedIndex;

    /// <summary>Выделенное приложение.</summary>
    public SwitchableApplication? Selected => _items.Count == 0 ? null : _items[_selectedIndex];

    /// <summary>Список окон выделенного приложения раскрыт.</summary>
    public bool WindowsExpanded => _selectedWindowIndex >= 0;

    /// <summary>Индекс выделенного окна внутри приложения или −1.</summary>
    public int SelectedWindowIndex => _selectedWindowIndex;

    /// <summary>Выделенное окно, когда список окон раскрыт.</summary>
    public SwitchableWindow? SelectedWindow =>
        Selected is { } application &&
        WindowsExpanded &&
        _selectedWindowIndex < application.Windows.Count
            ? application.Windows[_selectedWindowIndex]
            : null;

    /// <summary>Передвигает выделение по кругу. Возвращает <see langword="true"/>, если выделение изменилось.</summary>
    public bool Move(int delta)
    {
        if (_items.Count == 0 || delta == 0)
        {
            return false;
        }

        var target = Wrap(_selectedIndex + delta);

        if (target == _selectedIndex)
        {
            return false;
        }

        _selectedIndex = target;
        _selectedWindowIndex = -1;
        return true;
    }

    /// <summary>Выделяет приложение по индексу (наведение или клик).</summary>
    public bool Select(int index)
    {
        if (index < 0 || index >= _items.Count || index == _selectedIndex)
        {
            return false;
        }

        _selectedIndex = index;
        _selectedWindowIndex = -1;
        return true;
    }

    /// <summary>
    /// Раскрывает список окон выделенного приложения или сворачивает его, если он уже
    /// раскрыт. У приложения без окон раскрывать нечего.
    /// </summary>
    public bool ToggleWindows()
    {
        if (WindowsExpanded)
        {
            _selectedWindowIndex = -1;
            return true;
        }

        if (Selected is not { HasWindows: true })
        {
            return false;
        }

        _selectedWindowIndex = 0;
        return true;
    }

    /// <summary>Передвигает выделение по окнам приложения. Работает только в раскрытом списке.</summary>
    public bool MoveWindow(int delta)
    {
        if (!WindowsExpanded || Selected is not { } application || delta == 0)
        {
            return false;
        }

        var count = application.Windows.Count;

        if (count <= 1)
        {
            return false;
        }

        var target = ((_selectedWindowIndex + delta) % count + count) % count;

        if (target == _selectedWindowIndex)
        {
            return false;
        }

        _selectedWindowIndex = target;
        return true;
    }

    /// <summary>
    /// Убирает выделенное приложение из снимка (закрытие по Q) и оставляет выделение на
    /// соседнем элементе. Возвращает удалённую запись или <see langword="null"/>.
    /// </summary>
    public SwitchableApplication? RemoveSelected()
    {
        if (_items.Count == 0)
        {
            return null;
        }

        var removed = _items[_selectedIndex];
        _items.RemoveAt(_selectedIndex);
        _selectedWindowIndex = -1;

        if (_items.Count == 0)
        {
            _selectedIndex = 0;
        }
        else if (_selectedIndex >= _items.Count)
        {
            _selectedIndex = _items.Count - 1;
        }

        return removed;
    }

    private int Wrap(int index) => ((index % _items.Count) + _items.Count) % _items.Count;
}
