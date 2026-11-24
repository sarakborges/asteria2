using Asteria.Core;
using Asteria.Serialization;
using Asteria.World.LevelGen;

namespace Asteria.World.LevelGen.Placement;

/// <summary>Offsets X and Z independently within a 16×16 chunk column.</summary>
public sealed class InSquarePlacement : PlacementModifier
{
    public static InSquarePlacement INSTANCE { get; } = new();

    public static MapCodec<InSquarePlacement> MAP_CODEC { get; } =
        MapCodec<InSquarePlacement>.Unit(INSTANCE);

    public static MapCodec<PlacementModifier> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<PlacementModifier>(
            static modifier => modifier,
            static modifier => (InSquarePlacement)modifier);

    private InSquarePlacement()
    {
    }

    public static InSquarePlacement Spread() => INSTANCE;

    public void Modify(
        PlacementContext context,
        IRandomSource random,
        BlockPos origin,
        Action<BlockPos> output)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(output);

        int x = random.NextInt(16) + origin.GetX();
        int z = random.NextInt(16) + origin.GetZ();
        output(new BlockPos(x, origin.GetY(), z));
    }

    public MapCodec<PlacementModifier> Codec() => TYPE_CODEC;
}
