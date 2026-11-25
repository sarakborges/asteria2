namespace Asteria.Serialization.Nbt;

/// <summary>Mutable 64-bit integer array NBT collection (tag ID 12).</summary>
public sealed class LongArrayTag : CollectionTag
{
    private const long SELF_SIZE_IN_BYTES = 24;
    private long[] _data;

    public static TagType<LongArrayTag> TYPE { get; } =
        TagType<LongArrayTag>.VariableSize(
            12,
            "LONG[]",
            "TAG_Long_Array",
            static (input, accounter) =>
            {
                accounter.AccountBytes(SELF_SIZE_IN_BYTES);
                int length = ReadLength(input);
                accounter.AccountBytes(sizeof(long), length);
                var values = new long[length];
                for (int index = 0; index < length; index++)
                    values[index] = input.ReadLong();
                return new LongArrayTag(values);
            },
            static (input, count, _) =>
            {
                for (int index = 0; index < count; index++)
                {
                    int length = ReadLength(input);
                    SkipPayload(input, checked((long)length * sizeof(long)));
                }
            });

    public LongArrayTag(long[] data) =>
        _data = data ?? throw new ArgumentNullException(nameof(data));

    public long[] Data => _data;

    public byte GetId() => 12;
    public TagType GetTagType() => TYPE;

    public void Write(NbtDataOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.WriteInt(_data.Length);
        foreach (long value in _data)
            output.WriteLong(value);
    }

    public Tag Copy() => new LongArrayTag((long[])_data.Clone());

    public long SizeInBytes() =>
        SELF_SIZE_IN_BYTES + sizeof(long) * (long)_data.Length;

    public long[] AsLongArray() => _data;

    public int Size() => _data.Length;

    public Tag Get(int index) =>
        LongTag.ValueOf(_data[index]);

    public bool SetTag(int index, Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (tag is not LongTag longTag || (uint)index >= (uint)_data.Length)
            return false;

        _data[index] = longTag.Value;
        return true;
    }

    public bool AddTag(int index, Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (tag is not LongTag longTag || (uint)index > (uint)_data.Length)
            return false;

        var expanded = new long[checked(_data.Length + 1)];
        Array.Copy(_data, 0, expanded, 0, index);
        expanded[index] = longTag.Value;
        Array.Copy(_data, index, expanded, index + 1, _data.Length - index);
        _data = expanded;
        return true;
    }

    public Tag Remove(int index)
    {
        long removed = _data[index];
        var reduced = new long[_data.Length - 1];
        Array.Copy(_data, 0, reduced, 0, index);
        Array.Copy(_data, index + 1, reduced, index, reduced.Length - index);
        _data = reduced;
        return LongTag.ValueOf(removed);
    }

    public void Clear() =>
        _data = Array.Empty<long>();

    public override string ToString() =>
        "[L;" + string.Join(",", _data.Select(static value =>
            value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "L")) + "]";

    private static int ReadLength(NbtDataInput input)
    {
        int length = input.ReadInt();
        if (length < 0)
            throw new NbtFormatException($"Long array length cannot be negative: {length}");
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
