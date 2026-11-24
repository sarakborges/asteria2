using Asteria.Content.Fluids;
using Asteria.Core;
using Asteria.Core.Shapes;
using Asteria.World;

namespace Asteria.Content.Blocks;

/// <summary>
/// A non-colliding block whose state exposes the associated still fluid state.
/// </summary>
public sealed class LiquidBlock : Block
{
    private readonly Fluid _fluid;

    public LiquidBlock(BlockProperties properties, Fluid fluid)
        : base(properties)
    {
        _fluid = fluid ?? throw new ArgumentNullException(nameof(fluid));
    }

    public override FluidState GetFluidState(BlockState state) =>
        _fluid.DefaultFluidState;

    public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos position) =>
        Shapes.Empty();
}
