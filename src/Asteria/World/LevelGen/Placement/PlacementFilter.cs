using Asteria.Core;

namespace Asteria.World.LevelGen.Placement;

/// <summary>Filter modifier that emits its input position only when its predicate passes.</summary>
public interface PlacementFilter : PlacementModifier
{
    bool ShouldPlace(PlacementContext context, IRandomSource random, BlockPos origin);

    void PlacementModifier.Modify(
        PlacementContext context,
        IRandomSource random,
        BlockPos origin,
        Action<BlockPos> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (ShouldPlace(context, random, origin))
            output(origin);
    }
}
