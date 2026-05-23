using System.Runtime.InteropServices;

namespace SimpleFramework.ECS;

internal interface IComponentColumn
{
    Type ComponentType { get; }

    int Count { get; }

    void AddBoxed(IComponent component);

    void SetBoxed(int row, IComponent component);

    IComponent GetBoxed(int row);

    void RemoveAtSwapBack(int row);
}

internal sealed class ComponentColumn<T> : IComponentColumn
    where T : IComponent
{
    private readonly List<T> _items = new();

    public Type ComponentType => typeof(T);

    public int Count => _items.Count;

    public void Add(T component)
    {
        _items.Add(component);
    }

    public void AddBoxed(IComponent component)
    {
        _items.Add((T)component);
    }

    public void SetBoxed(int row, IComponent component)
    {
        _items[row] = (T)component;
    }

    public IComponent GetBoxed(int row)
    {
        return _items[row];
    }

    public ref T GetRef(int row)
    {
        return ref CollectionsMarshal.AsSpan(_items)[row];
    }

    public void RemoveAtSwapBack(int row)
    {
        var last = _items.Count - 1;
        if (row != last)
        {
            _items[row] = _items[last];
        }

        _items.RemoveAt(last);
    }
}