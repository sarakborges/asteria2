using Asteria.Content.Registries;

namespace Asteria.Content.Storage;

/// <summary>
/// Compact storage for a fixed number of registry values.
/// Uses a zero-bit single-value palette, a local indirect palette (4-8 bits by default),
/// and a direct global-ID palette when the local palette can no longer grow.
/// A container is designed for one writer at a time.
/// </summary>
public sealed class PalettedContainer<T> where T : class
{
    private readonly IIdMap<T> _idMap;
    private readonly T _defaultValue;
    private readonly int _size;
    private readonly int _minimumLocalBits;
    private readonly int _maximumLocalBits;

    private PaletteMode _mode = PaletteMode.SingleValue;
    private ILocalPalette<T>? _localPalette;
    private PackedBitStorage? _storage;
    private int _bitsPerEntry;

    public int Size => _size;
    public PaletteMode Mode => _mode;
    public int BitsPerEntry => _bitsPerEntry;
    public int PaletteSize => _mode switch
    {
        PaletteMode.SingleValue => 1,
        PaletteMode.Local => _localPalette!.Count,
        PaletteMode.Global => _idMap.Count,
        _ => throw new InvalidOperationException($"Unknown palette mode '{_mode}'.")
    };
    public int StorageLongCount => _storage?.LongCount ?? 0;
    public bool IsSingleValue => _mode == PaletteMode.SingleValue;
    public string? LocalPaletteImplementation =>
        _mode == PaletteMode.Local ? _localPalette!.GetType().Name : null;

    public PalettedContainer(
        IIdMap<T> idMap,
        T defaultValue,
        int size,
        int minimumLocalBits = 4,
        int maximumLocalBits = 8)
    {
        _idMap = idMap ?? throw new ArgumentNullException(nameof(idMap));
        _defaultValue = defaultValue ?? throw new ArgumentNullException(nameof(defaultValue));

        if (!_idMap.IsFrozen)
        {
            throw new InvalidOperationException("The value ID map must be frozen before creating a paletted container.");
        }

        if (_idMap.GetId(_defaultValue) < 0)
        {
            throw new ArgumentException("The default value must exist in the supplied ID map.", nameof(defaultValue));
        }

        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        if (minimumLocalBits is < 1 or > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumLocalBits));
        }

        if (maximumLocalBits < minimumLocalBits || maximumLocalBits > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLocalBits));
        }

        _size = size;
        _minimumLocalBits = minimumLocalBits;
        _maximumLocalBits = maximumLocalBits;
    }

    public T Get(int index)
    {
        ValidateIndex(index);

        if (_mode == PaletteMode.SingleValue)
        {
            return _defaultValue;
        }

        int storedValue = _storage!.Get(index);

        return _mode switch
        {
            PaletteMode.Local => _localPalette!.GetValue(storedValue),
            PaletteMode.Global => _idMap.GetById(storedValue),
            _ => throw new InvalidOperationException($"Unknown palette mode '{_mode}'.")
        };
    }

    /// <summary>
    /// Replaces one value and returns the previous value.
    /// The incoming value must already have a stable ID in the frozen value map.
    /// </summary>
    public T Set(int index, T value)
    {
        ValidateIndex(index);
        ArgumentNullException.ThrowIfNull(value);

        int globalId = _idMap.GetId(value);
        if (globalId < 0)
        {
            throw new ArgumentException("The value is not registered in the supplied ID map.", nameof(value));
        }

        T previous = Get(index);
        if (ReferenceEquals(previous, value))
        {
            return previous;
        }

        switch (_mode)
        {
            case PaletteMode.SingleValue:
                CreateLocalPalette(value, index);
                break;

            case PaletteMode.Local:
                SetLocal(index, value, globalId);
                break;

            case PaletteMode.Global:
                _storage!.Set(index, globalId);
                break;

            default:
                throw new InvalidOperationException($"Unknown palette mode '{_mode}'.");
        }

        return previous;
    }

    /// <summary>
    /// Checks the palette values rather than scanning all stored positions.
    /// In global mode every registered value is potentially present, so this returns true.
    /// </summary>
    public bool MaybeHas(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return _mode switch
        {
            PaletteMode.SingleValue => predicate(_defaultValue),
            PaletteMode.Local => _localPalette!.Values.Any(predicate),
            PaletteMode.Global => true,
            _ => throw new InvalidOperationException($"Unknown palette mode '{_mode}'.")
        };
    }

    private void CreateLocalPalette(T value, int targetIndex)
    {
        _mode = PaletteMode.Local;
        _bitsPerEntry = _minimumLocalBits;
        _localPalette = CreateLocalPaletteForBits(_bitsPerEntry, new[] { _defaultValue });
        _storage = new PackedBitStorage(_bitsPerEntry, _size);

        int localIndex = _localPalette.Add(value);
        _storage.Set(targetIndex, localIndex);
    }

    private void SetLocal(int index, T value, int globalId)
    {
        if (_localPalette!.TryGetIndex(value, out int localIndex))
        {
            _storage!.Set(index, localIndex);
            return;
        }

        int capacity = 1 << _bitsPerEntry;
        if (_localPalette.Count >= capacity)
        {
            if (_bitsPerEntry < _maximumLocalBits)
            {
                GrowLocalPalette(_bitsPerEntry + 1);
            }
            else
            {
                SwitchToGlobalPalette();
                _storage!.Set(index, globalId);
                return;
            }
        }

        localIndex = _localPalette!.Add(value);
        _storage!.Set(index, localIndex);
    }

    private void GrowLocalPalette(int newBitsPerEntry)
    {
        var newStorage = new PackedBitStorage(newBitsPerEntry, _size);
        _storage!.CopyTo(newStorage);

        if (_bitsPerEntry <= 4 && newBitsPerEntry > 4 &&
            _localPalette is LinearPalette<T>)
        {
            _localPalette = new HashMapPalette<T>(_localPalette.Values);
        }

        _storage = newStorage;
        _bitsPerEntry = newBitsPerEntry;
    }

    private static ILocalPalette<T> CreateLocalPaletteForBits(
        int bitsPerEntry,
        IEnumerable<T> initialValues)
    {
        T firstValue = initialValues.First();
        return bitsPerEntry <= 4
            ? new LinearPalette<T>(firstValue)
            : new HashMapPalette<T>(initialValues);
    }

    private void SwitchToGlobalPalette()
    {
        int globalBits = Math.Max(
            _maximumLocalBits + 1,
            PackedBitStorage.RequiredBitsForValueCount(_idMap.Count));

        if (globalBits > 31)
        {
            throw new InvalidOperationException("The global ID map requires more than 31 bits per entry.");
        }

        var globalStorage = new PackedBitStorage(globalBits, _size);
        for (int index = 0; index < _size; index++)
        {
            T value = _localPalette!.GetValue(_storage!.Get(index));
            int globalId = _idMap.GetId(value);

            if (globalId < 0)
            {
                throw new InvalidOperationException("A local palette value no longer exists in the frozen ID map.");
            }

            globalStorage.Set(index, globalId);
        }

        _storage = globalStorage;
        _localPalette = null;
        _bitsPerEntry = globalBits;
        _mode = PaletteMode.Global;
    }

    private void ValidateIndex(int index)
    {
        if ((uint)index >= (uint)_size)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Paletted container index is out of range.");
        }
    }
}
