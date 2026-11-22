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

    static TResult TraverseBlocks<TContext, TResult>(
        Vec3 from,
        Vec3 to,
        TContext context,
        Func<TContext, BlockPos, TResult?> consumer,
        Func<TContext, TResult> missFactory)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(consumer);
        ArgumentNullException.ThrowIfNull(missFactory);

        if (from.Equals(to))
            return missFactory(context);

        double toX = Mth.Lerp(-1.0E-7, to.X, from.X);
        double toY = Mth.Lerp(-1.0E-7, to.Y, from.Y);
        double toZ = Mth.Lerp(-1.0E-7, to.Z, from.Z);
        double fromX = Mth.Lerp(-1.0E-7, from.X, to.X);
        double fromY = Mth.Lerp(-1.0E-7, from.Y, to.Y);
        double fromZ = Mth.Lerp(-1.0E-7, from.Z, to.Z);

        int currentBlockX = Mth.Floor(fromX);
        int currentBlockY = Mth.Floor(fromY);
        int currentBlockZ = Mth.Floor(fromZ);
        var position = new BlockPos.MutableBlockPos(
            currentBlockX,
            currentBlockY,
            currentBlockZ);

        TResult? first = consumer(context, position);
        if (first is not null)
            return first;

        double dx = toX - fromX;
        double dy = toY - fromY;
        double dz = toZ - fromZ;
        int signX = Mth.Sign(dx);
        int signY = Mth.Sign(dy);
        int signZ = Mth.Sign(dz);
        double tDeltaX = signX == 0 ? double.MaxValue : signX / dx;
        double tDeltaY = signY == 0 ? double.MaxValue : signY / dy;
        double tDeltaZ = signZ == 0 ? double.MaxValue : signZ / dz;
        double tX = tDeltaX * (signX > 0 ? 1.0 - Mth.Frac(fromX) : Mth.Frac(fromX));
        double tY = tDeltaY * (signY > 0 ? 1.0 - Mth.Frac(fromY) : Mth.Frac(fromY));
        double tZ = tDeltaZ * (signZ > 0 ? 1.0 - Mth.Frac(fromZ) : Mth.Frac(fromZ));

        while (tX <= 1.0 || tY <= 1.0 || tZ <= 1.0)
        {
            if (tX < tY)
            {
                if (tX < tZ)
                {
                    currentBlockX += signX;
                    tX += tDeltaX;
                }
                else
                {
                    currentBlockZ += signZ;
                    tZ += tDeltaZ;
                }
            }
            else if (tY < tZ)
            {
                currentBlockY += signY;
                tY += tDeltaY;
            }
            else
            {
                currentBlockZ += signZ;
                tZ += tDeltaZ;
            }

            TResult? result = consumer(
                context,
                position.Set(currentBlockX, currentBlockY, currentBlockZ));
            if (result is not null)
                return result;
        }

        return missFactory(context);
    }

    BlockHitResult IsBlockInLine(ClipBlockStateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return TraverseBlocks<ClipBlockStateContext, BlockHitResult>(
            context.GetFrom(),
            context.GetTo(),
            context,
            (clipContext, position) =>
            {
                BlockState state = GetBlockState(position);
                Vec3 delta = clipContext.GetFrom().Subtract(clipContext.GetTo());
                if (!clipContext.IsTargetBlock(state))
                    return null;

                Vec3 end = clipContext.GetTo();
                return new BlockHitResult(
                    end,
                    Direction.GetApproximateNearest(delta.X, delta.Y, delta.Z),
                    BlockPos.Containing(end.X, end.Y, end.Z),
                    inside: false);
            },
            clipContext =>
            {
                Vec3 end = clipContext.GetTo();
                Vec3 delta = clipContext.GetFrom().Subtract(end);
                return BlockHitResult.Miss(
                    end,
                    Direction.GetApproximateNearest(delta.X, delta.Y, delta.Z),
                    BlockPos.Containing(end.X, end.Y, end.Z));
            });
    }

    BlockHitResult? ClipWithInteractionOverride(
        Vec3 from,
        Vec3 to,
        BlockPos position,
        VoxelShape blockShape,
        BlockState blockState)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(blockShape);
        ArgumentNullException.ThrowIfNull(blockState);

        BlockHitResult? result = blockShape.Clip(from, to, position);
        if (result is not null)
        {
            BlockHitResult? interactionHit =
                blockState.GetInteractionShape(this, position).Clip(from, to, position);
            if (interactionHit is not null &&
                interactionHit.GetLocation().DistanceToSquared(from) <
                result.GetLocation().DistanceToSquared(from))
            {
                return result.WithDirection(interactionHit.GetDirection());
            }
        }

        return result;
    }
}
