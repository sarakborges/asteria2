namespace Asteria.Serialization.Nbt;

/// <summary>
/// DynamicOps implementation over the NBT value tree, matching Minecraft Java 26.3.
/// Empty is TAG_End; map values are compounds; list operations retain primitive arrays
/// while all appended values remain compatible with their original element type.
/// </summary>
public sealed class NbtOps : DynamicOps<Tag>
{
    public static NbtOps INSTANCE { get; } = new();

    public Tag Empty => EndTag.INSTANCE;
    public Tag EmptyMap => new CompoundTag();
    public Tag EmptyList => new ListTag();
    public bool CompressMaps => false;

    private NbtOps()
    {
    }

    public bool IsEmpty(Tag input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input is EndTag;
    }

    public DataResult<NumberValue> GetNumberValue(Tag input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.AsNumber() is NumberValue number
            ? DataResult<NumberValue>.Success(number)
            : DataResult<NumberValue>.Error(() => $"Not a number: {input}");
    }

    public Tag CreateNumeric(NumberValue value) =>
        DoubleTag.ValueOf(value.DoubleValue);

    public DataResult<bool> GetBooleanValue(Tag input) =>
        GetNumberValue(input).Map(static number => number.DoubleValue != 0.0);

    public Tag CreateBoolean(bool value) =>
        ByteTag.ValueOf(value);

    public DataResult<string> GetStringValue(Tag input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input is StringTag stringTag
            ? DataResult<string>.Success(stringTag.Value)
            : DataResult<string>.Error(() => $"Not a string: {input}");
    }

    public Tag CreateString(string value) =>
        StringTag.ValueOf(value);

    public Tag CreateByte(sbyte value) =>
        ByteTag.ValueOf(value);

    public Tag CreateUnsignedByte(byte value) =>
        CreateNumeric(NumberValue.Of(value));

    public Tag CreateShort(short value) =>
        ShortTag.ValueOf(value);

    public Tag CreateUnsignedShort(ushort value) =>
        CreateNumeric(NumberValue.Of(value));

    public Tag CreateInt(int value) =>
        IntTag.ValueOf(value);

    public Tag CreateUnsignedInt(uint value) =>
        CreateNumeric(NumberValue.Of(value));

    public Tag CreateLong(long value) =>
        LongTag.ValueOf(value);

    public Tag CreateUnsignedLong(ulong value) =>
        CreateNumeric(NumberValue.Of(value));

    public Tag CreateFloat(float value) =>
        FloatTag.ValueOf(value);

    public Tag CreateDouble(double value) =>
        DoubleTag.ValueOf(value);

    public DataResult<IReadOnlyList<KeyValuePair<Tag, Tag>>> GetMapValues(Tag input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input is not CompoundTag compound)
        {
            return DataResult<IReadOnlyList<KeyValuePair<Tag, Tag>>>.Error(
                () => $"Not a map: {input}");
        }

        var entries = new List<KeyValuePair<Tag, Tag>>(compound.Size());
        foreach (KeyValuePair<string, Tag> entry in compound.Values)
        {
            entries.Add(new KeyValuePair<Tag, Tag>(
                StringTag.ValueOf(entry.Key),
                entry.Value));
        }

