namespace Asteria.Content.Storage;

/// <summary>
/// Small local palette with linear identity lookup.
/// </summary>
internal sealed class LinearPalette<T> : ILocalPalette<T> where T : class
{
    private readonly List<T> _values = new();
    private readonly int _bitsPerEntry;

    public int Count => _values.Count;

    public LinearPalette(int bitsPerEntry)
    {
        if (bitsPerEntry is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerEntry));
        }

        _bitsPerEntry = bitsPerEntry;
    }

    public int IdFor(T value, IPaletteResize<T> resize)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(resize);

        for (int index = 0; index < _values.Count; index++)
        {
            if (ReferenceEquals(_values[index], value))
            {
                return index;
            }
        }

        if (_values.Count < (1 << _bitsPerEntry))
        {
            int index = _values.Count;
            _values.Add(value);
            return index;
        }

        return resize.OnResize(_bitsPerEntry + 1, value);
    }

    public T ValueFor(int id)
    {
        if ((uint)id >= (uint)_values.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "The local palette ID does not exist.");
        }

        return _values[id];
    }

    public bool MaybeHas(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _values.Any(predicate);
    }

    public void ForEach(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        foreach (T value in _values)
        {
            action(value);
        }
    }

    public IPalette<T> Copy()
    {
        var copy = new LinearPalette<T>(_bitsPerEntry);
        copy._values.AddRange(_values);
        return copy;
    }
}
