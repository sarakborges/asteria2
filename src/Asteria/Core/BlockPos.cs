using Asteria.Content.Blocks;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.LevelGen;

namespace Asteria.Core;

/// <summary>
/// Integer block coordinates. Packed layout follows the reference 26/12/26-bit representation.
/// </summary>
public class BlockPos : Vec3i
{
    /// <summary>Exactly three signed integer coordinates in X/Y/Z order.</summary>
    public static Codec<BlockPos> CODEC { get; } =
        Codecs.INT.ListOf().FlatXMap(
            static values => values.Count == 3
                ? DataResult<BlockPos>.Success(new BlockPos(values[0], values[1], values[2]))
                : DataResult<BlockPos>.Error(() => "Input is not a list of 3 elements"),
            static position => DataResult<IReadOnlyList<int>>.Success(
                new[] { position.GetX(), position.GetY(), position.GetZ() })).WithLifecycle(Lifecycle.Stable());

    public const int PackedHorizontalLength = 26;
    public const int PackedYLength = 12;
    public const int MaxHorizontalCoordinate = (1 << PackedHorizontalLength) / 2 - 1;

    private const int YOffset = 0;
    private const int ZOffset = PackedYLength;
    private const int XOffset = PackedYLength + PackedHorizontalLength;

    private const long PackedXMask = (1L << PackedHorizontalLength) - 1L;
    private const long PackedYMask = (1L << PackedYLength) - 1L;
    private const long PackedZMask = (1L << PackedHorizontalLength) - 1L;

    public new static readonly BlockPos Zero = new(0, 0, 0);

    public BlockPos(int x, int y, int z)
        : base(x, y, z)
    {
    }

    public static int GetX(long blockNode) =>
        unchecked((int)(blockNode >> XOffset));

    public static int GetY(long blockNode) =>
        unchecked((int)(blockNode << (64 - PackedYLength) >> (64 - PackedYLength)));

    public static int GetZ(long blockNode) =>
        unchecked((int)(blockNode << (64 - ZOffset - PackedHorizontalLength) >> (64 - PackedHorizontalLength)));

    public static BlockPos Of(long blockNode) =>
        new(GetX(blockNode), GetY(blockNode), GetZ(blockNode));

    public static BlockPos Containing(double x, double y, double z) =>
        new(FloorToInt(x), FloorToInt(y), FloorToInt(z));

    public static BlockPos Min(BlockPos a, BlockPos b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return new BlockPos(
            Math.Min(a.GetX(), b.GetX()),
            Math.Min(a.GetY(), b.GetY()),
            Math.Min(a.GetZ(), b.GetZ()));
    }

    public static BlockPos Max(BlockPos a, BlockPos b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return new BlockPos(
            Math.Max(a.GetX(), b.GetX()),
            Math.Max(a.GetY(), b.GetY()),
            Math.Max(a.GetZ(), b.GetZ()));
    }

    public long AsLong() =>
        AsLong(GetX(), GetY(), GetZ());

    public static long AsLong(int x, int y, int z)
    {
        long node = 0L;
        node |= ((long)x & PackedXMask) << XOffset;
        node |= ((long)y & PackedYMask) << YOffset;
        return node | ((long)z & PackedZMask) << ZOffset;
    }

    public static long Offset(long blockNode, int stepX, int stepY, int stepZ) =>
        AsLong(
            unchecked(GetX(blockNode) + stepX),
            unchecked(GetY(blockNode) + stepY),
            unchecked(GetZ(blockNode) + stepZ));

    public static long GetFlatIndex(long neighborBlockNode) =>
        neighborBlockNode & -16L;

    public override BlockPos Offset(int x, int y, int z) =>
        x == 0 && y == 0 && z == 0
            ? this
            : new BlockPos(
                unchecked(GetX() + x),
                unchecked(GetY() + y),
                unchecked(GetZ() + z));

    public override BlockPos Offset(Vec3i vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return Offset(vector.GetX(), vector.GetY(), vector.GetZ());
    }

