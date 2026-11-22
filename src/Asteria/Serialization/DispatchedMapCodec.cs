using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace Asteria.Serialization;

/// <summary>
/// A dynamic map codec whose value codec is selected by each decoded key.
/// </summary>
internal sealed class DispatchedMapCodec<TKey, TValue> :
    Codec<IReadOnlyDictionary<TKey, TValue>>
    where TKey : notnull
{
    private readonly Codec<TKey> _keyCodec;
    private readonly Func<TKey, Codec<TValue>> _valueCodec;
    private readonly ConcurrentDictionary<TKey, Codec<TValue>> _valueCodecs = new();

    public DispatchedMapCodec(
        Codec<TKey> keyCodec,
        Func<TKey, Codec<TValue>> valueCodec)
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

        DataResult<IReadOnlyList<KeyValuePair<TData, TData>>> encodedEntries =
            DataResult<IReadOnlyList<KeyValuePair<TData, TData>>>.Success(
                Array.Empty<KeyValuePair<TData, TData>>());

        foreach (KeyValuePair<TKey, TValue> entry in input)
        {
            DataResult<KeyValuePair<TData, TData>> encodedEntry =
                _keyCodec.EncodeStart(ops, entry.Key).Apply2(
                    static (key, value) => new KeyValuePair<TData, TData>(key, value),
                    GetValueCodec(entry.Key).EncodeStart(ops, entry.Value));

            encodedEntries = encodedEntries.Apply2(
                static (entries, encoded) =>
                {
                    var result = new List<KeyValuePair<TData, TData>>(entries.Count + 1);
                    result.AddRange(entries);
                    result.Add(encoded);
                    return (IReadOnlyList<KeyValuePair<TData, TData>>)result;
                },
                encodedEntry);
        }

        return encodedEntries.FlatMap(entries => ops.MergeToMap(prefix, entries));
    }

    public override DataResult<Pair<IReadOnlyDictionary<TKey, TValue>, TData>> Decode<TData>(
        DynamicOps<TData> ops,
        TData input) =>
        ops.GetMapValues(input).FlatMap(entries =>
        {
            DataResult<IReadOnlyDictionary<TKey, TValue>> decoded =
                DataResult<IReadOnlyDictionary<TKey, TValue>>.Success(
                    new ReadOnlyDictionary<TKey, TValue>(new Dictionary<TKey, TValue>()));

            foreach (KeyValuePair<TData, TData> entry in entries)
            {
                DataResult<KeyValuePair<TKey, TValue>> decodedEntry =
                    _keyCodec.Parse(ops, entry.Key).FlatMap(key =>
                        GetValueCodec(key).Parse(ops, entry.Value).Map(value =>
                            new KeyValuePair<TKey, TValue>(key, value)));

                decoded = decoded.Apply2(
                    static (values, value) => Put(values, value.Key, value.Value),
                    decodedEntry);
            }

            return decoded.Map(values =>
                new Pair<IReadOnlyDictionary<TKey, TValue>, TData>(values, ops.Empty));
        });

    private Codec<TValue> GetValueCodec(TKey key) =>
        _valueCodecs.GetOrAdd(
            key,
            value => _valueCodec(value) ??
                throw new InvalidOperationException("A dispatched map value codec cannot be null."));

    private static IReadOnlyDictionary<TKey, TValue> Put(
        IReadOnlyDictionary<TKey, TValue> values,
        TKey key,
        TValue value)
    {
        var result = values.ToDictionary(pair => pair.Key, pair => pair.Value);
        result[key] = value;
        return new ReadOnlyDictionary<TKey, TValue>(result);
    }
}
