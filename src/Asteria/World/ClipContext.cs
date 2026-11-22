using Asteria.Content.Blocks;
using Asteria.Content.Fluids;
using Asteria.Core;
using Asteria.Core.Shapes;

namespace Asteria.World;

/// <summary>
/// Selects block and fluid geometry for a ray clip, matching the supported
/// selectors from Minecraft's ClipContext.
/// </summary>
public sealed class ClipContext
{
    public enum Block
    {
        Collider,
        Outline,
        Visual
    }

    public enum Fluid
    {
        None,
        SourceOnly,
        Any
    }

    private readonly Block _block;
    private readonly Fluid _fluid;

    public Vec3 From { get; }

    public Vec3 To { get; }

    public ClipContext(Vec3 from, Vec3 to, Block block, Fluid fluid)
    {
        From = from ?? throw new ArgumentNullException(nameof(from));
        To = to ?? throw new ArgumentNullException(nameof(to));
        _block = block;
        _fluid = fluid;
    }

    public Vec3 GetFrom() => From;

    public Vec3 GetTo() => To;

    public VoxelShape GetBlockShape(BlockState state, BlockGetter level, BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(position);

        return _block switch
        {
            Block.Collider => state.GetCollisionShape(level, position),
            Block.Outline => state.GetShape(level, position),
            Block.Visual => state.GetVisualShape(level, position),
            _ => throw new ArgumentOutOfRangeException(nameof(_block), _block, "Unsupported block clip mode.")
        };
    }

    public VoxelShape GetFluidShape(FluidState state, BlockGetter level, BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(position);

        bool canPick = _fluid switch
        {
            Fluid.None => false,
            Fluid.SourceOnly => state.IsSource(),
            Fluid.Any => !state.IsEmpty(),
            _ => throw new ArgumentOutOfRangeException(nameof(_fluid), _fluid, "Unsupported fluid clip mode.")
        };

        return canPick ? state.GetShape(level, position) : Shapes.Empty();
    }
}
