using Asteria.Core;
using Asteria.Serialization;
using Asteria.World.LevelGen;

namespace Asteria.World.LevelGen.Placement;

/// <summary>Base class for placement filters that emit their input only when a predicate passes.</summary>
public abstract class PlacementFilter : PlacementModifier
{
    public abstract bool ShouldPlace(PlacementContext context, IRandomSource random, BlockPos origin);

    public abstract MapCodec<PlacementModifier> Codec();

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

        if (ShouldPlace(context, random, origin))
            output(origin);
    }
}
