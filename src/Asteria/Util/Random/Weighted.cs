using Asteria.Serialization;

namespace Asteria.Util.Random;

/// <summary>A value and its non-negative selection weight.</summary>
public sealed record Weighted<T>
{
    public T Value { get; }
    public int Weight { get; }

    public Weighted(T value, int weight)
    {
        if (weight < 0)
            throw new ArgumentException("Weight should be >= 0");

        Value = value;
        Weight = weight;
    }

    public Weighted<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return new Weighted<TResult>(mapper(Value), Weight);
    }

    public override int GetHashCode()
    {
        int valueHash = Value is null ? 0 : EqualityComparer<T>.Default.GetHashCode(Value);
        return unchecked(valueHash * 31 + Weight);
    }
}

/// <summary>Codecs for weighted values.</summary>
public static class Weighted
{
    public static Codec<Weighted<T>> Codec<T>(Codec<T> elementCodec)
    {
        ArgumentNullException.ThrowIfNull(elementCodec);
        return Codec(elementCodec.FieldOf("data"));
    }

    public static Codec<Weighted<T>> Codec<T>(MapCodec<T> elementCodec)
    {
        ArgumentNullException.ThrowIfNull(elementCodec);
        return RecordCodecBuilder.Create<Weighted<T>>(instance =>
            instance.Group(
                elementCodec.ForGetter<Weighted<T>>(static weighted => weighted.Value),
                ExtraCodecs.NON_NEGATIVE_INT
                    .FieldOf("weight")
                    .ForGetter<Weighted<T>>(static weighted => weighted.Weight))
            .Apply(instance, static (value, weight) => new Weighted<T>(value, weight)));
    }
}
