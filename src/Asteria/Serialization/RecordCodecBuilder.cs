namespace Asteria.Serialization;

/// <summary>
/// Typed field definition used to assemble a record codec from named map codecs.
/// </summary>
public sealed class RecordField<TRecord, TValue>
{
    public MapCodec<TValue> Codec { get; }
    public Func<TRecord, TValue> Getter { get; }

    internal RecordField(MapCodec<TValue> codec, Func<TRecord, TValue> getter)
    {
        Codec = codec ?? throw new ArgumentNullException(nameof(codec));
        Getter = getter ?? throw new ArgumentNullException(nameof(getter));
    }
}

/// <summary>
/// Builds map codecs and codecs for record-like values from composable field codecs.
/// The two-field applicative group used by current environmental records preserves
/// both field errors and partial values through DataResult.Apply2.
/// </summary>
public static class RecordCodecBuilder
{
    public static Codec<TRecord> Create<TRecord>(
        Func<Instance<TRecord>, MapCodec<TRecord>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder(new Instance<TRecord>()).Codec();
    }

    public static MapCodec<TRecord> MapCodec<TRecord>(
        Func<Instance<TRecord>, MapCodec<TRecord>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder(new Instance<TRecord>());
    }

    public sealed class Instance<TRecord>
    {
        public Group2<TRecord, TFirst, TSecond> Group<TFirst, TSecond>(
            RecordField<TRecord, TFirst> first,
            RecordField<TRecord, TSecond> second) =>
            new(first, second);

        public Group3<TRecord, TFirst, TSecond, TThird> Group<TFirst, TSecond, TThird>(
            RecordField<TRecord, TFirst> first,
            RecordField<TRecord, TSecond> second,
            RecordField<TRecord, TThird> third) =>
            new(first, second, third);
    }

    public sealed class Group2<TRecord, TFirst, TSecond>
    {
        private readonly RecordField<TRecord, TFirst> _first;
        private readonly RecordField<TRecord, TSecond> _second;

        internal Group2(
            RecordField<TRecord, TFirst> first,
            RecordField<TRecord, TSecond> second)
        {
            _first = first ?? throw new ArgumentNullException(nameof(first));
            _second = second ?? throw new ArgumentNullException(nameof(second));
        }

        public MapCodec<TRecord> Apply(
            Instance<TRecord> instance,
            Func<TFirst, TSecond, TRecord> constructor)
        {
            ArgumentNullException.ThrowIfNull(instance);
            ArgumentNullException.ThrowIfNull(constructor);
            return new RecordMapCodec<TRecord, TFirst, TSecond>(
                _first,
                _second,
                constructor);
        }
    }


    public sealed class Group3<TRecord, TFirst, TSecond, TThird>
    {
        private readonly RecordField<TRecord, TFirst> _first;
        private readonly RecordField<TRecord, TSecond> _second;
        private readonly RecordField<TRecord, TThird> _third;

        internal Group3(
            RecordField<TRecord, TFirst> first,
            RecordField<TRecord, TSecond> second,
            RecordField<TRecord, TThird> third)
        {
            _first = first ?? throw new ArgumentNullException(nameof(first));
            _second = second ?? throw new ArgumentNullException(nameof(second));
            _third = third ?? throw new ArgumentNullException(nameof(third));
        }

        public MapCodec<TRecord> Apply(
            Instance<TRecord> instance,
            Func<TFirst, TSecond, TThird, TRecord> constructor)
        {
            ArgumentNullException.ThrowIfNull(instance);
            ArgumentNullException.ThrowIfNull(constructor);
            return new RecordMapCodec<TRecord, TFirst, TSecond, TThird>(
                _first,
                _second,
                _third,
                constructor);
        }
    }

    private sealed class RecordMapCodec<TRecord, TFirst, TSecond, TThird> : MapCodec<TRecord>
    {
        private readonly RecordField<TRecord, TFirst> _first;
        private readonly RecordField<TRecord, TSecond> _second;
        private readonly RecordField<TRecord, TThird> _third;
        private readonly Func<TFirst, TSecond, TThird, TRecord> _constructor;

        public RecordMapCodec(
            RecordField<TRecord, TFirst> first,
            RecordField<TRecord, TSecond> second,
            RecordField<TRecord, TThird> third,
            Func<TFirst, TSecond, TThird, TRecord> constructor)
        {
            _first = first;
            _second = second;
            _third = third;
            _constructor = constructor;
        }

        public override DataResult<TRecord> Decode<TData>(
            DynamicOps<TData> ops,
            MapLike<TData> input)
        {
            DataResult<TFirst> first = _first.Codec.Decode(ops, input);
            DataResult<TSecond> second = _second.Codec.Decode(ops, input);
            DataResult<TThird> third = _third.Codec.Decode(ops, input);

            return first.Apply2(
                    static (left, right) => new Pair<TFirst, TSecond>(left, right),
                    second)
                .Apply2(
                    (pair, last) => _constructor(pair.First, pair.Second, last),
                    third);
        }

