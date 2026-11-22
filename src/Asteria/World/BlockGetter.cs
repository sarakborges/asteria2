using Asteria.Content.Blocks;
using Asteria.Content.Fluids;
using Asteria.Core;
using Asteria.Core.Shapes;

namespace Asteria.World;

/// <summary>
/// Provides block and fluid state access over a level's build-height bounds.
/// </summary>
public interface BlockGetter : LevelHeightAccessor
{
    BlockState GetBlockState(BlockPos position);

    FluidState GetFluidState(BlockPos position);

    int GetLightEmission(BlockPos position) =>
        GetBlockState(position).GetLightEmission();

    IEnumerable<BlockState> GetBlockStates(AABB box)
    {
        ArgumentNullException.ThrowIfNull(box);

        BlockPos min = BlockPos.Containing(box.MinX, box.MinY, box.MinZ);
        BlockPos max = BlockPos.Containing(box.MaxX, box.MaxY, box.MaxZ);
        return BlockPos.BetweenClosed(min, max).Select(GetBlockState);
    }

    double GetBlockFloorHeight(VoxelShape blockShape, Func<VoxelShape> belowBlockShape)
    {
        ArgumentNullException.ThrowIfNull(blockShape);

        if (!blockShape.IsEmpty())
            return blockShape.Max(Direction.Axis.Y);

        ArgumentNullException.ThrowIfNull(belowBlockShape);
        double belowFloor = belowBlockShape().Max(Direction.Axis.Y);
        return belowFloor >= 1.0 ? belowFloor - 1.0 : double.NegativeInfinity;
    }

    double GetBlockFloorHeight(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);

        VoxelShape blockShape = GetBlockState(position).GetCollisionShape(this, position);
        return GetBlockFloorHeight(blockShape, () =>
        {
            BlockPos below = position.Below();
            return GetBlockState(below).GetCollisionShape(this, below);
        });
    }
}
