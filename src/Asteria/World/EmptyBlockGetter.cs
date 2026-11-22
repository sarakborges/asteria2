using Asteria.Content.Blocks;
using Asteria.Content.Fluids;
using Asteria.Core;

namespace Asteria.World;

/// <summary>
/// Empty world context used for block-state shape and cache initialization.
/// </summary>
public sealed class EmptyBlockGetter : BlockGetter
{
    public static EmptyBlockGetter Instance { get; } = new();

    private EmptyBlockGetter()
    {
    }

    public BlockState GetBlockState(BlockPos position) =>
        Blocks.AIR.DefaultState;

    public FluidState GetFluidState(BlockPos position) =>
        Fluids.EMPTY.DefaultFluidState;

    public int GetHeight() => 0;

    public int GetMinY() => 0;
}
