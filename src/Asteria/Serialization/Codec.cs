using Asteria.Content.Registries;

namespace Asteria.Serialization;

/// <summary>
/// Bidirectional, format-independent codec. Encode/decode preserve the DataFixerUpper
/// prefix and unconsumed-input contracts.
/// </summary>
public abstract class Codec<TValue> : Encoder<TValue>, Decoder<TValue>
{
    public abstract DataResult<TData> Encode<TData>(
        TValue input,
        DynamicOps<TData> ops,
        TData prefix);

    public abstract DataResult<Pair<TValue, TData>> Decode<TData>(
        DynamicOps<TData> ops,
        TData input);

    public DataResult<TValue> Parse<TData>(
        DynamicOps<TData> ops,
        TData input) =>
        Decode(ops, input).Map(pair => pair.First);

    public DataResult<TData> EncodeStart<TData>(
        DynamicOps<TData> ops,
        TValue input) =>
        Encode(input, ops, ops.Empty);

    public Codec<TMapped> XMap<TMapped>(
        Func<TValue, TMapped> to,
        Func<TMapped, TValue> from)
    {
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(from);
        return new XMappedCodec<TValue, TMapped>(this, to, from);
    }

    public Codec<TMapped> FlatXMap<TMapped>(
        Func<TValue, DataResult<TMapped>> to,
        Func<TMapped, DataResult<TValue>> from)
    {
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(from);
        return new FlatXMappedCodec<TValue, TMapped>(this, to, from);
    }

    /// <summary>
    /// Tries this codec first, then the alternative codec. Encoding always uses this codec.
    /// Both branches preserve DataFixerUpper partial-result precedence.
    /// </summary>
    public Codec<TValue> WithAlternative<TAlternative>(
        Codec<TAlternative> alternative,
        Func<TAlternative, TValue> convert)
    {
        ArgumentNullException.ThrowIfNull(alternative);
        ArgumentNullException.ThrowIfNull(convert);
        return new AlternativeCodec<TValue, TAlternative>(this, alternative, convert);
    }

    /// <summary>Selects the compressed representation only when DynamicOps requests map compression.</summary>
    public Codec<TValue> OrCompressed(Codec<TValue> compressedCodec)
    {
        ArgumentNullException.ThrowIfNull(compressedCodec);
        return new CompressedCodec<TValue>(this, compressedCodec);
    }

    public Codec<TValue> Validate(Func<TValue, DataResult<TValue>> checker)
    {
        ArgumentNullException.ThrowIfNull(checker);
        return FlatXMap(checker, checker);
    }

