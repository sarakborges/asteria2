namespace Asteria.Serialization;

/// <summary>Built-in codecs for common primitive and constrained values.</summary>
public static class Codecs
{
    public static readonly Codec<bool> BOOL = new BooleanCodec();
    public static readonly Codec<int> INT = new IntegerCodec();
    public static readonly Codec<float> FLOAT = new FloatCodec();
    public static readonly Codec<double> DOUBLE = new DoubleCodec();
    public static readonly Codec<string> STRING = new StringCodec();

    /// <summary>
    /// Creates a codec for a map whose value codec is selected independently for each decoded key.
    /// Keys and values retain the DynamicOps map and DataResult lifecycle contracts.
    /// </summary>
    public static Codec<IReadOnlyDictionary<TKey, TValue>> DispatchedMap<TKey, TValue>(
        Codec<TKey> keyCodec,
        Func<TKey, Codec<TValue>> valueCodec)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keyCodec);
        ArgumentNullException.ThrowIfNull(valueCodec);
        return new DispatchedMapCodec<TKey, TValue>(keyCodec, valueCodec);
    }

    public static Codec<int> IntRange(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxInclusive));

        return INT.Validate(value =>
            value >= minInclusive && value <= maxInclusive
                ? DataResult<int>.Success(value)
                : DataResult<int>.Error(
                    () => $"Value {value} outside of range [{minInclusive}: {maxInclusive}]"));
    }

    public static Codec<float> FloatRange(float minInclusive, float maxInclusive)
    {
        if (float.IsNaN(minInclusive) || float.IsNaN(maxInclusive) ||
            maxInclusive < minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxInclusive));

        return FLOAT.Validate(value =>
            value >= minInclusive && value <= maxInclusive
                ? DataResult<float>.Success(value)
                : DataResult<float>.Error(
                    () => $"Value {value} outside of range [{minInclusive}: {maxInclusive}]"));
    }

    private sealed class BooleanCodec : PrimitiveCodec<bool>
    {
        public override DataResult<bool> Read<TData>(DynamicOps<TData> ops, TData input) =>
            ops.GetBooleanValue(input);

        public override TData Write<TData>(DynamicOps<TData> ops, bool value) =>
            ops.CreateBoolean(value);

        public override string ToString() => "Bool";
    }

    private sealed class IntegerCodec : PrimitiveCodec<int>
    {
        public override DataResult<int> Read<TData>(DynamicOps<TData> ops, TData input) =>
            ops.GetNumberValue(input).Map(number => number.IntValue);

        public override TData Write<TData>(DynamicOps<TData> ops, int value) =>
            ops.CreateInt(value);

        public override string ToString() => "Int";
    }

    private sealed class FloatCodec : PrimitiveCodec<float>
    {
        public override DataResult<float> Read<TData>(DynamicOps<TData> ops, TData input) =>
            ops.GetNumberValue(input).Map(number => number.FloatValue);

        public override TData Write<TData>(DynamicOps<TData> ops, float value) =>
            ops.CreateFloat(value);

        public override string ToString() => "Float";
    }

    private sealed class DoubleCodec : PrimitiveCodec<double>
    {
        public override DataResult<double> Read<TData>(DynamicOps<TData> ops, TData input) =>
            ops.GetNumberValue(input).Map(number => number.DoubleValue);

        public override TData Write<TData>(DynamicOps<TData> ops, double value) =>
            ops.CreateDouble(value);

        public override string ToString() => "Double";
    }

    private sealed class StringCodec : PrimitiveCodec<string>
    {
        public override DataResult<string> Read<TData>(DynamicOps<TData> ops, TData input) =>
            ops.GetStringValue(input);

        public override TData Write<TData>(DynamicOps<TData> ops, string value) =>
            ops.CreateString(value);

        public override string ToString() => "String";
    }
}
