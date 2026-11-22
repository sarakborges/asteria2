using Asteria.Content.Blocks;
using Asteria.Content.Fluids;
using Asteria.Core;

namespace Asteria.World;

/// <summary>
/// Provides block and fluid state access over a level's build-height bounds.
/// </summary>
public interface BlockGetter : LevelHeightAccessor
{
    BlockState GetBlockState(BlockPos position);

    FluidState GetFluidState(BlockPos position);
}
