namespace Asteria.Content.Storage;

/// <summary>
/// Fixed-width storage matching Minecraft SimpleBitStorage's word layout.
/// Entries never straddle 64-bit word boundaries.
/// </summary>
public sealed class PackedBitStorage
{
    private readonly ulong[] _data;
    private readonly ulong _mask;
    private readonly int _valuesPerLong;

    public int BitsPerEntry { get; }
    public int Size { get; }
    public int LongCount => _data.Length;

    public PackedBitStorage(int bitsPerEntry, int size)
    {
        if (bitsPerEntry is < 1 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerEntry), "Bits per entry must be between 1 and 32.");
        }

        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Storage size must be positive.");
        }

        BitsPerEntry = bitsPerEntry;
        Size = size;
        _valuesPerLong = 64 / bitsPerEntry;
        _mask = (1UL << bitsPerEntry) - 1UL;

        int longCount = checked((size + _valuesPerLong - 1) / _valuesPerLong);
        _data = new ulong[longCount];
    }

    private PackedBitStorage(int bitsPerEntry, int size, ulong[] data)
        : this(bitsPerEntry, size)
    {
        if (data.Length != _data.Length)
        {
            throw new ArgumentException("Raw storage has an invalid word count.", nameof(data));
        }

        Array.Copy(data, _data, data.Length);
    }

    public int Get(int index)
    {
        ValidateIndex(index);
        int cell = index / _valuesPerLong;
        int bitOffset = (index % _valuesPerLong) * BitsPerEntry;
        return (int)((_data[cell] >> bitOffset) & _mask);
    }

    public int GetAndSet(int index, int value)
    {
        int previous = Get(index);
        Set(index, value);
        return previous;
    }

    public void Set(int index, int value)
    {
        ValidateIndex(index);

        if (value < 0 || ((ulong)value & ~_mask) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Value does not fit in {BitsPerEntry} bits.");
        }

        int cell = index / _valuesPerLong;
        int bitOffset = (index % _valuesPerLong) * BitsPerEntry;
        ulong shiftedMask = _mask << bitOffset;
        ulong shiftedValue = (ulong)value << bitOffset;
        _data[cell] = (_data[cell] & ~shiftedMask) | shiftedValue;
    }

    public void Fill(int value)
    {
        if (value < 0 || ((ulong)value & ~_mask) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Value does not fit in {BitsPerEntry} bits.");
        }

        ulong cellValue = (ulong)value & _mask;
        int offset = BitsPerEntry;
        do
        {
            cellValue |= cellValue << offset;
        }
        while ((offset <<= 1) < 64);

        Array.Fill(_data, cellValue);
    }

    public void GetAll(Action<int> output)
    {
        ArgumentNullException.ThrowIfNull(output);

        int count = 0;
        foreach (ulong storedValue in _data)
        {
            ulong cellValue = storedValue;
            for (int valueIndex = 0; valueIndex < _valuesPerLong; valueIndex++)
            {
                output((int)(cellValue & _mask));
                if (++count >= Size)
                {
                    return;
                }

                cellValue >>= BitsPerEntry;
            }
        }
    }

    public void CopyTo(PackedBitStorage destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (destination.Size != Size)
        {
            throw new ArgumentException("Source and destination storage must have the same size.", nameof(destination));
        }

        for (int index = 0; index < Size; index++)
        {
            destination.Set(index, Get(index));
        }
    }

    public PackedBitStorage Copy() => new(BitsPerEntry, Size, _data);

    public ulong[] GetRawCopy() => (ulong[])_data.Clone();

    public static int RequiredBitsForValueCount(int valueCount)
    {
        if (valueCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valueCount));
        }

        return valueCount == 1
            ? 0
            : System.Numerics.BitOperations.Log2((uint)(valueCount - 1)) + 1;
    }

    private void ValidateIndex(int index)
    {
        if ((uint)index >= (uint)Size)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Storage index is out of range.");
        }
    }
}
