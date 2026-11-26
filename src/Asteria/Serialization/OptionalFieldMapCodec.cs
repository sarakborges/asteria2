namespace Asteria.Serialization;

/// <summary>
/// Optional map field that preserves the distinction between an absent field and a present value.
/// </summary>
internal sealed class OptionalFieldMapCodec<TValue> : MapCodec<OptionalValue<TValue>>
{
    private readonly Codec<TValue> _codec;
    private readonly string _name;

    public OptionalFieldMapCodec(Codec<TValue> codec, string name)
    {
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public override DataResult<OptionalValue<TValue>> Decode<TData>(
        DynamicOps<TData> ops,
        MapLike<TData> input) =>
        input.TryGet(_name, out TData value)
            ? _codec.Parse(ops, value).Map(OptionalValue<TValue>.Of)
            : DataResult<OptionalValue<TValue>>.Success(OptionalValue<TValue>.Empty);

    public override DataResult<TData> Encode<TData>(
        OptionalValue<TValue> input,
        DynamicOps<TData> ops,
        TData prefix) =>
        input.HasValue
            ? _codec.EncodeStart(ops, input.Value).FlatMap(value =>
                ops.MergeToMap(prefix, ops.CreateString(_name), value))
            : DataResult<TData>.Success(prefix);

    public override IReadOnlyList<TData> Keys<TData>(DynamicOps<TData> ops) =>
        new[] { ops.CreateString(_name) };

    public override string ToString() =>
        $"OptionalField[{_name}: {_codec}]";
}