    public Codec<TValue> WithLifecycle(Lifecycle lifecycle)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        return new LifecycleCodec<TValue>(this, lifecycle);
    }

    public Codec<IReadOnlyList<TValue>> ListOf(
        int minSize = 0,
        int maxSize = int.MaxValue)
    {
        if (minSize < 0)
            throw new ArgumentOutOfRangeException(nameof(minSize));
        if (maxSize < minSize)
            throw new ArgumentOutOfRangeException(nameof(maxSize));

        return new ListCodec<TValue>(this, minSize, maxSize);
    }

    public MapCodec<TValue> FieldOf(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new FieldMapCodec<TValue>(this, name);
    }

    public MapCodec<TValue> OptionalFieldOf(string name, TValue defaultValue)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new DefaultedFieldMapCodec<TValue>(this, name, defaultValue);
    }

    /// <summary>
    /// Optional field with a decode default that is still emitted when encoding,
    /// matching DataFixerUpper's optionalAlwaysPresentFieldOf contract.
    /// </summary>
    public MapCodec<TValue> OptionalAlwaysPresentFieldOf(string name, TValue defaultValue)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new AlwaysPresentDefaultedFieldMapCodec<TValue>(this, name, defaultValue);
    }

    public override string ToString() =>
        GetType().Name;

    private sealed class XMappedCodec<TSource, TTarget> : Codec<TTarget>
    {
        private readonly Codec<TSource> _source;
        private readonly Func<TSource, TTarget> _to;
        private readonly Func<TTarget, TSource> _from;

        public XMappedCodec(
            Codec<TSource> source,
            Func<TSource, TTarget> to,
            Func<TTarget, TSource> from)
        {
            _source = source;
            _to = to;
            _from = from;
        }

        public override DataResult<TData> Encode<TData>(
            TTarget input,
            DynamicOps<TData> ops,
            TData prefix) =>
            _source.Encode(_from(input), ops, prefix);

        public override DataResult<Pair<TTarget, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input) =>
            _source.Decode(ops, input).Map(pair =>
                new Pair<TTarget, TData>(_to(pair.First), pair.Second));

        public override string ToString() =>
            $"{_source}[xmapped]";
    }

    private sealed class FlatXMappedCodec<TSource, TTarget> : Codec<TTarget>
    {
        private readonly Codec<TSource> _source;
        private readonly Func<TSource, DataResult<TTarget>> _to;
        private readonly Func<TTarget, DataResult<TSource>> _from;

        public FlatXMappedCodec(
            Codec<TSource> source,
            Func<TSource, DataResult<TTarget>> to,
            Func<TTarget, DataResult<TSource>> from)
        {
            _source = source;
            _to = to;
            _from = from;
        }

        public override DataResult<TData> Encode<TData>(
            TTarget input,
            DynamicOps<TData> ops,
            TData prefix) =>
            _from(input).FlatMap(value => _source.Encode(value, ops, prefix));

        public override DataResult<Pair<TTarget, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input) =>
            _source.Decode(ops, input).FlatMap(pair =>
                _to(pair.First).Map(value =>
                    new Pair<TTarget, TData>(value, pair.Second)));

        public override string ToString() =>
            $"{_source}[flatXmapped]";
    }

    private sealed class AlternativeCodec<TMain, TAlternative> : Codec<TMain>
    {
        private readonly Codec<TMain> _primary;
        private readonly Codec<TAlternative> _alternative;
        private readonly Func<TAlternative, TMain> _convert;

        public AlternativeCodec(
            Codec<TMain> primary,
            Codec<TAlternative> alternative,
            Func<TAlternative, TMain> convert)
        {
            _primary = primary;
            _alternative = alternative;
            _convert = convert;
        }

        public override DataResult<TData> Encode<TData>(
            TMain input,
            DynamicOps<TData> ops,
            TData prefix) =>
            _primary.Encode(input, ops, prefix);

        public override DataResult<Pair<TMain, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input)
        {
            DataResult<Pair<TMain, TData>> primary = _primary.Decode(ops, input);
            if (primary.IsSuccess)
                return primary;

            DataResult<Pair<TMain, TData>> alternative =
                _alternative.Decode(ops, input).Map(pair =>
                    new Pair<TMain, TData>(_convert(pair.First), pair.Second));

            if (alternative.IsSuccess)
                return alternative;

            if (primary.HasResultOrPartial)
                return primary;

            if (alternative.HasResultOrPartial)
                return alternative;

            return DataResult<Pair<TMain, TData>>.Error(() =>
                $"Failed to parse either. First: {primary.ErrorMessage}; Second: {alternative.ErrorMessage}");
        }

        public override string ToString() =>
            $"{_primary}[ or {_alternative}]";
    }

    private sealed class CompressedCodec<TSource> : Codec<TSource>
    {
        private readonly Codec<TSource> _normal;
        private readonly Codec<TSource> _compressed;

        public CompressedCodec(Codec<TSource> normal, Codec<TSource> compressed)
        {
            _normal = normal;
            _compressed = compressed;
        }

        public override DataResult<TData> Encode<TData>(
            TSource input,
            DynamicOps<TData> ops,
            TData prefix) =>
            (ops.CompressMaps ? _compressed : _normal).Encode(input, ops, prefix);

        public override DataResult<Pair<TSource, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input) =>
            (ops.CompressMaps ? _compressed : _normal).Decode(ops, input);

        public override string ToString() =>
            $"{_normal}[orCompressed {_compressed}]";
    }

    private sealed class LifecycleCodec<TSource> : Codec<TSource>
    {
        private readonly Codec<TSource> _source;
        private readonly Lifecycle _lifecycle;

        public LifecycleCodec(Codec<TSource> source, Lifecycle lifecycle)
        {
            _source = source;
            _lifecycle = lifecycle;
        }

        public override DataResult<TData> Encode<TData>(
            TSource input,
            DynamicOps<TData> ops,
            TData prefix) =>
            _source.Encode(input, ops, prefix).SetLifecycle(_lifecycle);

        public override DataResult<Pair<TSource, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input) =>
            _source.Decode(ops, input).SetLifecycle(_lifecycle);

        public override string ToString() =>
            _source.ToString();
    }

    private sealed class ListCodec<TElement> : Codec<IReadOnlyList<TElement>>
    {
        private readonly Codec<TElement> _elementCodec;
        private readonly int _minSize;
        private readonly int _maxSize;

        public ListCodec(Codec<TElement> elementCodec, int minSize, int maxSize)
        {
            _elementCodec = elementCodec;
            _minSize = minSize;
            _maxSize = maxSize;
        }

        public override DataResult<TData> Encode<TData>(
            IReadOnlyList<TElement> input,
            DynamicOps<TData> ops,
            TData prefix)
        {
            ArgumentNullException.ThrowIfNull(input);
            if (input.Count < _minSize || input.Count > _maxSize)
                return DataResult<TData>.Error(() =>
                    $"List is outside of range [{_minSize}: {_maxSize}]: {input.Count}");

            DataResult<IReadOnlyList<TData>> encoded =
                DataResult<IReadOnlyList<TData>>.Success(Array.Empty<TData>());

            foreach (TElement element in input)
            {
                DataResult<TData> next = _elementCodec.EncodeStart(ops, element);
                encoded = encoded.Apply2(
                    static (values, value) =>
                    {
                        var result = new List<TData>(values.Count + 1);
                        result.AddRange(values);
                        result.Add(value);
                        return (IReadOnlyList<TData>)result;
                    },
                    next);
            }

            return encoded.FlatMap(values => ops.MergeToList(prefix, values));
        }

        public override DataResult<Pair<IReadOnlyList<TElement>, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input) =>
            ops.GetList(input).FlatMap(elements =>
            {
                if (elements.Count < _minSize || elements.Count > _maxSize)
                {
                    return DataResult<Pair<IReadOnlyList<TElement>, TData>>.Error(() =>
                        $"List is outside of range [{_minSize}: {_maxSize}]: {elements.Count}");
                }

                DataResult<IReadOnlyList<TElement>> decoded =
                    DataResult<IReadOnlyList<TElement>>.Success(Array.Empty<TElement>());

                foreach (TData element in elements)
                {
                    DataResult<TElement> next = _elementCodec.Parse(ops, element);
                    decoded = decoded.Apply2(
                        static (values, value) =>
                        {
                            var result = new List<TElement>(values.Count + 1);
                            result.AddRange(values);
                            result.Add(value);
                            return (IReadOnlyList<TElement>)result;
                        },
                        next);
                }

                return decoded.Map(values =>
                    new Pair<IReadOnlyList<TElement>, TData>(values, ops.Empty));
            });

        public override string ToString() =>
            $"List[{_elementCodec}]";
    }
}
