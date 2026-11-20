using System;

namespace Asteria.Content.Storage;

/// <summary>
/// Fixed-width packed storage following the reference bit-storage contract.
/// Entries never straddle 64-bit word boundaries.
/// </summary>
public sealed class PackedBitStorage : IBitStorage
{
    /// <summary>
    /// Thrown when supplied packed data does not have the exact word count required by the storage.
    /// Reports invalid packed-data length during storage initialization.
    /// </summary>
    public sealed class InitializationException : Exception
    {
        internal InitializationException(string message)
            : base(message)
        {
        }
    }

    private static readonly int[] Magic = new int[]
    {
        -1, -1, 0, int.MinValue, 0, 0, 0x55555555, 0x55555555, 0, int.MinValue, 0, 1, 0x33333333, 0x33333333, 0, 0x2AAAAAAA, 0x2AAAAAAA, 0, 0x24924924, 0x24924924, 0, int.MinValue, 0, 2, 0x1C71C71C, 0x1C71C71C, 0, 0x19999999, 0x19999999, 0, 390451572, 390451572, 0, 0x15555555, 0x15555555, 0, 0x13B13B13, 0x13B13B13, 0, 306783378, 306783378, 0, 0x11111111, 0x11111111, 0, int.MinValue, 0, 3, 0xF0F0F0F, 0xF0F0F0F, 0, 0xE38E38E, 0xE38E38E, 0, 226050910, 226050910, 0, 0xCCCCCCC, 0xCCCCCCC, 0, 0xC30C30C, 0xC30C30C, 0, 195225786, 195225786, 0, 186737708, 186737708, 0, 0xAAAAAAA, 0xAAAAAAA, 0, 171798691, 171798691, 0, 0x9D89D89, 0x9D89D89, 0, 159072862, 159072862, 0, 0x9249249, 0x9249249, 0, 148102320, 148102320, 0, 0x8888888, 0x8888888, 0, 138547332, 138547332, 0, int.MinValue, 0, 4, 130150524, 130150524, 0, 0x7878787, 0x7878787, 0, 0x7507507, 0x7507507, 0, 0x71C71C7, 0x71C71C7, 0, 116080197, 116080197, 0, 113025455, 113025455, 0, 0x6906906, 0x6906906, 0, 0x6666666, 0x6666666, 0, 104755299, 104755299, 0, 0x6186186, 0x6186186, 0, 99882960, 99882960, 0, 97612893, 97612893, 0, 0x5B05B05, 0x5B05B05, 0, 93368854, 93368854, 0, 91382282, 91382282, 0, 0x5555555, 0x5555555, 0, 87652393, 87652393, 0, 85899345, 85899345, 0, 0x5050505, 0x5050505, 0, 0x4EC4EC4, 0x4EC4EC4, 0, 81037118, 81037118, 0, 79536431, 79536431, 0, 78090314, 78090314, 0, 0x4924924, 0x4924924, 0, 75350303, 75350303, 0, 74051160, 74051160, 0, 72796055, 72796055, 0, 0x4444444, 0x4444444, 0, 70409299, 70409299, 0, 69273666, 69273666, 0, 0x4104104, 0x4104104, 0, int.MinValue, 0, 5
    };

    private readonly ulong[] _data;
    private readonly ulong _mask;
    private readonly int _valuesPerLong;
    private readonly uint _divideMul;
    private readonly uint _divideAdd;
    private readonly int _divideShift;

    public int BitsPerEntry { get; }
    public int Size { get; }
    public int LongCount => _data.Length;

    public PackedBitStorage(int bitsPerEntry, int size)
        : this(bitsPerEntry, size, (ulong[]?)null)
    {
    }

    public PackedBitStorage(int bitsPerEntry, int size, int[] values)
        : this(bitsPerEntry, size)
    {
        ArgumentNullException.ThrowIfNull(values);

        int inputOffset;
        int outputIndex = 0;
        for (inputOffset = 0; inputOffset <= size - _valuesPerLong; inputOffset += _valuesPerLong)
        {
            ulong packedValue = 0;
            for (int indexInLong = _valuesPerLong - 1; indexInLong >= 0; indexInLong--)
            {
                packedValue <<= bitsPerEntry;
                packedValue |= (ulong)values[inputOffset + indexInLong] & _mask;
            }

            _data[outputIndex++] = packedValue;
        }

        int remainderCount = size - inputOffset;
        if (remainderCount > 0)
        {
            ulong lastPackedValue = 0;
            for (int indexInLong = remainderCount - 1; indexInLong >= 0; indexInLong--)
            {
                lastPackedValue <<= bitsPerEntry;
                lastPackedValue |= (ulong)values[inputOffset + indexInLong] & _mask;
            }

            _data[outputIndex] = lastPackedValue;
        }
    }

