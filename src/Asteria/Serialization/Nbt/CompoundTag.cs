namespace Asteria.Serialization.Nbt;

/// <summary>Named NBT payload collection (tag ID 10).</summary>
public sealed class CompoundTag : Tag, IEquatable<CompoundTag>
{
    private const long SELF_SIZE_IN_BYTES = 48;
    private const long MAP_ENTRY_SIZE_IN_BYTES = 36;
    private const long STRING_OBJECT_SIZE_IN_BYTES = 28;

    private readonly Dictionary<string, Tag> _values;

    public static TagType<CompoundTag> TYPE { get; } =
        TagType<CompoundTag>.VariableSize(
            10,
            "COMPOUND",
            "TAG_Compound",
            static (input, accounter) =>
            {
                accounter.PushDepth();
                try
                {
                    accounter.AccountBytes(SELF_SIZE_IN_BYTES);
                    var values = new Dictionary<string, Tag>(StringComparer.Ordinal);
                    while (true)
                    {
                        byte typeId = input.ReadByte();
                        if (typeId == EndTag.INSTANCE.GetId())
                            break;

                        string name = ReadName(input, accounter);
                        Tag value = TagTypes.GetType(typeId).Load(input, accounter);
                        if (!values.ContainsKey(name))
                            accounter.AccountBytes(MAP_ENTRY_SIZE_IN_BYTES);

                        values[name] = value;
                    }

                    return new CompoundTag(values);
                }
                finally
                {
                    accounter.PopDepth();
                }
            },
            static (input, compoundCount, accounter) =>
            {
                for (int compoundIndex = 0; compoundIndex < compoundCount; compoundIndex++)
                {
                    accounter.PushDepth();
                    try
                    {
                        while (true)
                        {
                            byte typeId = input.ReadByte();
                            if (typeId == EndTag.INSTANCE.GetId())
                                break;

                            input.SkipUTF();
                            TagTypes.GetType(typeId).Skip(input, 1, accounter);
                        }
                    }
                    finally
                    {
                        accounter.PopDepth();
                    }
                }
            });

    public CompoundTag()
        : this(new Dictionary<string, Tag>(StringComparer.Ordinal))
    {
    }

    internal CompoundTag(Dictionary<string, Tag> values) =>
        _values = values ?? throw new ArgumentNullException(nameof(values));

    public byte GetId() => 10;
    public TagType GetTagType() => TYPE;

    public void Write(NbtDataOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        foreach (KeyValuePair<string, Tag> entry in _values)
            WriteNamedTag(entry.Key, entry.Value, output);
        output.WriteByte(EndTag.INSTANCE.GetId());
    }

    public Tag Copy()
    {
        var copy = new Dictionary<string, Tag>(_values.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, Tag> entry in _values)
            copy.Add(entry.Key, entry.Value.Copy());
        return new CompoundTag(copy);
    }

    public long SizeInBytes()
    {
        long size = SELF_SIZE_IN_BYTES;
        foreach (KeyValuePair<string, Tag> entry in _values)
        {
            size = checked(size + STRING_OBJECT_SIZE_IN_BYTES + sizeof(char) * (long)entry.Key.Length);
            size = checked(size + MAP_ENTRY_SIZE_IN_BYTES);
            size = checked(size + entry.Value.SizeInBytes());
        }
        return size;
    }

    public CompoundTag? AsCompound() => this;

    public int Size() => _values.Count;

    public bool IsEmpty() => _values.Count == 0;

    public IReadOnlyDictionary<string, Tag> Values => _values;

    public IEnumerable<string> Keys => _values.Keys;

    public IEnumerable<Tag> Tags => _values.Values;

    public Tag? Get(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _values.GetValueOrDefault(name);
    }

    public Tag? Put(string name, Tag value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        _values.TryGetValue(name, out Tag? previous);
        _values[name] = value;
        return previous;
    }