    public override BlockPos Subtract(Vec3i vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return Offset(
            unchecked(-vector.GetX()),
            unchecked(-vector.GetY()),
            unchecked(-vector.GetZ()));
    }

    public override BlockPos Multiply(int scale) =>
        scale == 1
            ? this
            : scale == 0
                ? Zero
                : new BlockPos(
                    unchecked(GetX() * scale),
                    unchecked(GetY() * scale),
                    unchecked(GetZ() * scale));

    public override BlockPos Above() => Above(1);

    public override BlockPos Above(int steps) => Offset(0, steps, 0);

    public override BlockPos Below() => Below(1);

    public override BlockPos Below(int steps) => Offset(0, unchecked(-steps), 0);

    public override BlockPos North() => North(1);

    public override BlockPos North(int steps) => Offset(0, 0, unchecked(-steps));

    public override BlockPos South() => South(1);

    public override BlockPos South(int steps) => Offset(0, 0, steps);

    public override BlockPos West() => West(1);

    public override BlockPos West(int steps) => Offset(unchecked(-steps), 0, 0);

    public override BlockPos East() => East(1);

    public override BlockPos East(int steps) => Offset(steps, 0, 0);

    public BlockPos Relative(Direction direction) => Relative(direction, 1);

    public BlockPos Relative(Direction direction, int steps)
    {
        ArgumentNullException.ThrowIfNull(direction);
        return Offset(
            unchecked(direction.GetStepX() * steps),
            unchecked(direction.GetStepY() * steps),
            unchecked(direction.GetStepZ() * steps));
    }

    public override BlockPos Relative(Axis axis, int steps) =>
        steps == 0
            ? this
            : axis switch
            {
                Axis.X => Offset(steps, 0, 0),
                Axis.Y => Offset(0, steps, 0),
                Axis.Z => Offset(0, 0, steps),
                _ => throw new ArgumentOutOfRangeException(nameof(axis))
            };

    public override BlockPos Cross(Vec3i vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        return new BlockPos(
            unchecked(GetY() * vector.GetZ() - GetZ() * vector.GetY()),
            unchecked(GetZ() * vector.GetX() - GetX() * vector.GetZ()),
            unchecked(GetX() * vector.GetY() - GetY() * vector.GetX()));
    }

    public BlockPos AtY(int y) =>
        new(GetX(), y, GetZ());

    public virtual BlockPos Rotate(Rotation rotation)
    {
        ArgumentNullException.ThrowIfNull(rotation);

        return rotation switch
        {
            var value when ReferenceEquals(value, Rotation.CLOCKWISE_90) =>
                new BlockPos(unchecked(-GetZ()), GetY(), GetX()),
            var value when ReferenceEquals(value, Rotation.CLOCKWISE_180) =>
                new BlockPos(unchecked(-GetX()), GetY(), unchecked(-GetZ())),
            var value when ReferenceEquals(value, Rotation.COUNTERCLOCKWISE_90) =>
                new BlockPos(GetZ(), GetY(), unchecked(-GetX())),
            var value when ReferenceEquals(value, Rotation.NONE) => this,
            _ => throw new ArgumentOutOfRangeException(nameof(rotation))
        };
    }

    public Vec3 ClampLocationWithin(Vec3 location)
    {
        ArgumentNullException.ThrowIfNull(location);

        return new Vec3(
            Mth.Clamp(
                location.X,
                (double)((float)GetX() + 1.0E-5f),
                (double)GetX() + 1.0 - (double)1.0E-5f),
            Mth.Clamp(
                location.Y,
                (double)((float)GetY() + 1.0E-5f),
                (double)GetY() + 1.0 - (double)1.0E-5f),
            Mth.Clamp(
                location.Z,
                (double)((float)GetZ() + 1.0E-5f),
                (double)GetZ() + 1.0 - (double)1.0E-5f));
    }

    public virtual BlockPos Immutable() => this;

    public MutableBlockPos Mutable() =>
        new(GetX(), GetY(), GetZ());