        public override DataResult<TData> Encode<TData>(
            TRecord input,
            DynamicOps<TData> ops,
            TData prefix)
        {
            DataResult<TData> first = _first.Codec.EncodeStart(ops, _first.Getter(input));
            DataResult<TData> second = _second.Codec.EncodeStart(ops, _second.Getter(input));
            DataResult<TData> third = _third.Codec.EncodeStart(ops, _third.Getter(input));

            return first.Apply2(
                    static (left, right) => new Pair<TData, TData>(left, right),
                    second)
                .Apply2(
                    static (firstPair, last) =>
                        new Pair<Pair<TData, TData>, TData>(firstPair, last),
                    third)
                .FlatMap(maps =>
                    ops.GetMapValues(maps.First.First).Apply2(
                        static (firstEntries, secondEntries) =>
                        {
                            var combined = new List<KeyValuePair<TData, TData>>(
                                firstEntries.Count + secondEntries.Count);
                            combined.AddRange(firstEntries);
                            combined.AddRange(secondEntries);
                            return (IReadOnlyList<KeyValuePair<TData, TData>>)combined;
                        },
                        ops.GetMapValues(maps.First.Second))
                    .FlatMap(firstTwoEntries =>
                        ops.GetMapValues(maps.Second).Map(thirdEntries =>
                        {
                            var combined = new List<KeyValuePair<TData, TData>>(
                                firstTwoEntries.Count + thirdEntries.Count);
                            combined.AddRange(firstTwoEntries);
                            combined.AddRange(thirdEntries);
                            return (IReadOnlyList<KeyValuePair<TData, TData>>)combined;
                        })))
                .FlatMap(entries => ops.MergeToMap(prefix, entries));
        }

        public override IReadOnlyList<TData> Keys<TData>(DynamicOps<TData> ops)
        {
            IReadOnlyList<TData> first = _first.Codec.Keys(ops);
            IReadOnlyList<TData> second = _second.Codec.Keys(ops);
            IReadOnlyList<TData> third = _third.Codec.Keys(ops);
            var keys = new List<TData>(first.Count + second.Count + third.Count);
            keys.AddRange(first);
            keys.AddRange(second);
            keys.AddRange(third);
            return keys;
        }

        public override string ToString() =>
            $"RecordCodec[{_first.Codec} * {_second.Codec} * {_third.Codec}]";
    }

    private sealed class RecordMapCodec<TRecord, TFirst, TSecond> : MapCodec<TRecord>
    {
        private readonly RecordField<TRecord, TFirst> _first;
        private readonly RecordField<TRecord, TSecond> _second;
        private readonly Func<TFirst, TSecond, TRecord> _constructor;

        public RecordMapCodec(
            RecordField<TRecord, TFirst> first,
            RecordField<TRecord, TSecond> second,
            Func<TFirst, TSecond, TRecord> constructor)
        {
            _first = first;
            _second = second;
            _constructor = constructor;
        }

        public override DataResult<TRecord> Decode<TData>(
            DynamicOps<TData> ops,
            MapLike<TData> input)
        {
            DataResult<TFirst> first = _first.Codec.Decode(ops, input);
            DataResult<TSecond> second = _second.Codec.Decode(ops, input);
            return first.Apply2(_constructor, second);
        }

        public override DataResult<TData> Encode<TData>(
            TRecord input,
            DynamicOps<TData> ops,
            TData prefix)
        {
            DataResult<TData> first = _first.Codec.EncodeStart(ops, _first.Getter(input));
            DataResult<TData> second = _second.Codec.EncodeStart(ops, _second.Getter(input));

            return first.Apply2(
                static (firstMap, secondMap) =>
                    new Pair<TData, TData>(firstMap, secondMap),
                second)
                .FlatMap(maps =>
                    ops.GetMapValues(maps.First).Apply2(
                        static (firstEntries, secondEntries) =>
                        {
                            var combined = new List<KeyValuePair<TData, TData>>(
                                firstEntries.Count + secondEntries.Count);
                            combined.AddRange(firstEntries);
                            combined.AddRange(secondEntries);
                            return (IReadOnlyList<KeyValuePair<TData, TData>>)combined;
                        },
                        ops.GetMapValues(maps.Second)))
                .FlatMap(entries => ops.MergeToMap(prefix, entries));
        }

        public override IReadOnlyList<TData> Keys<TData>(DynamicOps<TData> ops)
        {
            IReadOnlyList<TData> first = _first.Codec.Keys(ops);
            IReadOnlyList<TData> second = _second.Codec.Keys(ops);
            var keys = new List<TData>(first.Count + second.Count);
            keys.AddRange(first);
            keys.AddRange(second);
            return keys;
        }

        public override string ToString() =>
            $"RecordCodec[{_first.Codec} * {_second.Codec}]";
    }
}
