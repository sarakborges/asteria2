using Asteria.Serialization;
using Asteria.Util.Random;

namespace Asteria.World.LevelGen;

/// <summary>Selects and samples one height provider from a weighted distribution.</summary>
public sealed class WeightedListHeight : HeightProvider
{
    public WeightedList<HeightProvider> Distribution { get; }

    public static MapCodec<WeightedListHeight> MAP_CODEC { get; } =
        WeightedList<HeightProvider>.CreateNonEmptyCodec(HeightProvider.CODEC)
            .FieldOf("distribution")
            .XMap<WeightedListHeight>(
                static distribution => new WeightedListHeight(distribution),
                static provider => provider.Distribution);

    public static MapCodec<HeightProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<HeightProvider>(
            static provider => provider,
            static provider => (WeightedListHeight)provider);

    public WeightedListHeight(WeightedList<HeightProvider> distribution) =>
        Distribution = distribution ?? throw new ArgumentNullException(nameof(distribution));

    public override int Sample(IRandomSource random, WorldGenerationContext context)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(context);
        return Distribution.GetRandomOrThrow(random)!.Sample(random, context);
    }

    public override MapCodec<HeightProvider> Codec() => TYPE_CODEC;
}
