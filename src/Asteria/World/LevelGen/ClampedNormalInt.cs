using Asteria.Core;
using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Clamped normal-distribution integer provider from Minecraft Java 26.3.</summary>
public sealed record ClampedNormalInt(
    float Mean,
    float Deviation,
    int Minimum,
    int Maximum) : IntProvider
{
    public static MapCodec<ClampedNormalInt> MAP_CODEC { get; } =
        RecordCodecBuilder.MapCodec<ClampedNormalInt>(instance =>
            instance.Group(
                    Codecs.FLOAT.FieldOf("mean")
                        .ForGetter<ClampedNormalInt>(static provider => provider.Mean),
                    Codecs.FLOAT.FieldOf("deviation")
                        .ForGetter<ClampedNormalInt>(static provider => provider.Deviation),
                    Codecs.INT.FieldOf("min_inclusive")
                        .ForGetter<ClampedNormalInt>(static provider => provider.Minimum),
                    Codecs.INT.FieldOf("max_inclusive")
                        .ForGetter<ClampedNormalInt>(static provider => provider.Maximum))
                .Apply(
                    instance,
                    static (mean, deviation, minimum, maximum) =>
                        new ClampedNormalInt(mean, deviation, minimum, maximum)))
        .Validate(static provider =>
            provider.Maximum < provider.Minimum
                ? DataResult<ClampedNormalInt>.Error(() =>
                    $"Max must be larger than min: [{provider.Minimum}, {provider.Maximum}]")
                : DataResult<ClampedNormalInt>.Success(provider));

    public static MapCodec<IntProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<IntProvider>(
            static provider => provider,
            static provider => (ClampedNormalInt)provider);

    public static ClampedNormalInt Of(
        float mean,
        float deviation,
        int minimum,
        int maximum) =>
        new(mean, deviation, minimum, maximum);

    public int Sample(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return Sample(random, Mean, Deviation, Minimum, Maximum);
    }

    public static int Sample(
        IRandomSource random,
        float mean,
        float deviation,
        float minimum,
        float maximum)
    {
        ArgumentNullException.ThrowIfNull(random);
        float normal = mean + (float)random.NextGaussian() * deviation;
        return JavaFloatToInt(Mth.Clamp(normal, minimum, maximum));
    }

    public int MinInclusive() => Minimum;

    public int MaxInclusive() => Maximum;

    public MapCodec<IntProvider> Codec() => TYPE_CODEC;

    private static int JavaFloatToInt(float value)
    {
        if (float.IsNaN(value))
            return 0;
        if (value >= int.MaxValue)
            return int.MaxValue;
        if (value <= int.MinValue)
            return int.MinValue;
        return (int)value;
    }

    public override string ToString() =>
        $"normal({Mean.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
        $"{Deviation.ToString(System.Globalization.CultureInfo.InvariantCulture)}) in " +
        $"[{Minimum}-{Maximum}]";
}
