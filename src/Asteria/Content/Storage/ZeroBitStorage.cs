namespace Asteria.Content.Storage;

/// <summary>
/// Zero-width storage used by the single-value palette configuration.
/// Every valid entry is zero and no packed words are allocated.
/// </summary>
public sealed class ZeroBitStorage : IBitStorage
{
    public const int BitCount = 0;

    public int BitsPerEntry => BitCount;
    public int Size { get; }
    public int LongCount => 0;

    public ulong[] GetRaw() => Array.Empty<ulong>();

    public ZeroBitStorage(int size)
    {
        Size = size;
    }

    public int GetAndSet(int index, int value)
    {
        ValidateIndex(index);
        ValidateValue(value);
        return 0;
    }

    public void Set(int index, int value)
    {
        ValidateIndex(index);
        ValidateValue(value);
    }

    public void Fill(int value) => ValidateValue(value);

    public int Get(int index)
    {
        ValidateIndex(index);
        return 0;
    }

    public void GetAll(Action<int> output)
    {
        ArgumentNullException.ThrowIfNull(output);

        for (int index = 0; index < Size; index++)
        {
            output(0);
        }
    }

    public void Unpack(int[] output)
    {
        ArgumentNullException.ThrowIfNull(output);
        Array.Fill(output, 0, 0, Size);
    }

    public ZeroBitStorage Copy() => this;

    IBitStorage IBitStorage.Copy() => Copy();

    private void ValidateIndex(int index)
    {
        if ((uint)index >= (uint)Size)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Storage index is out of range.");
        }
    }

    private static void ValidateValue(int value)
    {
        if (value != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Zero-bit storage can only contain zero.");
        }
    }
}