    /// <summary>
    /// Enumerates an inclusive box in X/Y/Z traversal order.
    /// The same mutable cursor is reused for each yielded position, matching the source contract.
    /// </summary>
    public static IEnumerable<BlockPos> BetweenClosed(
        int minX,
        int minY,
        int minZ,
        int maxX,
        int maxY,
        int maxZ)
    {
        if (minX > maxX || minY > maxY || minZ > maxZ)
            yield break;

        var cursor = new MutableBlockPos();
        for (int z = minZ; ; z = unchecked(z + 1))
        {
            for (int y = minY; ; y = unchecked(y + 1))
            {
                for (int x = minX; ; x = unchecked(x + 1))
                {
                    yield return cursor.Set(x, y, z);
                    if (x == maxX)
                        break;
                }

                if (y == maxY)
                    break;
            }

            if (z == maxZ)
                break;
        }
    }



    public static IEnumerable<BlockPos> RandomInCube(
        IRandomSource random,
        int limit,
        BlockPos center,
        int sizeToScanInAllDirections)
    {
        ArgumentNullException.ThrowIfNull(center);
        return RandomBetweenClosed(
            random,
            limit,
            unchecked(center.GetX() - sizeToScanInAllDirections),
            unchecked(center.GetY() - sizeToScanInAllDirections),
            unchecked(center.GetZ() - sizeToScanInAllDirections),
            unchecked(center.GetX() + sizeToScanInAllDirections),
            unchecked(center.GetY() + sizeToScanInAllDirections),
            unchecked(center.GetZ() + sizeToScanInAllDirections));
    }

    public static IEnumerable<BlockPos> RandomBetweenClosed(
        IRandomSource random,
        int limit,
        int minX,
        int minY,
        int minZ,
        int maxX,
        int maxY,
        int maxZ)
    {
        ArgumentNullException.ThrowIfNull(random);

        int width = unchecked(maxX - minX + 1);
        int height = unchecked(maxY - minY + 1);
        int depth = unchecked(maxZ - minZ + 1);

        return Enumerate();

        IEnumerable<BlockPos> Enumerate()
        {
            var cursor = new MutableBlockPos();
            int counter = limit;
            while (counter > 0)
            {
                int x = unchecked(minX + random.NextInt(width));
                int y = unchecked(minY + random.NextInt(height));
                int z = unchecked(minZ + random.NextInt(depth));
                counter--;
                yield return cursor.Set(x, y, z);
            }
        }
    }

    public static IEnumerable<BlockPos> WithinManhattan(BlockPos origin, int reach)
    {
        ArgumentNullException.ThrowIfNull(origin);
        return ManhattanOrdered(origin, reach, reach, reach, reach);
    }

    public static IEnumerable<BlockPos> WithinClippedManhattan(
        BlockPos origin,
        int reachX,
        int reachY,
        int reachZ)
    {
        ArgumentNullException.ThrowIfNull(origin);
        return ManhattanOrdered(
            origin,
            reachX,
            reachY,
            reachZ,
            Math.Max(reachX, Math.Max(reachY, reachZ)));
    }

    public static IEnumerable<BlockPos> WithinBoxByManhattanDistance(
        BlockPos origin,
        int reachX,
        int reachY,
        int reachZ)
    {
        ArgumentNullException.ThrowIfNull(origin);
        return ManhattanOrdered(
            origin,
            reachX,
            reachY,
            reachZ,
            unchecked(reachX + reachY + reachZ));
    }

