using Asteria.Core;
using Asteria.Serialization;

namespace Asteria.World.LevelGen.Placement;

/// <summary>Placement modifier that emits the input position a sampled number of times.</summary>
public abstract class RepeatingPlacement : PlacementModifier
{
    public abstract int Count(IRandomSource random, BlockPos origin);

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

        int count = Count(random, origin);
        for (int index = 0; index < count; index++)
            output(origin);
    }

    public abstract MapCodec<PlacementModifier> Codec();
}