    public Tag? Remove(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!_values.Remove(name, out Tag? previous))
            return null;
        return previous;
    }

    public bool Contains(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _values.ContainsKey(name);
    }

    public void Clear() =>
        _values.Clear();

    public void PutByte(string name, sbyte value) =>
        Put(name, ByteTag.ValueOf(value));

    public void PutBoolean(string name, bool value) =>
        Put(name, ByteTag.ValueOf(value));

    public void PutShort(string name, short value) =>
        Put(name, ShortTag.ValueOf(value));

    public void PutInt(string name, int value) =>
        Put(name, IntTag.ValueOf(value));

    public void PutLong(string name, long value) =>
        Put(name, LongTag.ValueOf(value));

    public void PutFloat(string name, float value) =>
        Put(name, FloatTag.ValueOf(value));

    public void PutDouble(string name, double value) =>
        Put(name, DoubleTag.ValueOf(value));

    public void PutString(string name, string value) =>
        Put(name, StringTag.ValueOf(value));

    public void PutByteArray(string name, byte[] value) =>
        Put(name, new ByteArrayTag(value));

    public void PutIntArray(string name, int[] value) =>
        Put(name, new IntArrayTag(value));

    public void PutLongArray(string name, long[] value) =>
        Put(name, new LongArrayTag(value));

    public void PutCompound(string name, CompoundTag value) =>
        Put(name, value);

    public void PutList(string name, ListTag value) =>
        Put(name, value);

    public sbyte GetByteOr(string name, sbyte defaultValue) =>
        Get(name) is NumericTag value ? value.ByteValue() : defaultValue;

    public short GetShortOr(string name, short defaultValue) =>
        Get(name) is NumericTag value ? value.ShortValue() : defaultValue;

    public int GetIntOr(string name, int defaultValue) =>
        Get(name) is NumericTag value ? value.IntValue() : defaultValue;

    public long GetLongOr(string name, long defaultValue) =>
        Get(name) is NumericTag value ? value.LongValue() : defaultValue;

    public float GetFloatOr(string name, float defaultValue) =>
        Get(name) is NumericTag value ? value.FloatValue() : defaultValue;

    public double GetDoubleOr(string name, double defaultValue) =>
        Get(name) is NumericTag value ? value.DoubleValue() : defaultValue;

    public bool GetBooleanOr(string name, bool defaultValue) =>
        GetByteOr(name, defaultValue ? (sbyte)1 : (sbyte)0) != 0;

    public string GetStringOr(string name, string defaultValue) =>
        Get(name) is StringTag value ? value.Value : defaultValue;

    public CompoundTag GetCompoundOrEmpty(string name) =>
        Get(name) is CompoundTag value ? value : new CompoundTag();

    public ListTag GetListOrEmpty(string name) =>
        Get(name) is ListTag value ? value : new ListTag();

    public byte[]? GetByteArray(string name) =>
        Get(name) is ByteArrayTag value ? value.Data : null;

    public int[]? GetIntArray(string name) =>
        Get(name) is IntArrayTag value ? value.Data : null;

    public long[]? GetLongArray(string name) =>
        Get(name) is LongArrayTag value ? value.Data : null;

    private static string ReadName(NbtDataInput input, NbtAccounter accounter)
    {
        string name = input.ReadUTF();
        accounter.AccountBytes(STRING_OBJECT_SIZE_IN_BYTES);
        return accounter.ReadUTF(name);
    }

    private static void WriteNamedTag(string name, Tag tag, NbtDataOutput output)
    {
        output.WriteByte(tag.GetId());
        output.WriteUTF(name);
        tag.Write(output);
    }

    public bool Equals(CompoundTag? other)
    {
        if (other is null || _values.Count != other._values.Count)
            return false;

        foreach (KeyValuePair<string, Tag> entry in _values)
        {
            if (!other._values.TryGetValue(entry.Key, out Tag? value) ||
                !Equals(entry.Value, value))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) =>
        obj is CompoundTag other && Equals(other);

    public override int GetHashCode()
    {
        int result = 0;
        foreach (KeyValuePair<string, Tag> entry in _values)
            result ^= HashCode.Combine(entry.Key, entry.Value);
        return result;
    }

    public override string ToString() =>
        "{" + string.Join(",", _values.Select(static entry =>
            entry.Key + ":" + entry.Value)) + "}";
}
