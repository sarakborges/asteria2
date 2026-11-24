using Asteria.Core;
using Asteria.Serialization;
using Asteria.World.LevelGen;

namespace Asteria.World.LevelGen.Placement;

/// <summary>Emits a position with probability 1/chance.</summary>
public sealed class RarityFilter : PlacementFilter
{
    public int Chance { get; }

    public static MapCodec<RarityFilter> MAP_CODEC { get; } =
        Codecs.IntRange(1, int.MaxValue)
            .FieldOf("chance")
            .XMap<RarityFilter>(
                static chance => new RarityFilter(chance),
                static filter => filter.Chance);

    public static MapCodec<PlacementModifier> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<PlacementModifier>(
            static modifier => modifier,
            static modifier => (RarityFilter)modifier);

    public RarityFilter(int chance) => Chance = chance;

    public static RarityFilter OnAverageOnceEvery(int chance) =>
        new(chance);

    public override bool ShouldPlace(
        PlacementContext context,
        IRandomSource random,
        BlockPos origin)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(origin);
        return random.NextFloat() < 1.0f / Chance;
    }

    public override MapCodec<PlacementModifier> Codec() => TYPE_CODEC;
}
