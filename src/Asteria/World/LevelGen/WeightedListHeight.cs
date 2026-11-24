using Asteria.Serialization;
using Asteria.Util.Random;

namespace Asteria.World.LevelGen;

/// <summary>Selects and samples one height provider from a weighted distribution.</summary>
public sealed record WeightedListHeight(
    WeightedList<HeightProvider> Distribution) : HeightProvider
{
    public static MapCodec<WeightedListHeight> MAP_CODEC { get; } =
        RecordCodecBuilder.MapCodec<WeightedListHeight>(instance =>
            instance.Group(
                    WeightedList<HeightProvider>.CreateNonEmptyCodec(HeightProvider.CODEC)
                        .FieldOf("distribution")
                        .ForGetter<WeightedListHeight>(static provider => provider.Distribution))
                .Apply(
                    instance,
                    static distribution => new WeightedListHeight(distribution)));

    public static MapCodec<HeightProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<HeightProvider>(
            static provider => provider,
            static provider => (WeightedListHeight)provider);

    public override int Sample(IRandomSource random, WorldGenerationContext context)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(context);
        return Distribution.GetRandomOrThrow(random)!.Sample(random, context);
    }

    public override MapCodec<HeightProvider> Codec() => TYPE_CODEC;
}
