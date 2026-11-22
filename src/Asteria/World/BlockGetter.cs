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

    BlockHitResult Clip(ClipContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return TraverseBlocks<ClipContext, BlockHitResult>(
            context.GetFrom(),
            context.GetTo(),
            context,
            (clipContext, position) =>
            {
                BlockState blockState = GetBlockState(position);
                FluidState fluidState = GetFluidState(position);
                Vec3 from = clipContext.GetFrom();
                Vec3 to = clipContext.GetTo();

                VoxelShape blockShape = clipContext.GetBlockShape(blockState, this, position);
                BlockHitResult? blockResult =
                    ClipWithInteractionOverride(from, to, position, blockShape, blockState);

                VoxelShape fluidShape = clipContext.GetFluidShape(fluidState, this, position);
                BlockHitResult? fluidResult = fluidShape.Clip(from, to, position);

                double blockDistanceSquared = blockResult is null
                    ? double.MaxValue
                    : from.DistanceToSquared(blockResult.GetLocation());
                double fluidDistanceSquared = fluidResult is null
                    ? double.MaxValue
                    : from.DistanceToSquared(fluidResult.GetLocation());

                return blockDistanceSquared <= fluidDistanceSquared
                    ? blockResult
                    : fluidResult;
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

    public delegate bool BlockStepVisitor(BlockPos position, int step);

    static bool ForEachBlockIntersectedBetween(
        Vec3 from,
        Vec3 to,
        AABB aabbAtTarget,
        BlockStepVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(aabbAtTarget);
        ArgumentNullException.ThrowIfNull(visitor);

        Vec3 travel = to.Subtract(from);
        if (travel.LengthSquared() < Mth.Square(1.0E-5f))
        {
            foreach (BlockPos position in BlockPos.BetweenClosed(
                Mth.Floor(aabbAtTarget.MinX),
                Mth.Floor(aabbAtTarget.MinY),
                Mth.Floor(aabbAtTarget.MinZ),
                Mth.Floor(aabbAtTarget.MaxX),
                Mth.Floor(aabbAtTarget.MaxY),
                Mth.Floor(aabbAtTarget.MaxZ)))
            {
                if (!visitor(position, 0))
                    return false;
            }

            return true;
        }

        var visitedBlocks = new HashSet<long>();
        AABB startBounds = aabbAtTarget.Move(travel.Scale(-1.0));
        foreach (BlockPos position in BlockPos.BetweenCornersInDirection(startBounds, travel))
        {
            if (!visitor(position, 0))
                return false;

            visitedBlocks.Add(position.AsLong());
        }

        int iterations = AddCollisionsAlongTravel(visitedBlocks, travel, aabbAtTarget, visitor);
        if (iterations < 0)
            return false;

        foreach (BlockPos position in BlockPos.BetweenCornersInDirection(aabbAtTarget, travel))
        {
            if (!visitedBlocks.Add(position.AsLong()))
                continue;

            if (!visitor(position, iterations + 1))
                return false;
        }

        return true;
    }

    private static int AddCollisionsAlongTravel(
        HashSet<long> visitedBlocks,
        Vec3 travel,
        AABB aabbAtTarget,
        BlockStepVisitor visitor)
    {
        double boxSizeX = aabbAtTarget.GetXSize();
        double boxSizeY = aabbAtTarget.GetYSize();
        double boxSizeZ = aabbAtTarget.GetZSize();

        Vec3i cornerDirection = GetFurthestCorner(travel);
        Vec3 targetCenter = aabbAtTarget.GetCenter();
        Vec3 targetCorner = new(
            targetCenter.X + boxSizeX * 0.5 * cornerDirection.GetX(),
            targetCenter.Y + boxSizeY * 0.5 * cornerDirection.GetY(),
            targetCenter.Z + boxSizeZ * 0.5 * cornerDirection.GetZ());
        Vec3 startCorner = targetCorner.Subtract(travel);

        int currentBlockX = Mth.Floor(startCorner.X);
        int currentBlockY = Mth.Floor(startCorner.Y);
        int currentBlockZ = Mth.Floor(startCorner.Z);

        int signX = Mth.Sign(travel.X);
        int signY = Mth.Sign(travel.Y);
        int signZ = Mth.Sign(travel.Z);

        double tDeltaX = signX == 0 ? double.MaxValue : (double)signX / travel.X;
        double tDeltaY = signY == 0 ? double.MaxValue : (double)signY / travel.Y;
        double tDeltaZ = signZ == 0 ? double.MaxValue : (double)signZ / travel.Z;

        double tX = tDeltaX * (signX > 0 ? 1.0 - Mth.Frac(startCorner.X) : Mth.Frac(startCorner.X));
        double tY = tDeltaY * (signY > 0 ? 1.0 - Mth.Frac(startCorner.Y) : Mth.Frac(startCorner.Y));
        double tZ = tDeltaZ * (signZ > 0 ? 1.0 - Mth.Frac(startCorner.Z) : Mth.Frac(startCorner.Z));

        int iterations = 0;
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

            Vec3? hitPoint = AABB.Clip(
                currentBlockX,
                currentBlockY,
                currentBlockZ,
                currentBlockX + 1.0,
                currentBlockY + 1.0,
                currentBlockZ + 1.0,
                startCorner,
                targetCorner);
            if (hitPoint is null)
                continue;

            double cornerHitX = Mth.Clamp(
                hitPoint.X,
                currentBlockX + 1.0E-5f,
                currentBlockX + 1.0 - 1.0E-5f);
            double cornerHitY = Mth.Clamp(
                hitPoint.Y,
                currentBlockY + 1.0E-5f,
                currentBlockY + 1.0 - 1.0E-5f);
            double cornerHitZ = Mth.Clamp(
                hitPoint.Z,
                currentBlockZ + 1.0E-5f,
                currentBlockZ + 1.0 - 1.0E-5f);

            int oppositeCornerX = Mth.Floor(cornerHitX - boxSizeX * cornerDirection.GetX());
            int oppositeCornerY = Mth.Floor(cornerHitY - boxSizeY * cornerDirection.GetY());
            int oppositeCornerZ = Mth.Floor(cornerHitZ - boxSizeZ * cornerDirection.GetZ());

            int currentIteration = ++iterations;
            foreach (BlockPos position in BlockPos.BetweenCornersInDirection(
                currentBlockX,
                currentBlockY,
                currentBlockZ,
                oppositeCornerX,
                oppositeCornerY,
                oppositeCornerZ,
                travel))
            {
                if (!visitedBlocks.Add(position.AsLong()))
                    continue;

                if (!visitor(position, currentIteration))
                    return -1;
            }
        }

        return iterations;
    }

    private static Vec3i GetFurthestCorner(Vec3 direction)
    {
        double xDot = Math.Abs(Vec3.X_AXIS.Dot(direction));
        double yDot = Math.Abs(Vec3.Y_AXIS.Dot(direction));
        double zDot = Math.Abs(Vec3.Z_AXIS.Dot(direction));

        int xSign = direction.X >= 0.0 ? 1 : -1;
        int ySign = direction.Y >= 0.0 ? 1 : -1;
        int zSign = direction.Z >= 0.0 ? 1 : -1;

        if (xDot <= yDot && xDot <= zDot)
            return new Vec3i(-xSign, -zSign, ySign);

        if (yDot <= zDot)
            return new Vec3i(zSign, -ySign, -xSign);

        return new Vec3i(-ySign, xSign, -zSign);
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