    private static IEnumerable<BlockPos> ManhattanOrdered(
        BlockPos origin,
        int reachX,
        int reachY,
        int reachZ,
        int maxDepth)
    {
        int originX = origin.GetX();
        int originY = origin.GetY();
        int originZ = origin.GetZ();
        var cursor = new MutableBlockPos();
        int currentDepth = 0;
        int maxX = 0;
        int maxY = 0;
        int x = 0;
        int y = 0;
        bool zMirror = false;

        while (true)
        {
            if (zMirror)
            {
                zMirror = false;
                cursor.SetZ(unchecked(originZ - (cursor.GetZ() - originZ)));
                yield return cursor;
                continue;
            }

            bool found = false;
            while (!found)
            {
                if (y > maxY)
                {
                    x++;
                    if (x > maxX)
                    {
                        currentDepth++;
                        if (currentDepth > maxDepth)
                            yield break;

                        maxX = Math.Min(reachX, currentDepth);
                        x = unchecked(-maxX);
                    }

                    maxY = Math.Min(reachY, unchecked(currentDepth - AbsLikeReference(x)));
                    y = unchecked(-maxY);
                }

                int xx = x;
                int yy = y;
                int zz = unchecked(currentDepth - AbsLikeReference(xx) - AbsLikeReference(yy));
                if (zz <= reachZ)
                {
                    zMirror = zz != 0;
                    cursor.Set(
                        unchecked(originX + xx),
                        unchecked(originY + yy),
                        unchecked(originZ + zz));
                    found = true;
                }

                y++;
            }

            yield return cursor;
        }
    }

    private static int AbsLikeReference(int value) =>
        value == int.MinValue ? int.MinValue : Math.Abs(value);

    public static IEnumerable<BlockPos> BetweenClosed(AABB bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        return BetweenClosed(
            Containing(bounds.MinX, bounds.MinY, bounds.MinZ),
            Containing(bounds.MaxX, bounds.MaxY, bounds.MaxZ));
    }

    public static IEnumerable<BlockPos> NeighborColumn(
        int startX,
        int startY,
        int startZ,
        int endY)
    {
        int yDirection = endY > startY ? 1 : -1;
        int height = unchecked(AbsLikeReference(unchecked(endY - startY)) + 1);
        (int X, int Z)[] steps =
        {
            (0, 0),
            (0, -1),
            (1, 0),
            (0, 1),
            (-1, 0)
        };

        return Enumerate();

        IEnumerable<BlockPos> Enumerate()
        {
            var cursor = new MutableBlockPos();
            for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
            {
                (int offsetX, int offsetZ) = steps[stepIndex];
                for (int y = 0; y < height; y++)
                {
                    yield return cursor.Set(
                        unchecked(startX + offsetX),
                        unchecked(startY + y * yDirection),
                        unchecked(startZ + offsetZ));
                }
            }
        }
    }

    public static IEnumerable<MutableBlockPos> SpiralAround(
        BlockPos center,
        int radius,
        Direction firstDirection,
        Direction secondDirection)
    {
        ArgumentNullException.ThrowIfNull(center);
        ArgumentNullException.ThrowIfNull(firstDirection);
        ArgumentNullException.ThrowIfNull(secondDirection);

        if (ReferenceEquals(firstDirection.GetAxis(), secondDirection.GetAxis()))
            throw new InvalidOperationException("The two directions cannot be on the same axis");

        return Enumerate();

        IEnumerable<MutableBlockPos> Enumerate()
        {
            Direction[] directions =
            {
                firstDirection,
                secondDirection,
                firstDirection.GetOpposite(),
                secondDirection.GetOpposite()
            };

            MutableBlockPos cursor = center.Mutable().Move(
                secondDirection.GetStepX(),
                secondDirection.GetStepY(),
                secondDirection.GetStepZ());
            int legs = unchecked(4 * radius);
            int leg = -1;
            int legSize = 0;
            int legIndex = 0;
            int lastX = cursor.GetX();
            int lastY = cursor.GetY();
            int lastZ = cursor.GetZ();

            while (true)
            {
                Direction direction = directions[unchecked(leg + 4) % 4];
                cursor.Set(lastX, lastY, lastZ).Move(
                    direction.GetStepX(),
                    direction.GetStepY(),
                    direction.GetStepZ());
                lastX = cursor.GetX();
                lastY = cursor.GetY();
                lastZ = cursor.GetZ();

                if (legIndex >= legSize)
                {
                    if (leg >= legs)
                        yield break;

                    leg++;
                    legIndex = 0;
                    legSize = leg / 2 + 1;
                }

                legIndex++;
                yield return cursor;
            }
        }
    }

