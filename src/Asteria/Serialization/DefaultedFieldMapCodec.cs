namespace Asteria.Serialization;

/// <summary>
/// Optional record field with a default value. Missing values decode to the default;
/// encoding the default omits the field, matching DataFixerUpper's optionalFieldOf(name, value).
/// </summary>
internal sealed class DefaultedFieldMapCodec<TValue> : MapCodec<TValue>
{
    private readonly Codec<TValue> _codec;
    private readonly string _name;
    private readonly TValue _defaultValue;

    public DefaultedFieldMapCodec(
        Codec<TValue> codec,
        string name,
        TValue defaultValue)
    {
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _defaultValue = defaultValue;
    }

    public override DataResult<TValue> Decode<TData>(
        DynamicOps<TData> ops,
        MapLike<TData> input) =>
        input.TryGet(_name, out TData value)
            ? _codec.Parse(ops, value)
            : DataResult<TValue>.Success(_defaultValue);

    public override DataResult<TData> Encode<TData>(
        TValue input,
        DynamicOps<TData> ops,
        TData prefix)
    {
        if (EqualityComparer<TValue>.Default.Equals(input, _defaultValue))
            return DataResult<TData>.Success(prefix);

        return _codec.EncodeStart(ops, input).FlatMap(value =>
            ops.MergeToMap(prefix, ops.CreateString(_name), value));
    }

    public override IReadOnlyList<TData> Keys<TData>(DynamicOps<TData> ops) =>
        new[] { ops.CreateString(_name) };

    public override string ToString() =>
        $"OptionalField[{_name}: {_codec} = {_defaultValue}]";
}
