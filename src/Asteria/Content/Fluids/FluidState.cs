using Asteria.Content.Blocks;
using Asteria.Core;
using Asteria.Core.Shapes;
using Asteria.World;

namespace Asteria.Content.Fluids;

public sealed class FluidState : StateHolder<Fluid, FluidState>
{
    public const int AmountMax = 9;
    public const int AmountFull = 8;

    public Fluid Fluid => Owner;

    internal FluidState(StateDefinition<Fluid, FluidState> definition, object[] values)
        : base(definition, values)
    {
    }

    public bool IsSource() => Fluid.IsSource(this);

    public bool IsSourceOfType(Fluid fluidType)
    {
        ArgumentNullException.ThrowIfNull(fluidType);
        return ReferenceEquals(Fluid, fluidType) && Fluid.IsSource(this);
    }

    public bool IsEmpty() => Fluid.IsEmpty;

    public bool IsRandomlyTicking() => Fluid.IsRandomlyTicking;

    public int GetAmount() => Fluid.GetAmount(this);

    public float GetHeight(BlockGetter level, BlockPos position) =>
        Fluid.GetHeight(this, level, position);

    public float GetHeightForCamera(BlockGetter level, BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(position);

        BlockPos above = position.Above();
        if (IsSource() && level.GetBlockState(above).IsFaceSturdy(level, above, Direction.DOWN))
            return 1.0f;

        return GetHeight(level, position);
    }

    public float GetOwnHeight() => Fluid.GetOwnHeight(this);

    public VoxelShape GetShape(BlockGetter level, BlockPos position) =>
        Fluid.GetShape(this, level, position);

    public AABB? GetAABB(BlockGetter level, BlockPos position) =>
        Fluid.GetAABB(this, level, position);

    public bool ShouldRenderBackwardUpFace(BlockGetter level, BlockPos above)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(above);

        for (int offsetX = -1; offsetX <= 1; offsetX++)
        {
            for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
            {
                BlockPos position = above.Offset(offsetX, 0, offsetZ);
                FluidState neighborFluid = level.GetFluidState(position);
                if (neighborFluid.Fluid.IsSame(Fluid) || level.GetBlockState(position).IsSolidRender)
                    continue;

                return true;
            }
        }

        return false;
    }

    public bool IsFull() => GetAmount() == AmountFull;
}
