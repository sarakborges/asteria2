namespace Asteria.Content.Storage;

/// <summary>
/// Larger local palette with identity hash-map lookups.
/// </summary>
internal sealed class HashMapPalette<T> : ILocalPalette<T> where T : class
{
    private readonly List<T> _values = new();
    private readonly Dictionary<T, int> _indices =
        new(ReferenceEqualityComparer.Instance);

    public int Count => _values.Count;
    public IReadOnlyList<T> Values => _values;

    public HashMapPalette(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        foreach (T value in values)
        {
            Add(value);
        }
    }

    public bool TryGetIndex(T value, out int index)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _indices.TryGetValue(value, out index);
    }

    public T GetValue(int index) => _values[index];

    public int Add(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (_indices.TryGetValue(value, out int existingIndex))
        {
            return existingIndex;
        }

        int index = _values.Count;
        _values.Add(value);
        _indices.Add(value, index);
        return index;
    }
}
