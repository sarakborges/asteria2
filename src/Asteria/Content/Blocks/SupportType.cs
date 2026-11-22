using Asteria.Core;
using Asteria.Core.Shapes;
using Asteria.World;

namespace Asteria.Content.Blocks;

/// <summary>
/// Describes the minimum face geometry required to support a block.
/// </summary>
public enum SupportType
{
    Full,
    Center,
    Rigid
}

public static class SupportTypeExtensions
{
    private static readonly VoxelShape CENTER_SUPPORT_SHAPE =
        Shapes.Box(7.0 / 16.0, 0.0, 7.0 / 16.0, 9.0 / 16.0, 10.0 / 16.0, 9.0 / 16.0);

    private static readonly VoxelShape RIGID_SUPPORT_SHAPE =
        Shapes.Join(
            Shapes.Block(),
            Shapes.Box(2.0 / 16.0, 0.0, 2.0 / 16.0, 14.0 / 16.0, 1.0, 14.0 / 16.0),
            BooleanOp.ONLY_FIRST);

    public static bool IsSupporting(
        this SupportType supportType,
        BlockState state,
        BlockGetter level,
        BlockPos position,
        Direction direction)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(direction);

        VoxelShape faceShape = state.GetBlockSupportShape(level, position).GetFaceShape(direction);
        return supportType switch
        {
            SupportType.Full => Block.IsShapeFullBlock(faceShape),
            SupportType.Center => !Shapes.JoinIsNotEmpty(
                faceShape,
                CENTER_SUPPORT_SHAPE,
                BooleanOp.ONLY_SECOND),
            SupportType.Rigid => !Shapes.JoinIsNotEmpty(
                faceShape,
                RIGID_SUPPORT_SHAPE,
                BooleanOp.ONLY_SECOND),
            _ => throw new ArgumentOutOfRangeException(nameof(supportType), supportType, "Unsupported support type.")
        };
    }
}
