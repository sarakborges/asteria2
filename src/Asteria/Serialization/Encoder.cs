namespace Asteria.Serialization;

/// <summary>Encodes values into a format described by DynamicOps.</summary>
public interface Encoder<TValue>
{
    DataResult<TData> Encode<TData>(
        TValue input,
        DynamicOps<TData> ops,
        TData prefix);

    DataResult<TData> EncodeStart<TData>(
        DynamicOps<TData> ops,
        TValue input) =>
        Encode(input, ops, ops.Empty);
}

/// <summary>Decodes values from a format described by DynamicOps.</summary>
public interface Decoder<TValue>
{
    DataResult<Pair<TValue, TData>> Decode<TData>(
        DynamicOps<TData> ops,
        TData input);

    DataResult<TValue> Parse<TData>(
        DynamicOps<TData> ops,
        TData input) =>
        Decode(ops, input).Map(pair => pair.First);
}
