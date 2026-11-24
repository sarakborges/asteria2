using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Uniform half-open world-generation float provider from Minecraft Java 26.3.</summary>
public sealed record UniformFloat(float Minimum, float Maximum) : FloatProvider
{
    public static MapCodec<UniformFloat> MAP_CODEC { get; } =
        RecordCodecBuilder.MapCodec<UniformFloat>(instance =>
            instance.Group(
                    Codecs.FLOAT.FieldOf("min_inclusive")
                        .ForGetter<UniformFloat>(static provider => provider.Minimum),
                    Codecs.FLOAT.FieldOf("max_exclusive")
                        .ForGetter<UniformFloat>(static provider => provider.Maximum))
                .Apply(
                    instance,
                    static (minimum, maximum) => new UniformFloat(minimum, maximum)))
        .Validate(static provider =>
            provider.Maximum <= provider.Minimum
                ? DataResult<UniformFloat>.Error(() =>
                    $"Max must be larger than min, min: {provider.Minimum}, max: {provider.Maximum}")
                : DataResult<UniformFloat>.Success(provider));

    public static MapCodec<FloatProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<FloatProvider>(
            static provider => provider,
            static provider => (UniformFloat)provider);

    public static UniformFloat Of(float minimum, float maximum)
    {
        if (maximum <= minimum)
            throw new ArgumentException("Max must exceed min.");

        return new UniformFloat(minimum, maximum);
    }

    public float Sample(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return Minimum + random.NextFloat() * (Maximum - Minimum);
    }

    public float Min() => Minimum;

    public float Max() => Maximum;

    public MapCodec<FloatProvider> Codec() => TYPE_CODEC;

    public override string ToString() =>
        $"[{Minimum.ToString(System.Globalization.CultureInfo.InvariantCulture)}-" +
        $"{Maximum.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
}
