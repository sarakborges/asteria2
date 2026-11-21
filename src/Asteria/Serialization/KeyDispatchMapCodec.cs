namespace Asteria.Serialization;

internal sealed class UnitMapCodec<TValue> : MapCodec<TValue>
{
    private readonly Func<TValue> _value;

    public UnitMapCodec(Func<TValue> value) =>
        _value = value ?? throw new ArgumentNullException(nameof(value));

    public override DataResult<TValue> Decode<TData>(
        DynamicOps<TData> ops,
        MapLike<TData> input) =>
        DataResult<TValue>.Success(_value());

    public override DataResult<TData> Encode<TData>(
        TValue input,
        DynamicOps<TData> ops,
        TData prefix) =>
        DataResult<TData>.Success(prefix);

    public override IReadOnlyList<TData> Keys<TData>(DynamicOps<TData> ops) =>
        Array.Empty<TData>();

    public override string ToString() =>
        $"Unit[{_value()}]";
}

/// <summary>
/// Dispatches a value through a discriminator field and the selected subtype's map codec.
/// This is the non-compressed map form of DataFixerUpper's KeyDispatchCodec.
/// </summary>
internal sealed class KeyDispatchMapCodec<TKey, TValue> : MapCodec<TValue>
{
    private readonly MapCodec<TKey> _keyCodec;
    private readonly Func<TValue, DataResult<TKey>> _type;
    private readonly Func<TKey, DataResult<MapCodec<TValue>>> _codec;

    public KeyDispatchMapCodec(
        MapCodec<TKey> keyCodec,
        Func<TValue, DataResult<TKey>> type,
        Func<TKey, DataResult<MapCodec<TValue>>> codec)
    {
        _keyCodec = keyCodec;
        _type = type;
        _codec = codec;
    }

    public override DataResult<TValue> Decode<TData>(
        DynamicOps<TData> ops,
        MapLike<TData> input) =>
        _keyCodec.Decode(ops, input).FlatMap(type =>
            _codec(type).FlatMap(codec => codec.Decode(ops, input)));

    public override DataResult<TData> Encode<TData>(
        TValue input,
        DynamicOps<TData> ops,
        TData prefix) =>
        _type(input).FlatMap(type =>
            _codec(type).FlatMap(codec =>
                codec.Encode(input, ops, prefix).FlatMap(encoded =>
                    _keyCodec.Encode(type, ops, encoded))));

    public override IReadOnlyList<TData> Keys<TData>(DynamicOps<TData> ops) =>
        _keyCodec.Keys(ops);

    public override string ToString() =>
        $"KeyDispatch[{_keyCodec}]";
}
