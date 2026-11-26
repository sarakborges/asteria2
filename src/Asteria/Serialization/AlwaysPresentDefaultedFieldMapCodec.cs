namespace Asteria.Serialization;

/// <summary>
/// Optional record field with a decode default that is always emitted when encoding.
/// </summary>
internal sealed class AlwaysPresentDefaultedFieldMapCodec<TValue> : MapCodec<TValue>
{
    private readonly Codec<TValue> _codec;
    private readonly string _name;
    private readonly TValue _defaultValue;

    public AlwaysPresentDefaultedFieldMapCodec(
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
        TData prefix) =>
        _codec.EncodeStart(ops, input).FlatMap(value =>
            ops.MergeToMap(prefix, ops.CreateString(_name), value));

    public override IReadOnlyList<TData> Keys<TData>(DynamicOps<TData> ops) =>
        new[] { ops.CreateString(_name) };

    public override string ToString() =>
        $"OptionalAlwaysPresentField[{_name}: {_codec} = {_defaultValue}]";
}
