using Asteria.Core;
using Asteria.Core.Shapes;
using Asteria.World;

namespace Asteria.Content.Fluids;

public sealed class EmptyFluid : Fluid
{
    public override bool IsEmpty => true;

    public override bool IsSource(FluidState state) => false;

    public override int GetAmount(FluidState state) => 0;

    public override float GetHeight(FluidState state, BlockGetter level, BlockPos position) => 0.0f;

    public override float GetOwnHeight(FluidState state) => 0.0f;

    public override VoxelShape GetShape(FluidState state, BlockGetter level, BlockPos position) =>
        Shapes.Empty();
}
