namespace Asteria.Content.Storage;

/// <summary>
/// Palette used while every position stores the same value.
/// </summary>
internal sealed class SingleValuePalette<T> : IPalette<T> where T : class
{
    private T? _value;

    public int Count => _value is null ? 0 : 1;

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
        if (id != 0 || _value is null)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "The single-value palette only contains ID zero.");
        }

        return _value;
    }

    public bool MaybeHas(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _value is not null && predicate(_value);
    }

    public void ForEach(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_value is not null)
        {
            action(_value);
        }
    }

    public IPalette<T> Copy()
    {
        var copy = new SingleValuePalette<T>();
        copy._value = _value;
        return copy;
    }
}
