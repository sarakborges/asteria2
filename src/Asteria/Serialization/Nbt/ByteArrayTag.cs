namespace Asteria.Serialization.Nbt;

/// <summary>Mutable signed-byte array NBT collection (tag ID 7).</summary>
public sealed class ByteArrayTag : CollectionTag, IEquatable<ByteArrayTag>
{
    private const long SELF_SIZE_IN_BYTES = 24;
    private byte[] _data;

    public static TagType<ByteArrayTag> TYPE { get; } =
        TagType<ByteArrayTag>.VariableSize(
            7,
            "BYTE[]",
            "TAG_Byte_Array",
            static (input, accounter) =>
            {
                accounter.AccountBytes(SELF_SIZE_IN_BYTES);
                int length = ReadLength(input);
                accounter.AccountBytes(sizeof(byte), length);
                var values = new byte[length];
                input.ReadFully(values);
                return new ByteArrayTag(values);
            },
            static (input, count, _) =>
            {
                for (int index = 0; index < count; index++)
                {
                    int length = ReadLength(input);
                    SkipPayload(input, length);
                }
            });

    public ByteArrayTag(byte[] data) =>
        _data = data ?? throw new ArgumentNullException(nameof(data));

    public byte[] Data => _data;

    public byte GetId() => 7;
    public TagType GetTagType() => TYPE;

    public void Write(NbtDataOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.WriteInt(_data.Length);
        output.Write(_data);
    }

    public Tag Copy() => new ByteArrayTag((byte[])_data.Clone());

    public long SizeInBytes() =>
        SELF_SIZE_IN_BYTES + sizeof(byte) * (long)_data.Length;

    public byte[] AsByteArray() => _data;

    public int Size() => _data.Length;

    public bool IsEmpty() => _data.Length == 0;

    public Tag Get(int index) =>
        ByteTag.ValueOf(unchecked((sbyte)_data[index]));

    public bool SetTag(int index, Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (tag is not ByteTag byteTag || (uint)index >= (uint)_data.Length)
            return false;

        _data[index] = unchecked((byte)byteTag.Value);
        return true;
    }

    public bool AddTag(int index, Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (tag is not ByteTag byteTag || (uint)index > (uint)_data.Length)
            return false;

        var expanded = new byte[checked(_data.Length + 1)];
        Array.Copy(_data, 0, expanded, 0, index);
        expanded[index] = unchecked((byte)byteTag.Value);
        Array.Copy(_data, index, expanded, index + 1, _data.Length - index);
        _data = expanded;
        return true;
    }

    public Tag Remove(int index)
    {
        byte removed = _data[index];
        var reduced = new byte[_data.Length - 1];
        Array.Copy(_data, 0, reduced, 0, index);
        Array.Copy(_data, index + 1, reduced, index, reduced.Length - index);
        _data = reduced;
        return ByteTag.ValueOf(unchecked((sbyte)removed));
    }

    public void Clear() =>
        _data = Array.Empty<byte>();

    public bool Equals(ByteArrayTag? other) =>
        other is not null && _data.AsSpan().SequenceEqual(other._data);

    public override bool Equals(object? obj) =>
        obj is ByteArrayTag other && Equals(other);

    public override int GetHashCode()
    {
        int result = 1;
        foreach (byte value in _data)
            result = unchecked(31 * result + (sbyte)value);
        return result;
    }

    public override string ToString() =>
        "[B;" + string.Join(",", _data.Select(value =>
            ByteTag.ValueOf(unchecked((sbyte)value)).ToString())) + "]";

    private static int ReadLength(NbtDataInput input)
    {
        int length = input.ReadInt();
        if (length < 0)
            throw new NbtFormatException($"Byte array length cannot be negative: {length}");
        return length;
    }

    private static void SkipPayload(NbtDataInput input, int length)
    {
        if (input.SkipBytes(length) != length)
            throw new EndOfStreamException();
    }
}
