namespace Asteria.Serialization.Nbt;

/// <summary>Mutable 32-bit integer array NBT collection (tag ID 11).</summary>
public sealed class IntArrayTag : CollectionTag, IEquatable<IntArrayTag>
{
    private const long SELF_SIZE_IN_BYTES = 24;
    private int[] _data;

    public static TagType<IntArrayTag> TYPE { get; } =
        TagType<IntArrayTag>.VariableSize(
            11,
            "INT[]",
            "TAG_Int_Array",
            static (input, accounter) =>
            {
                accounter.AccountBytes(SELF_SIZE_IN_BYTES);
                int length = ReadLength(input);
                accounter.AccountBytes(sizeof(int), length);
                var values = new int[length];
                for (int index = 0; index < length; index++)
                    values[index] = input.ReadInt();
                return new IntArrayTag(values);
            },
            static (input, count, _) =>
            {
                for (int index = 0; index < count; index++)
                {
                    int length = ReadLength(input);
                    SkipPayload(input, checked((long)length * sizeof(int)));
                }
            });

    public IntArrayTag(int[] data) =>
        _data = data ?? throw new ArgumentNullException(nameof(data));

    public int[] Data => _data;

    public byte GetId() => 11;
    public TagType GetTagType() => TYPE;

    public void Write(NbtDataOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.WriteInt(_data.Length);
        foreach (int value in _data)
            output.WriteInt(value);
    }

    public Tag Copy() => new IntArrayTag((int[])_data.Clone());

    public long SizeInBytes() =>
        SELF_SIZE_IN_BYTES + sizeof(int) * (long)_data.Length;

    public int[] AsIntArray() => _data;

    public int Size() => _data.Length;

    public bool IsEmpty() => _data.Length == 0;

    public Tag Get(int index) =>
        IntTag.ValueOf(_data[index]);

    public bool SetTag(int index, Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (tag is not IntTag intTag || (uint)index >= (uint)_data.Length)
            return false;

        _data[index] = intTag.Value;
        return true;
    }

    public bool AddTag(int index, Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (tag is not IntTag intTag || (uint)index > (uint)_data.Length)
            return false;

        var expanded = new int[checked(_data.Length + 1)];
        Array.Copy(_data, 0, expanded, 0, index);
        expanded[index] = intTag.Value;
        Array.Copy(_data, index, expanded, index + 1, _data.Length - index);
        _data = expanded;
        return true;
    }

    public Tag Remove(int index)
    {
        int removed = _data[index];
        var reduced = new int[_data.Length - 1];
        Array.Copy(_data, 0, reduced, 0, index);
        Array.Copy(_data, index + 1, reduced, index, reduced.Length - index);
        _data = reduced;
        return IntTag.ValueOf(removed);
    }

    public void Clear() =>
        _data = Array.Empty<int>();

    public bool Equals(IntArrayTag? other) =>
        other is not null && _data.AsSpan().SequenceEqual(other._data);

    public override bool Equals(object? obj) =>
        obj is IntArrayTag other && Equals(other);

    public override int GetHashCode()
    {
        int result = 1;
        foreach (int value in _data)
            result = unchecked(31 * result + value);
        return result;
    }

    public override string ToString() =>
        "[I;" + string.Join(",", _data.Select(static value =>
            value.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";

    private static int ReadLength(NbtDataInput input)
    {
        int length = input.ReadInt();
        if (length < 0)
            throw new NbtFormatException($"Int array length cannot be negative: {length}");
        return length;
    }

    private static void SkipPayload(NbtDataInput input, long byteLength)
    {
        while (byteLength > 0)
        {
            int requested = (int)Math.Min(byteLength, int.MaxValue);
            int skipped = input.SkipBytes(requested);
            if (skipped != requested)
                throw new EndOfStreamException();
            byteLength -= skipped;
        }
    }
}