        return DataResult<IReadOnlyList<KeyValuePair<Tag, Tag>>>.Success(entries);
    }

    public DataResult<MapLike<Tag>> GetMap(Tag input)
    {
        DataResult<IReadOnlyList<KeyValuePair<Tag, Tag>>> entries = GetMapValues(input);
        return entries.Map(values => new MapLike<Tag>(this, input, values));
    }

    public Tag CreateMap(IEnumerable<KeyValuePair<Tag, Tag>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var compound = new CompoundTag();
        foreach (KeyValuePair<Tag, Tag> entry in values)
        {
            if (entry.Key is not StringTag key)
            {
                throw new NotSupportedException(
                    $"Cannot create map with non-string key: {entry.Key}");
            }

            compound.Put(key.Value, entry.Value);
        }

        return compound;
    }

    public DataResult<IReadOnlyList<Tag>> GetList(Tag input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input is CollectionTag collection
            ? DataResult<IReadOnlyList<Tag>>.Success(collection.ToArray())
            : DataResult<IReadOnlyList<Tag>>.Error(() => $"Not a list: {input}");
    }

    public Tag CreateList(IEnumerable<Tag> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var list = new ListTag();
        foreach (Tag value in values)
            list.AddTag(list.Size(), value);
        return list;
    }

    public DataResult<Tag> Get(Tag input, string key)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(key);
        if (input is not CompoundTag compound || compound.Get(key) is not Tag value)
        {
            return DataResult<Tag>.Error(
                () => $"No element {key} in the map {input}");
        }

        return DataResult<Tag>.Success(value);
    }

    public DataResult<Tag> MergeToList(Tag list, Tag value)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(value);
        ListCollector? collector = CreateCollector(list);
        if (collector is null)
        {
            return DataResult<Tag>.Error(
                () => $"mergeToList called with not a list: {list}",
                list);
        }

        return DataResult<Tag>.Success(collector.Accept(value).Result());
    }

    public DataResult<Tag> MergeToList(Tag list, IReadOnlyList<Tag> values)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(values);
        ListCollector? collector = CreateCollector(list);
        if (collector is null)
        {
            return DataResult<Tag>.Error(
                () => $"mergeToList called with not a list: {list}",
                list);
        }

        return DataResult<Tag>.Success(collector.AcceptAll(values).Result());
    }

    public DataResult<Tag> MergeToMap(Tag map, Tag key, Tag value)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        if (map is not CompoundTag && map is not EndTag)
        {
            return DataResult<Tag>.Error(
                () => $"mergeToMap called with not a map: {map}",
                map);
        }

        if (key is not StringTag stringKey)
        {
            return DataResult<Tag>.Error(
                () => $"key is not a string: {key}",
                map);
        }

        CompoundTag output = map is CompoundTag compound
            ? compound.ShallowCopy()
            : new CompoundTag();
        output.Put(stringKey.Value, value);
        return DataResult<Tag>.Success(output);
    }

    public DataResult<Tag> MergeToMap(
        Tag map,
        IReadOnlyList<KeyValuePair<Tag, Tag>> values)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(values);

        if (map is not CompoundTag && map is not EndTag)
        {
            return DataResult<Tag>.Error(
                () => $"mergeToMap called with not a map: {map}",
                map);
        }

        if (values.Count == 0)
            return DataResult<Tag>.Success(IsEmpty(map) ? EmptyMap : map);

        CompoundTag output = map is CompoundTag compound
            ? compound.ShallowCopy()
            : new CompoundTag();
        var missed = new List<Tag>();

        foreach (KeyValuePair<Tag, Tag> entry in values)
        {
            if (entry.Key is StringTag key)
                output.Put(key.Value, entry.Value);
            else
                missed.Add(entry.Key);
        }

        return missed.Count > 0
            ? DataResult<Tag>.Error(
                () => $"some keys are not strings: [{string.Join(", ", missed)}]",
                output)
            : DataResult<Tag>.Success(output);
    }

    public Tag Remove(Tag input, string key)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(key);
        if (input is not CompoundTag compound)
            return input;

        CompoundTag output = compound.ShallowCopy();
        output.Remove(key);
        return output;
    }

    public U ConvertTo<U>(DynamicOps<U> outputOps, Tag input)
    {
        ArgumentNullException.ThrowIfNull(outputOps);
        ArgumentNullException.ThrowIfNull(input);

        if (outputOps is NbtOps)
            return (U)(object)input.Copy();

        if (input is CompoundTag compound)
        {
            var values = new List<KeyValuePair<U, U>>(compound.Size());
            foreach (KeyValuePair<string, Tag> entry in compound.Values)
            {
                values.Add(new KeyValuePair<U, U>(
                    outputOps.CreateString(entry.Key),
                    ConvertTo(outputOps, entry.Value)));
            }

            return outputOps.CreateMap(values);
        }

        if (input is CollectionTag collection)
        {
            var values = new List<U>(collection.Size());
            foreach (Tag element in collection)
                values.Add(ConvertTo(outputOps, element));
            return outputOps.CreateList(values);
        }

        return input switch
        {
            EndTag => outputOps.Empty,
            ByteTag value => outputOps.CreateByte(value.Value),
            ShortTag value => outputOps.CreateShort(value.Value),
            IntTag value => outputOps.CreateInt(value.Value),
            LongTag value => outputOps.CreateLong(value.Value),
            FloatTag value => outputOps.CreateFloat(value.Value),
            DoubleTag value => outputOps.CreateDouble(value.Value),
            StringTag value => outputOps.CreateString(value.Value),
            _ => outputOps.Empty
        };
    }

    private static ListCollector? CreateCollector(Tag input)
    {
        if (input is EndTag)
            return new GenericListCollector();

        if (input is not CollectionTag collection)
            return null;

        if (collection.IsEmpty())
            return new GenericListCollector();

        return collection switch
        {
            ListTag list => new GenericListCollector(list),
            ByteArrayTag byteArray => new ByteListCollector(byteArray.Data),
            IntArrayTag intArray => new IntListCollector(intArray.Data),
            LongArrayTag longArray => new LongListCollector(longArray.Data),
            _ => throw new InvalidOperationException(
                $"Unsupported NBT collection type: {collection.GetType().Name}")
        };
    }

    private interface ListCollector
    {
        ListCollector Accept(Tag value);
        Tag Result();

        ListCollector AcceptAll(IEnumerable<Tag> values)
        {
            ListCollector collector = this;
            foreach (Tag value in values)
                collector = collector.Accept(value);
            return collector;
        }
    }

    private sealed class GenericListCollector : ListCollector
    {
        private readonly ListTag _result = new();

        public GenericListCollector()
        {
        }

        public GenericListCollector(ListTag initial)
        {
            foreach (Tag tag in initial)
                _result.AddTag(_result.Size(), tag);
        }

        public GenericListCollector(IEnumerable<Tag> initial)
        {
            foreach (Tag tag in initial)
                _result.AddTag(_result.Size(), tag);
        }

        public ListCollector Accept(Tag value)
        {
            _result.AddTag(_result.Size(), value);
            return this;
        }

        public Tag Result() => _result;
    }

    private sealed class ByteListCollector : ListCollector
    {
        private readonly List<sbyte> _values;

        public ByteListCollector(byte[] initialValues) =>
            _values = initialValues
                .Select(static value => unchecked((sbyte)value))
                .ToList();

        public ListCollector Accept(Tag value)
        {
            if (value is ByteTag byteTag)
            {
                _values.Add(byteTag.Value);
                return this;
            }

            return new GenericListCollector(
                    _values.Select(static current => (Tag)ByteTag.ValueOf(current)))
                .Accept(value);
        }

        public Tag Result() =>
            new ByteArrayTag(_values
                .Select(static value => unchecked((byte)value))
                .ToArray());
    }

    private sealed class IntListCollector : ListCollector
    {
        private readonly List<int> _values;

        public IntListCollector(int[] initialValues) =>
            _values = initialValues.ToList();

        public ListCollector Accept(Tag value)
        {
            if (value is IntTag intTag)
            {
                _values.Add(intTag.Value);
                return this;
            }

            return new GenericListCollector(
                    _values.Select(static current => (Tag)IntTag.ValueOf(current)))
                .Accept(value);
        }

        public Tag Result() =>
            new IntArrayTag(_values.ToArray());
    }

    private sealed class LongListCollector : ListCollector
    {
        private readonly List<long> _values;

        public LongListCollector(long[] initialValues) =>
            _values = initialValues.ToList();

        public ListCollector Accept(Tag value)
        {
            if (value is LongTag longTag)
            {
                _values.Add(longTag.Value);
                return this;
            }

            return new GenericListCollector(
                    _values.Select(static current => (Tag)LongTag.ValueOf(current)))
                .Accept(value);
        }

        public Tag Result() =>
            new LongArrayTag(_values.ToArray());
    }

    public override string ToString() => "NBT";
}
