namespace Asteria.Serialization;

/// <summary>
/// Codec for a primitive value. Like DataFixerUpper, decoding returns the format's empty
/// remainder and encoding delegates prefix handling to MergeToPrimitive.
/// </summary>
public abstract class PrimitiveCodec<TValue> : Codec<TValue>
{
    public abstract DataResult<TValue> Read<TData>(
        DynamicOps<TData> ops,
        TData input);

    public abstract TData Write<TData>(
        DynamicOps<TData> ops,
        TValue value);

    public sealed override DataResult<Pair<TValue, TData>> Decode<TData>(
        DynamicOps<TData> ops,
        TData input) =>
        Read(ops, input).Map(value =>
            new Pair<TValue, TData>(value, ops.Empty));

    public sealed override DataResult<TData> Encode<TData>(
        TValue input,
        DynamicOps<TData> ops,
        TData prefix) =>
        ops.MergeToPrimitive(prefix, Write(ops, input));
}
