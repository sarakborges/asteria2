namespace Asteria.Content.Storage;

/// <summary>
/// Larger local palette with identity hash-map lookup.
/// </summary>
internal sealed class HashMapPalette<T> : ILocalPalette<T> where T : class
{
    private readonly List<T> _values = new();
    private readonly Dictionary<T, int> _indices =
        new(ReferenceEqualityComparer.Instance);
    private readonly int _bitsPerEntry;

    public int Count => _values.Count;

    public HashMapPalette(int bitsPerEntry)
    {
        if (bitsPerEntry is < 5 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerEntry));
        }

        _bitsPerEntry = bitsPerEntry;
    }

    public int IdFor(T value, IPaletteResize<T> resize)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(resize);

        if (_indices.TryGetValue(value, out int existingIndex))
        {
            return existingIndex;
        }

        if (_values.Count < (1 << _bitsPerEntry))
        {
            int index = _values.Count;
            _values.Add(value);
            _indices.Add(value, index);
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
        var copy = new HashMapPalette<T>(_bitsPerEntry);
        foreach (T value in _values)
        {
            int index = copy._values.Count;
            copy._values.Add(value);
            copy._indices.Add(value, index);
        }

        return copy;
    }
}
