namespace Asteria.Content.Storage;

/// <summary>
/// Small local palette with linear identity lookup.
/// </summary>
internal sealed class LinearPalette<T> : ILocalPalette<T> where T : class
{
    private readonly List<T> _values;

    public int Count => _values.Count;
    public IReadOnlyList<T> Values => _values;

    public LinearPalette(T firstValue)
    {
        ArgumentNullException.ThrowIfNull(firstValue);
        _values = new List<T> { firstValue };
    }

    public bool TryGetIndex(T value, out int index)
    {
        for (int candidate = 0; candidate < _values.Count; candidate++)
        {
            if (ReferenceEquals(_values[candidate], value))
            {
                index = candidate;
                return true;
            }
        }

        index = -1;
        return false;
    }

    public T GetValue(int index) => _values[index];

    public int Add(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (TryGetIndex(value, out int existingIndex))
        {
            return existingIndex;
        }

        int index = _values.Count;
        _values.Add(value);
        return index;
    }
}
