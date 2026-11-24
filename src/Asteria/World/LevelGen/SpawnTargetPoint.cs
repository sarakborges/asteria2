using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.LevelGen.DensityFunctions;

namespace Asteria.World.LevelGen;

/// <summary>
/// One arbitrary-dimensional spawn suitability target from Minecraft Java 26.3.
/// </summary>
public sealed record SpawnTargetPoint(
    IReadOnlyDictionary<Holder<DensityFunction>, Climate.Parameter> Parameters)
{
    private static Codec<IReadOnlyDictionary<Holder<DensityFunction>, Climate.Parameter>> ParametersCodec { get; } =
        new UnboundedMapCodec<Holder<DensityFunction>, Climate.Parameter>(
            DensityFunction.REFERENCE_CODEC,
            Climate.Parameter.CODEC);

    /// <summary>
    /// Encodes a map from registered density-function references to climate parameter ranges.
    /// Each key is a density-function registry identifier; each value is a Climate.Parameter.
    /// </summary>
    public static Codec<SpawnTargetPoint> CODEC { get; } =
        ParametersCodec.XMap(
            static parameters => new SpawnTargetPoint(parameters),
            static target => target.Parameters);

    /// <summary>
    /// Calculates the squared Euclidean distance from the sampled density values
    /// to this target hypercube, using the vanilla climate-coordinate quantization.
    /// </summary>
    public long SampleFitness(
        DensitySamplerSet samplers,
        int blockX,
        int blockY,
        int blockZ)
    {
        ArgumentNullException.ThrowIfNull(samplers);

        long fitness = 0L;
        foreach (KeyValuePair<Holder<DensityFunction>, Climate.Parameter> parameter in Parameters)
        {
            DensityFunction function = parameter.Key.Value;
            long value = Climate.QuantizeCoord(
                samplers.SampleValue(function, blockX, blockY, blockZ));
            long distance = parameter.Value.Distance(value);
            fitness = unchecked(fitness + unchecked(distance * distance));
        }

        return fitness;
    }

    /// <summary>
    /// Codec.unboundedMap equivalent for registry-aware density-function map keys.
    /// </summary>
    private sealed class UnboundedMapCodec<TKey, TValue> : Codec<IReadOnlyDictionary<TKey, TValue>>
        where TKey : notnull
    {
        private readonly Codec<TKey> _keyCodec;
        private readonly Codec<TValue> _valueCodec;

        public UnboundedMapCodec(Codec<TKey> keyCodec, Codec<TValue> valueCodec)
        {
            _keyCodec = keyCodec ?? throw new ArgumentNullException(nameof(keyCodec));
            _valueCodec = valueCodec ?? throw new ArgumentNullException(nameof(valueCodec));
        }

        public override DataResult<TData> Encode<TData>(
            IReadOnlyDictionary<TKey, TValue> input,
            DynamicOps<TData> ops,
            TData prefix)
        {
            ArgumentNullException.ThrowIfNull(input);

            DataResult<IReadOnlyList<KeyValuePair<TData, TData>>> encoded =
                DataResult<IReadOnlyList<KeyValuePair<TData, TData>>>.Success(
                    Array.Empty<KeyValuePair<TData, TData>>());

            foreach (KeyValuePair<TKey, TValue> entry in input)
            {
                DataResult<KeyValuePair<TData, TData>> encodedEntry =
                    _keyCodec.EncodeStart(ops, entry.Key).Apply2(
                        static (key, value) => new KeyValuePair<TData, TData>(key, value),
                        _valueCodec.EncodeStart(ops, entry.Value));

                encoded = encoded.Apply2(
                    static (entries, next) =>
                    {
                        var combined = new List<KeyValuePair<TData, TData>>(entries.Count + 1);
                        combined.AddRange(entries);
                        combined.Add(next);
                        return (IReadOnlyList<KeyValuePair<TData, TData>>)combined;
                    },
                    encodedEntry);
            }

            return encoded.FlatMap(entries => ops.MergeToMap(prefix, entries));
        }

        public override DataResult<Pair<IReadOnlyDictionary<TKey, TValue>, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input) =>
            ops.GetMapValues(input).FlatMap(entries =>
            {
                DataResult<IReadOnlyDictionary<TKey, TValue>> decoded =
                    DataResult<IReadOnlyDictionary<TKey, TValue>>.Success(
                        new Dictionary<TKey, TValue>());

                foreach (KeyValuePair<TData, TData> entry in entries)
                {
                    DataResult<KeyValuePair<TKey, TValue>> decodedEntry =
                        _keyCodec.Parse(ops, entry.Key).Apply2(
                            static (key, value) => new KeyValuePair<TKey, TValue>(key, value),
                            _valueCodec.Parse(ops, entry.Value));

                    decoded = decoded.Apply2(
                        static (values, next) =>
                        {
                            var combined = new Dictionary<TKey, TValue>(values)
                            {
                                [next.Key] = next.Value
                            };
                            return (IReadOnlyDictionary<TKey, TValue>)combined;
                        },
                        decodedEntry);
                }

                return decoded.Map(values =>
                    new Pair<IReadOnlyDictionary<TKey, TValue>, TData>(values, input));
            });

        public override string ToString() =>
            $"UnboundedMap[{_keyCodec} -> {_valueCodec}]";
    }
}
