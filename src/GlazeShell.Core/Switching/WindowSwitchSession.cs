namespace GlazeShell.Core.Switching;

public sealed class WindowSwitchSession
{
    private readonly List<SwitchableWindow> _items;

    public WindowSwitchSession(IReadOnlyList<SwitchableWindow> items, long foreground, bool backwards)
    {
        _items = [.. items];
        var current = _items.FindIndex(window => window.Handle == foreground);
        SelectedIndex = _items.Count == 0 ? -1 :
            current < 0 ? (backwards ? _items.Count - 1 : 0) : Wrap(current + (backwards ? -1 : 1));
    }

    public IReadOnlyList<SwitchableWindow> Items => _items;

    public int SelectedIndex { get; private set; }

    public SwitchableWindow? Selected => SelectedIndex < 0 ? null : _items[SelectedIndex];

    public bool Move(int delta) => _items.Count > 0 && Select(Wrap(SelectedIndex + delta));

    public bool Select(int index)
    {
        if (index < 0 || index >= _items.Count || index == SelectedIndex)
        {
            return false;
        }

        SelectedIndex = index;
        return true;
    }

    public void RemoveSelected()
    {
        if (SelectedIndex >= 0)
        {
            _items.RemoveAt(SelectedIndex);
            SelectedIndex = Math.Min(SelectedIndex, _items.Count - 1);
        }
    }

    private int Wrap(int index) => ((index % _items.Count) + _items.Count) % _items.Count;
}