    public static int BreadthFirstTraversal(
        BlockPos startPosition,
        int maxDepth,
        int maxCount,
        Action<BlockPos, Action<BlockPos>> neighborProvider,
        Func<BlockPos, TraversalNodeStatus> nodeProcessor)
    {
        ArgumentNullException.ThrowIfNull(startPosition);
        ArgumentNullException.ThrowIfNull(neighborProvider);
        ArgumentNullException.ThrowIfNull(nodeProcessor);

        var nodes = new Queue<(BlockPos Position, int Depth)>();
        var visited = new HashSet<long>();
        nodes.Enqueue((startPosition, 0));
        int count = 0;

        while (nodes.Count > 0)
        {
            (BlockPos currentPosition, int depth) = nodes.Dequeue();
            if (!visited.Add(currentPosition.AsLong()))
                continue;

            TraversalNodeStatus status = nodeProcessor(currentPosition);
            if (status == TraversalNodeStatus.SKIP)
                continue;
            if (status == TraversalNodeStatus.STOP)
                break;

            count = unchecked(count + 1);
            if (count >= maxCount)
                return count;
            if (depth >= maxDepth)
                continue;

            neighborProvider(
                currentPosition,
                position => nodes.Enqueue((position, unchecked(depth + 1))));
        }

        return count;
    }

    public enum TraversalNodeStatus
    {
        ACCEPT,
        SKIP,
        STOP
    }

    public static IEnumerable<BlockPos> BetweenClosed(BlockPos first, BlockPos second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return BetweenClosed(
            Math.Min(first.GetX(), second.GetX()),
            Math.Min(first.GetY(), second.GetY()),
            Math.Min(first.GetZ(), second.GetZ()),
            Math.Max(first.GetX(), second.GetX()),
            Math.Max(first.GetY(), second.GetY()),
            Math.Max(first.GetZ(), second.GetZ()));
    }

    /// <summary>
    /// Enumerates the integer corners of a box in movement order. The innermost
    /// traversal axis follows the 26.3 axis-step ordering.
    /// </summary>
    public static IEnumerable<BlockPos> BetweenCornersInDirection(AABB bounds, Vec3 direction)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(direction);

