using Asteria.Core;
using Asteria.Core.Shapes;
using Asteria.World;

namespace Asteria.Content.Fluids;

/// <summary>
/// Shared behavior for built-in still/source fluid types. Flowing-fluid state transitions
/// and fluid simulation remain separate ports.
/// </summary>
public abstract class SourceFluid : Fluid
{
    protected SourceFluid()
    {
    }

    public sealed override bool IsSource(FluidState state) => true;

    public sealed override int GetAmount(FluidState state) =>
        FluidState.AmountFull;

    public sealed override float GetHeight(
        FluidState state,
        BlockGetter level,
        BlockPos position)
    {
        FluidState above = level.GetFluidState(position.Above());
        return above.Fluid.IsSame(this) ? 1.0f : GetOwnHeight(state);
    }

    public sealed override float GetOwnHeight(FluidState state) =>
        state.GetAmount() / 9.0f;

    public sealed override VoxelShape GetShape(
        FluidState state,
        BlockGetter level,
        BlockPos position)
    {
        float height = GetHeight(state, level, position);
        return Shapes.Box(0.0, 0.0, 0.0, 1.0, height, 1.0);
    }
}
