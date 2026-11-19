namespace Asteria.Content.Storage;

/// <summary>
/// Larger local palette backed by Minecraft's incremental identity ID map.
/// </summary>
internal sealed class HashMapPalette<T> : ILocalPalette<T> where T : class
{
    private readonly CrudeIncrementalIntIdentityHashBiMap<T> _values;
    private readonly int _bitsPerEntry;

    public int Count => _values.Count;

    public HashMapPalette(int bitsPerEntry)
    {
        if (bitsPerEntry is < 5 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerEntry));
        }

        _bitsPerEntry = bitsPerEntry;
        _values = CrudeIncrementalIntIdentityHashBiMap<T>.Create(1 << bitsPerEntry);
    }

    private HashMapPalette(
        int bitsPerEntry,
        CrudeIncrementalIntIdentityHashBiMap<T> values)
    {
        _bitsPerEntry = bitsPerEntry;
        _values = values;
    }

    public int IdFor(T value, IPaletteResize<T> resize)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(resize);

        int id = _values.GetId(value);
        if (id == -1 && (id = _values.Add(value)) >= (1 << _bitsPerEntry))
        {
            id = resize.OnResize(_bitsPerEntry + 1, value);
        }

        return id;
    }

    public T ValueFor(int id) =>
        _values.ById(id) ?? throw new MissingPaletteEntryException(id);

    public bool MaybeHas(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        for (int id = 0; id < _values.Count; id++)
        {
            if (predicate(_values.ById(id)!))
            {
                return true;
            }
        }

        return false;
    }

    public void ForEach(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        for (int id = 0; id < _values.Count; id++)
        {
            action(_values.ById(id)!);
        }
    }

    public List<T> GetEntries() => _values.GetEntries();

    public IPalette<T> Copy() =>
        new HashMapPalette<T>(_bitsPerEntry, _values.Copy());
}
