using Asteria.Core;
using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Clamps the sampled value and effective range of another integer provider.</summary>
public sealed record ClampedInt(
    IntProvider Source,
    int Minimum,
    int Maximum) : IntProvider
{
    public static MapCodec<ClampedInt> MAP_CODEC { get; } =
        RecordCodecBuilder.MapCodec<ClampedInt>(instance =>
            instance.Group(
                    IntProviders.CODEC.FieldOf("source")
                        .ForGetter<ClampedInt>(static provider => provider.Source),
                    Codecs.INT.FieldOf("min_inclusive")
                        .ForGetter<ClampedInt>(static provider => provider.Minimum),
                    Codecs.INT.FieldOf("max_inclusive")
                        .ForGetter<ClampedInt>(static provider => provider.Maximum))
                .Apply(
                    instance,
                    static (source, minimum, maximum) =>
                        new ClampedInt(source, minimum, maximum)))
        .Validate(static provider =>
            provider.Maximum < provider.Minimum
                ? DataResult<ClampedInt>.Error(() =>
                    $"Max must be at least min, min_inclusive: {provider.Minimum}, max_inclusive: {provider.Maximum}")
                : DataResult<ClampedInt>.Success(provider));

    public static MapCodec<IntProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<IntProvider>(
            static provider => provider,
            static provider => (ClampedInt)provider);

    public static ClampedInt Of(
        IntProvider source,
        int minimum,
        int maximum) =>
        new(source, minimum, maximum);

    public int Sample(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(Source);
        ArgumentNullException.ThrowIfNull(random);
        return Mth.Clamp(Source.Sample(random), Minimum, Maximum);
    }

    public int MinInclusive() => Math.Max(Minimum, Source.MinInclusive());

    public int MaxInclusive() => Math.Min(Maximum, Source.MaxInclusive());

    public MapCodec<IntProvider> Codec() => TYPE_CODEC;

    public override string ToString() =>
        $"ClampedInt[source={Source}, minInclusive={Minimum}, maxInclusive={Maximum}]";
}
