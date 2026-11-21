namespace Asteria.Serialization;

/// <summary>Codec specialized for values represented by named map fields.</summary>
public abstract class MapCodec<TValue>
{
    public abstract DataResult<TValue> Decode<TData>(
        DynamicOps<TData> ops,
        MapLike<TData> input);

    public abstract DataResult<TData> Encode<TData>(
        TValue input,
        DynamicOps<TData> ops,
        TData prefix);

    public abstract IReadOnlyList<TData> Keys<TData>(DynamicOps<TData> ops);

    public DataResult<TValue> Parse<TData>(
        DynamicOps<TData> ops,
        TData input) =>
        ops.GetMap(input).FlatMap(map => Decode(ops, map));

    public DataResult<TData> EncodeStart<TData>(
        DynamicOps<TData> ops,
        TValue input) =>
        Encode(input, ops, ops.EmptyMap);

    public Codec<TValue> Codec() =>
        new MapCodecAdapter<TValue>(this);

    public MapCodec<TMapped> XMap<TMapped>(
        Func<TValue, TMapped> to,
        Func<TMapped, TValue> from)
    {
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(from);
        return new MappedMapCodec<TValue, TMapped>(this, to, from);
    }

    private sealed class MapCodecAdapter<TSource> : Codec<TSource>
    {
        private readonly MapCodec<TSource> _source;

        public MapCodecAdapter(MapCodec<TSource> source) =>
            _source = source;

        public override DataResult<TData> Encode<TData>(
            TSource input,
            DynamicOps<TData> ops,
            TData prefix) =>
            _source.Encode(input, ops, prefix);

        public override DataResult<Pair<TSource, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input) =>
            _source.Parse(ops, input).Map(value =>
                new Pair<TSource, TData>(value, input));

        public override string ToString() =>
            _source.ToString();
    }

    private sealed class MappedMapCodec<TSource, TTarget> : MapCodec<TTarget>
    {
        private readonly MapCodec<TSource> _source;
        private readonly Func<TSource, TTarget> _to;
        private readonly Func<TTarget, TSource> _from;

        public MappedMapCodec(
            MapCodec<TSource> source,
            Func<TSource, TTarget> to,
            Func<TTarget, TSource> from)
        {
            _source = source;
            _to = to;
            _from = from;
        }

        public override DataResult<TTarget> Decode<TData>(
            DynamicOps<TData> ops,
            MapLike<TData> input) =>
            _source.Decode(ops, input).Map(_to);

        public override DataResult<TData> Encode<TData>(
            TTarget input,
            DynamicOps<TData> ops,
            TData prefix) =>
            _source.Encode(_from(input), ops, prefix);

        public override IReadOnlyList<TData> Keys<TData>(DynamicOps<TData> ops) =>
            _source.Keys(ops);

        public override string ToString() =>
            $"{_source}[xmapped]";
    }
}

internal sealed class FieldMapCodec<TValue> : MapCodec<TValue>
{
    private readonly Codec<TValue> _codec;
    private readonly string _name;

    public FieldMapCodec(Codec<TValue> codec, string name)
    {
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public override DataResult<TValue> Decode<TData>(
        DynamicOps<TData> ops,
        MapLike<TData> input) =>
        input.Get(_name).FlatMap(value => _codec.Parse(ops, value));

    public override DataResult<TData> Encode<TData>(
        TValue input,
        DynamicOps<TData> ops,
        TData prefix) =>
        _codec.EncodeStart(ops, input).FlatMap(value =>
            ops.MergeToMap(prefix, ops.CreateString(_name), value));

    public override IReadOnlyList<TData> Keys<TData>(DynamicOps<TData> ops) =>
        new[] { ops.CreateString(_name) };

    public override string ToString() =>
        $"Field[{_name}: {_codec}]";
}
