namespace Asteria.Serialization.Nbt;

/// <summary>Homogeneous NBT list payload (tag ID 9).</summary>
public sealed class ListTag : CollectionTag, IEquatable<ListTag>
{
    private const long SELF_SIZE_IN_BYTES = 36;
    private const long ELEMENT_REFERENCE_SIZE = 4;
    private readonly List<Tag> _values;

    public static TagType<ListTag> TYPE { get; } =
        TagType<ListTag>.VariableSize(
            9,
            "LIST",
            "TAG_List",
            static (input, accounter) =>
            {
                accounter.PushDepth();
                try
                {
                    accounter.AccountBytes(SELF_SIZE_IN_BYTES);
                    byte elementTypeId = input.ReadByte();
                    int count = ReadListCount(input);
                    if (elementTypeId == EndTag.INSTANCE.GetId() && count > 0)
                        throw new NbtFormatException("Missing type on ListTag");

                    accounter.AccountBytes(ELEMENT_REFERENCE_SIZE, count);
                    TagType elementType = TagTypes.GetType(elementTypeId);
                    var values = new List<Tag>(count);
                    var list = new ListTag(values);
                    for (int index = 0; index < count; index++)
                        list.AddAndUnwrap(elementType.Load(input, accounter));

                    return list;
                }
                finally
                {
                    accounter.PopDepth();
                }
            },
            static (input, listCount, accounter) =>
            {
                for (int listIndex = 0; listIndex < listCount; listIndex++)
                {
                    accounter.PushDepth();
                    try
                    {
                        byte elementTypeId = input.ReadByte();
                        int count = ReadListCount(input);
                        if (elementTypeId == EndTag.INSTANCE.GetId() && count > 0)
                            throw new NbtFormatException("Missing type on ListTag");

                        TagTypes.GetType(elementTypeId).Skip(input, count, accounter);
                    }
                    finally
                    {
                        accounter.PopDepth();
                    }
                }
            });

    public ListTag()
        : this(new List<Tag>())
    {
    }

    internal ListTag(List<Tag> values) =>
        _values = values ?? throw new ArgumentNullException(nameof(values));

    public byte GetId() => 9;
    public TagType GetTagType() => TYPE;

    public void Write(NbtDataOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        byte elementType = IdentifyRawElementType();
        output.WriteByte(elementType);
        output.WriteInt(_values.Count);

        foreach (Tag value in _values)
            WrapIfNeeded(elementType, value).Write(output);
    }

    public Tag Copy() =>
        new ListTag(_values.Select(static value => value.Copy()).ToList());

    public long SizeInBytes()
    {
        long size = SELF_SIZE_IN_BYTES +
                    ELEMENT_REFERENCE_SIZE * (long)_values.Count;
        foreach (Tag child in _values)
            size = checked(size + child.SizeInBytes());
        return size;
    }

    public ListTag? AsList() => this;

    public int Size() => _values.Count;

    public bool IsEmpty() => _values.Count == 0;

    public Tag Get(int index) =>
        _values[index];

    public bool SetTag(int index, Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        _values[index] = tag;
        return true;
    }

    public bool AddTag(int index, Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        _values.Insert(index, tag);
        return true;
    }

    public Tag Remove(int index)
    {
        Tag removed = _values[index];
        _values.RemoveAt(index);
        return removed;
    }

    public void Clear() =>
        _values.Clear();

    /// <summary>
    /// Adds a decoded compound element after removing the empty-name wrapper used
    /// when a heterogeneous in-memory list is serialized as a list of compounds.
    /// </summary>
    public void AddAndUnwrap(Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (tag is CompoundTag compound &&
            compound.Size() == 1 &&
            compound.Contains(string.Empty))
        {
            _values.Add(compound.Get(string.Empty)!);
            return;
        }

        _values.Add(tag);
    }

    public byte IdentifyRawElementType()
    {
        byte homogeneousType = 0;
        foreach (Tag value in _values)
        {
            byte elementType = value.GetId();
            if (homogeneousType == 0)
                homogeneousType = elementType;
            else if (homogeneousType != elementType)
                return 10;
        }

        return homogeneousType;
    }

    private static Tag WrapIfNeeded(byte elementType, Tag tag)
    {
        if (elementType != 10)
            return tag;

        if (tag is CompoundTag compound &&
            !(compound.Size() == 1 && compound.Contains(string.Empty)))
        {
            return compound;
        }

        return WrapElement(tag);
    }

    private static CompoundTag WrapElement(Tag tag)
    {
        var wrapper = new CompoundTag();
        wrapper.Put(string.Empty, tag);
        return wrapper;
    }

    private static int ReadListCount(NbtDataInput input)
    {
        int count = input.ReadInt();
        if (count < 0)
            throw new NbtFormatException($"ListTag length cannot be negative: {count}");
        return count;
    }

    public bool Equals(ListTag? other) =>
        other is not null && _values.SequenceEqual(other._values);

    public override bool Equals(object? obj) =>
        obj is ListTag other && Equals(other);

    public override int GetHashCode()
    {
        int result = 1;
        foreach (Tag value in _values)
            result = unchecked(31 * result + value.GetHashCode());
        return result;
    }

    public override string ToString() =>
        "[" + string.Join(",", _values.Select(static value => value.ToString())) + "]";
}
