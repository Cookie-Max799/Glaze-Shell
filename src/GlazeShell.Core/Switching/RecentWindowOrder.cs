namespace GlazeShell.Core.Switching;

public sealed class RecentWindowOrder
{
    private const int Capacity = 64;
    private readonly List<long> _handles = [];

    public void Touch(long handle)
    {
        if (handle == 0)
        {
            return;
        }

        _handles.Remove(handle);
        _handles.Insert(0, handle);

        if (_handles.Count > Capacity)
        {
            _handles.RemoveAt(Capacity);
        }
    }

    public IReadOnlyList<SwitchableWindow> Order(IReadOnlyList<SwitchableWindow> windows)
    {
        var ranks = new Dictionary<long, int>(_handles.Count);

        for (var i = 0; i < _handles.Count; i++)
        {
            ranks.Add(_handles[i], i);
        }

        return windows.OrderBy(window => ranks.GetValueOrDefault(window.Handle, int.MaxValue)).ToArray();
    }
}
