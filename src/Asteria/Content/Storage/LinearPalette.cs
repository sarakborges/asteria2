namespace Asteria.Content.Storage;

/// <summary>
/// Small local palette backed by the fixed-size array used by Minecraft LinearPalette.
/// </summary>
internal sealed class LinearPalette<T> : ILocalPalette<T> where T : class
{
    private readonly T?[] _values;
    private readonly int _bitsPerEntry;
    private int _size;

    public int Count => _size;

    public LinearPalette(int bitsPerEntry)
    {
        if (bitsPerEntry is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerEntry));
        }

        _bitsPerEntry = bitsPerEntry;
        _values = new T[1 << bitsPerEntry];
    }

    private LinearPalette(T?[] values, int bitsPerEntry, int size)
    {
        _values = values;
        _bitsPerEntry = bitsPerEntry;
        _size = size;
    }

    public int IdFor(T value, IPaletteResize<T> resize)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(resize);

        for (int index = 0; index < _size; index++)
        {
            if (ReferenceEquals(_values[index], value))
            {
                return index;
            }
        }

        int newIndex = _size++;
        if (newIndex < _values.Length)
        {
            _values[newIndex] = value;
            return newIndex;
        }

        return resize.OnResize(_bitsPerEntry + 1, value);
    }

    public T ValueFor(int id)
    {
        if ((uint)id < (uint)_size && _values[id] is T value)
        {
            return value;
        }

        throw new MissingPaletteEntryException(id);
    }

    public bool MaybeHas(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        for (int index = 0; index < _size; index++)
        {
            if (predicate(_values[index]!))
            {
                return true;
            }
        }

        return false;
    }

    public void ForEach(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        for (int index = 0; index < _size; index++)
        {
            action(_values[index]!);
        }
    }

    public IPalette<T> Copy() =>
        new LinearPalette<T>((T?[])_values.Clone(), _bitsPerEntry, _size);
}