    public PackedBitStorage(int bitsPerEntry, int size, ulong[]? data)
    {
        if (bitsPerEntry is < 1 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerEntry), "Bits per entry must be between 1 and 32.");
        }

        BitsPerEntry = bitsPerEntry;
        Size = size;
        _mask = (1UL << bitsPerEntry) - 1UL;
        _valuesPerLong = 64 / bitsPerEntry;

        int magicIndex = 3 * (_valuesPerLong - 1);
        _divideMul = unchecked((uint)Magic[magicIndex]);
        _divideAdd = unchecked((uint)Magic[magicIndex + 1]);
        _divideShift = Magic[magicIndex + 2];

        int requiredLength = (size + _valuesPerLong - 1) / _valuesPerLong;
        if (data is not null)
        {
            if (data.Length != requiredLength)
            {
                throw new InitializationException(
                    $"Invalid length given for storage, got: {data.Length} but expected: {requiredLength}");
            }

            _data = data;
        }
        else
        {
            _data = new ulong[requiredLength];
        }
    }

    public int GetAndSet(int index, int value)
    {
        ValidateIndex(index);
        ValidateValue(value);

        int cell = CellIndex(index);
        ulong cellValue = _data[cell];
        int bitOffset = (index - cell * _valuesPerLong) * BitsPerEntry;
        int previous = (int)((cellValue >> bitOffset) & _mask);
        _data[cell] = (cellValue & ~(_mask << bitOffset)) | (((ulong)value & _mask) << bitOffset);
        return previous;
    }

    public void Set(int index, int value)
    {
        ValidateIndex(index);
        ValidateValue(value);

        int cell = CellIndex(index);
        ulong cellValue = _data[cell];
        int bitOffset = (index - cell * _valuesPerLong) * BitsPerEntry;
        _data[cell] = (cellValue & ~(_mask << bitOffset)) | (((ulong)value & _mask) << bitOffset);
    }

    public void Fill(int value)
    {
        ValidateValue(value);

        ulong cellValue = (ulong)value & _mask;
        int offset = BitsPerEntry;
        do
        {
            cellValue |= cellValue << offset;
        }
        while ((offset <<= 1) < 64);

        Array.Fill(_data, cellValue);
    }

    public int Get(int index)
    {
        ValidateIndex(index);

        int cell = CellIndex(index);
        ulong cellValue = _data[cell];
        int bitOffset = (index - cell * _valuesPerLong) * BitsPerEntry;
        return (int)((cellValue >> bitOffset) & _mask);
    }

    public ulong[] GetRaw() => _data;

    public ulong[] GetRawCopy() => (ulong[])_data.Clone();

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
                cellValue >>= BitsPerEntry;
                if (++count >= Size)
                {
                    return;
                }
            }
        }
    }

    public void Unpack(int[] output)
    {
        ArgumentNullException.ThrowIfNull(output);

        int outputOffset = 0;
        for (int wordIndex = 0; wordIndex < _data.Length - 1; wordIndex++)
        {
            ulong cellValue = _data[wordIndex];
            for (int valueIndex = 0; valueIndex < _valuesPerLong; valueIndex++)
            {
                output[outputOffset + valueIndex] = (int)(cellValue & _mask);
                cellValue >>= BitsPerEntry;
            }

            outputOffset += _valuesPerLong;
        }

        int remainder = Size - outputOffset;
        if (remainder > 0)
        {
            ulong cellValue = _data[^1];
            for (int valueIndex = 0; valueIndex < remainder; valueIndex++)
            {
                output[outputOffset + valueIndex] = (int)(cellValue & _mask);
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

    public PackedBitStorage Copy() =>
        new(BitsPerEntry, Size, (ulong[])_data.Clone());

    IBitStorage IBitStorage.Copy() => Copy();

    public static int RequiredBitsForValueCount(int valueCount)
    {
        if (valueCount <= 1)
        {
            return 0;
        }

        return System.Numerics.BitOperations.Log2((uint)(valueCount - 1)) + 1;
    }

    private int CellIndex(int index)
    {
        ulong divided = (ulong)index * _divideMul + _divideAdd;
        return (int)(divided >> 32 >> _divideShift);
    }

    private void ValidateIndex(int index)
    {
        if ((uint)index >= (uint)Size)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Storage index is out of range.");
        }
    }

    private void ValidateValue(int value)
    {
        if (value < 0 || ((ulong)value & ~_mask) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Value does not fit in {BitsPerEntry} bits.");
        }
    }
}