        return BetweenCornersInDirection(
            Mth.Floor(bounds.MinX),
            Mth.Floor(bounds.MinY),
            Mth.Floor(bounds.MinZ),
            Mth.Floor(bounds.MaxX),
            Mth.Floor(bounds.MaxY),
            Mth.Floor(bounds.MaxZ),
            direction);
    }

    public static IEnumerable<BlockPos> BetweenCornersInDirection(
        int firstCornerX,
        int firstCornerY,
        int firstCornerZ,
        int secondCornerX,
        int secondCornerY,
        int secondCornerZ,
        Vec3 direction)
    {
        ArgumentNullException.ThrowIfNull(direction);

        int minCornerX = Math.Min(firstCornerX, secondCornerX);
        int minCornerY = Math.Min(firstCornerY, secondCornerY);
        int minCornerZ = Math.Min(firstCornerZ, secondCornerZ);
        int maxCornerX = Math.Max(firstCornerX, secondCornerX);
        int maxCornerY = Math.Max(firstCornerY, secondCornerY);
        int maxCornerZ = Math.Max(firstCornerZ, secondCornerZ);

        int diffX = maxCornerX - minCornerX;
        int diffY = maxCornerY - minCornerY;
        int diffZ = maxCornerZ - minCornerZ;

        int startCornerX = direction.X >= 0.0 ? minCornerX : maxCornerX;
        int startCornerY = direction.Y >= 0.0 ? minCornerY : maxCornerY;
        int startCornerZ = direction.Z >= 0.0 ? minCornerZ : maxCornerZ;

        Direction.Axis[] axes = Math.Abs(direction.X) < Math.Abs(direction.Z)
            ? new[] { Direction.Axis.Y, Direction.Axis.Z, Direction.Axis.X }
            : new[] { Direction.Axis.Y, Direction.Axis.X, Direction.Axis.Z };

        Direction.Axis firstVisitAxis = axes[0];
        Direction.Axis secondVisitAxis = axes[1];
        Direction.Axis thirdVisitAxis = axes[2];

        Direction firstVisitDirection = direction.Get(firstVisitAxis) >= 0.0
            ? firstVisitAxis.GetPositive()
            : firstVisitAxis.GetNegative();
        Direction secondVisitDirection = direction.Get(secondVisitAxis) >= 0.0
            ? secondVisitAxis.GetPositive()
            : secondVisitAxis.GetNegative();
        Direction thirdVisitDirection = direction.Get(thirdVisitAxis) >= 0.0
            ? thirdVisitAxis.GetPositive()
            : thirdVisitAxis.GetNegative();

        int firstMax = firstVisitAxis.Choose(diffX, diffY, diffZ);
        int secondMax = secondVisitAxis.Choose(diffX, diffY, diffZ);
        int thirdMax = thirdVisitAxis.Choose(diffX, diffY, diffZ);

        int firstDirX = firstVisitDirection.GetStepX();
        int firstDirY = firstVisitDirection.GetStepY();
        int firstDirZ = firstVisitDirection.GetStepZ();
        int secondDirX = secondVisitDirection.GetStepX();
        int secondDirY = secondVisitDirection.GetStepY();
        int secondDirZ = secondVisitDirection.GetStepZ();
        int thirdDirX = thirdVisitDirection.GetStepX();
        int thirdDirY = thirdVisitDirection.GetStepY();
        int thirdDirZ = thirdVisitDirection.GetStepZ();

        var cursor = new MutableBlockPos();
        for (int firstIndex = 0; firstIndex <= firstMax; firstIndex++)
        {
            for (int secondIndex = 0; secondIndex <= secondMax; secondIndex++)
            {
                for (int thirdIndex = 0; thirdIndex <= thirdMax; thirdIndex++)
                {
                    yield return cursor.Set(
                        startCornerX + firstDirX * firstIndex +
                            secondDirX * secondIndex + thirdDirX * thirdIndex,
                        startCornerY + firstDirY * firstIndex +
                            secondDirY * secondIndex + thirdDirY * thirdIndex,
                        startCornerZ + firstDirZ * firstIndex +
                            secondDirZ * secondIndex + thirdDirZ * thirdIndex);
                }
            }
        }
    }

    private static int FloorToInt(double value)
    {
        if (double.IsNaN(value))
            return 0;

        double floor = Math.Floor(value);
        if (floor <= int.MinValue)
            return int.MinValue;
        if (floor >= int.MaxValue)
            return int.MaxValue;
        return (int)floor;
    }

    public sealed class MutableBlockPos : BlockPos
    {
        public MutableBlockPos()
            : this(0, 0, 0)
        {
        }

        public MutableBlockPos(int x, int y, int z)
            : base(x, y, z)
        {
        }

        public MutableBlockPos(double x, double y, double z)
            : this(FloorToInt(x), FloorToInt(y), FloorToInt(z))
        {
        }

        public override BlockPos Offset(int x, int y, int z) =>
            base.Offset(x, y, z).Immutable();

        public override BlockPos Offset(Vec3i vector)
        {
            ArgumentNullException.ThrowIfNull(vector);
            return Offset(vector.GetX(), vector.GetY(), vector.GetZ());
        }

        public override BlockPos Subtract(Vec3i vector) =>
            base.Subtract(vector).Immutable();

        public override BlockPos Multiply(int scale) =>
            base.Multiply(scale).Immutable();

        public override BlockPos Relative(Axis axis, int steps) =>
            base.Relative(axis, steps).Immutable();

        public override BlockPos Rotate(Rotation rotation) =>
            base.Rotate(rotation).Immutable();

        public MutableBlockPos Set(AxisCycle transform, int x, int y, int z)
        {
            ArgumentNullException.ThrowIfNull(transform);
            return Set(
                transform.Cycle(x, y, z, Direction.Axis.X),
                transform.Cycle(x, y, z, Direction.Axis.Y),
                transform.Cycle(x, y, z, Direction.Axis.Z));
        }

        public MutableBlockPos Set(int x, int y, int z)
        {
            SetX(x);
            SetY(y);
            SetZ(z);
            return this;
        }

        public MutableBlockPos Set(double x, double y, double z) =>
            Set(FloorToInt(x), FloorToInt(y), FloorToInt(z));

        public MutableBlockPos Set(Vec3i vector)
        {
            ArgumentNullException.ThrowIfNull(vector);
            return Set(vector.GetX(), vector.GetY(), vector.GetZ());
        }

        public MutableBlockPos Set(long packedPosition) =>
            Set(GetX(packedPosition), GetY(packedPosition), GetZ(packedPosition));

        public MutableBlockPos SetWithOffset(Vec3i position, Direction direction)
        {
            ArgumentNullException.ThrowIfNull(position);
            ArgumentNullException.ThrowIfNull(direction);
            return Set(
                unchecked(position.GetX() + direction.GetStepX()),
                unchecked(position.GetY() + direction.GetStepY()),
                unchecked(position.GetZ() + direction.GetStepZ()));
        }

        public MutableBlockPos SetWithOffset(Vec3i position, int x, int y, int z)
        {
            ArgumentNullException.ThrowIfNull(position);
            return Set(
                unchecked(position.GetX() + x),
                unchecked(position.GetY() + y),
                unchecked(position.GetZ() + z));
        }

        public MutableBlockPos SetWithOffset(Vec3i position, Vec3i offset)
        {
            ArgumentNullException.ThrowIfNull(offset);
            return SetWithOffset(position, offset.GetX(), offset.GetY(), offset.GetZ());
        }

        public MutableBlockPos Move(Direction direction) => Move(direction, 1);

        public MutableBlockPos Move(Direction direction, int steps)
        {
            ArgumentNullException.ThrowIfNull(direction);
            return Move(
                unchecked(direction.GetStepX() * steps),
                unchecked(direction.GetStepY() * steps),
                unchecked(direction.GetStepZ() * steps));
        }

        public MutableBlockPos Move(int x, int y, int z) =>
            Set(
                unchecked(GetX() + x),
                unchecked(GetY() + y),
                unchecked(GetZ() + z));

        public MutableBlockPos Move(Vec3i offset)
        {
            ArgumentNullException.ThrowIfNull(offset);
            return Move(offset.GetX(), offset.GetY(), offset.GetZ());
        }

        public MutableBlockPos Move(Axis axis, int steps) =>
            axis switch
            {
                Axis.X => Move(steps, 0, 0),
                Axis.Y => Move(0, steps, 0),
                Axis.Z => Move(0, 0, steps),
                _ => throw new ArgumentOutOfRangeException(nameof(axis))
            };

        public MutableBlockPos Clamp(Axis axis, int minimum, int maximum)
        {
            static int ClampLikeReference(int value, int min, int max) =>
                value < min ? min : value > max ? max : value;

            return axis switch
            {
                Axis.X => Set(ClampLikeReference(GetX(), minimum, maximum), GetY(), GetZ()),
                Axis.Y => Set(GetX(), ClampLikeReference(GetY(), minimum, maximum), GetZ()),
                Axis.Z => Set(GetX(), GetY(), ClampLikeReference(GetZ(), minimum, maximum)),
                _ => throw new ArgumentOutOfRangeException(nameof(axis))
            };
        }

        public new MutableBlockPos SetX(int x)
        {
            base.SetX(x);
            return this;
        }

        public new MutableBlockPos SetY(int y)
        {
            base.SetY(y);
            return this;
        }

        public new MutableBlockPos SetZ(int z)
        {
            base.SetZ(z);
            return this;
        }

        public override BlockPos Immutable() =>
            new(GetX(), GetY(), GetZ());
    }
}
