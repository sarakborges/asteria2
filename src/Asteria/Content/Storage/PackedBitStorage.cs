namespace Asteria.Content.Storage;

/// <summary>
/// Fixed-size packed integer storage using the same per-long layout as Minecraft SimpleBitStorage.
/// Values never straddle a 64-bit word; unused high bits remain padding.
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
        if (bitsPerEntry is < 1 or > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bitsPerEntry), "Bits per entry must be between 1 and 31.");
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

    public int Get(int index)
    {
        ValidateIndex(index);

        int cell = index / _valuesPerLong;
        int bitOffset = (index % _valuesPerLong) * BitsPerEntry;
        return (int)((_data[cell] >> bitOffset) & _mask);
    }

    public void Set(int index, int value)
    {
        ValidateIndex(index);

        if (value < 0 || ((ulong)value & ~_mask) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, $"Value does not fit in {BitsPerEntry} bits.");
        }

        int cell = index / _valuesPerLong;
        int bitOffset = (index % _valuesPerLong) * BitsPerEntry;
        ulong shiftedMask = _mask << bitOffset;
        ulong shiftedValue = (ulong)value << bitOffset;

        _data[cell] = (_data[cell] & ~shiftedMask) | shiftedValue;
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

    public static int RequiredBitsForValueCount(int valueCount)
    {
        if (valueCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valueCount));
        }

        if (valueCount == 1)
        {
            return 1;
        }

        return System.Numerics.BitOperations.Log2((uint)(valueCount - 1)) + 1;
    }

    private void ValidateIndex(int index)
    {
        if ((uint)index >= (uint)Size)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Storage index is out of range.");
        }
    }
}
