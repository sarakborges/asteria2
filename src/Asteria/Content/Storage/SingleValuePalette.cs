namespace Asteria.Content.Storage;

/// <summary>
/// Palette used while every position stores the same value.
/// </summary>
internal sealed class SingleValuePalette<T> : IPalette<T> where T : class
{
    private T? _value;

    public int Count => 1;

    public int IdFor(T value, IPaletteResize<T> resize)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(resize);

        if (_value is null)
        {
            _value = value;
            return 0;
        }

        return ReferenceEquals(_value, value) ? 0 : resize.OnResize(1, value);
    }

    public T ValueFor(int id)
    {
        if (_value is null || id != 0)
        {
            throw new InvalidOperationException($"Missing Palette entry for id {id}.");
        }

        return _value;
    }

    public bool MaybeHas(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (_value is null)
        {
            throw new InvalidOperationException("Use of an uninitialized palette");
        }

        return predicate(_value);
    }

    public void ForEach(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action(ValueFor(0));
    }

    public IPalette<T> Copy()
    {
        if (_value is null)
        {
            throw new InvalidOperationException("Use of an uninitialized palette");
        }

        return this;
    }
}
