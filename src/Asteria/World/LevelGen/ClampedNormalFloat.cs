using Asteria.Core;
using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Clamped normal-distribution float provider from Minecraft Java 26.3.</summary>
public sealed record ClampedNormalFloat(
    float Mean,
    float Deviation,
    float Minimum,
    float Maximum) : FloatProvider
{
    public static MapCodec<ClampedNormalFloat> MAP_CODEC { get; } =
        RecordCodecBuilder.MapCodec<ClampedNormalFloat>(instance =>
            instance.Group(
                    Codecs.FLOAT.FieldOf("mean")
                        .ForGetter<ClampedNormalFloat>(static provider => provider.Mean),
                    Codecs.FLOAT.FieldOf("deviation")
                        .ForGetter<ClampedNormalFloat>(static provider => provider.Deviation),
                    Codecs.FLOAT.FieldOf("min")
                        .ForGetter<ClampedNormalFloat>(static provider => provider.Minimum),
                    Codecs.FLOAT.FieldOf("max")
                        .ForGetter<ClampedNormalFloat>(static provider => provider.Maximum))
                .Apply(
                    instance,
                    static (mean, deviation, minimum, maximum) =>
                        new ClampedNormalFloat(mean, deviation, minimum, maximum)))
        .Validate(static provider =>
            provider.Maximum < provider.Minimum
                ? DataResult<ClampedNormalFloat>.Error(() =>
                    $"Max must be larger than min: [{provider.Minimum}, {provider.Maximum}]")
                : DataResult<ClampedNormalFloat>.Success(provider));

    public static MapCodec<FloatProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<FloatProvider>(
            static provider => provider,
            static provider => (ClampedNormalFloat)provider);

    public static ClampedNormalFloat Of(
        float mean,
        float deviation,
        float minimum,
        float maximum) =>
        new(mean, deviation, minimum, maximum);

    public float Sample(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return Sample(random, Mean, Deviation, Minimum, Maximum);
    }

    public static float Sample(
        IRandomSource random,
        float mean,
        float deviation,
        float minimum,
        float maximum)
    {
        ArgumentNullException.ThrowIfNull(random);
        float normal = mean + (float)random.NextGaussian() * deviation;
        return Mth.Clamp(normal, minimum, maximum);
    }

    public float Min() => Minimum;

    public float Max() => Maximum;

    public MapCodec<FloatProvider> Codec() => TYPE_CODEC;

    public override string ToString() =>
        $"normal({Mean.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
        $"{Deviation.ToString(System.Globalization.CultureInfo.InvariantCulture)}) in " +
        $"[{Minimum.ToString(System.Globalization.CultureInfo.InvariantCulture)}-" +
        $"{Maximum.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
}
