using Asteria.Core;
using Asteria.Core.Mathematics;

namespace Asteria.Core.Shapes;

/// <summary>
/// Factories and Boolean algebra for voxel shapes.
/// </summary>
public static class Shapes
{
    public const double Epsilon = 1.0E-7;
    public const double BigEpsilon = 1.0E-6;

    private static readonly VoxelShape BLOCK = CreateBlock();
    private static readonly Vec3 BLOCK_CENTER = new(0.5, 0.5, 0.5);
    private static readonly VoxelShape EMPTY = new ArrayVoxelShape(
        new BitSetDiscreteVoxelShape(0, 0, 0),
        new ArrayDoubleList(0.0),
        new ArrayDoubleList(0.0),
        new ArrayDoubleList(0.0));

    public static VoxelShape INFINITY { get; } = Box(
        double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity,
        double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);

    private static VoxelShape CreateBlock()
    {
        var shape = new BitSetDiscreteVoxelShape(1, 1, 1);
        shape.Fill(0, 0, 0);
        return new CubeVoxelShape(shape);
    }

    public static VoxelShape Empty() => EMPTY;

    public static VoxelShape Block() => BLOCK;

    public static Vec3 BlockCenter() => BLOCK_CENTER;

    public static VoxelShape Box(
        double minX, double minY, double minZ,
        double maxX, double maxY, double maxZ)
    {
        if (!(minX > maxX) && !(minY > maxY) && !(minZ > maxZ))
            return Create(minX, minY, minZ, maxX, maxY, maxZ);

        throw new ArgumentException("The min values need to be smaller or equals to the max values");
    }

    public static VoxelShape Create(
        double minX, double minY, double minZ,
        double maxX, double maxY, double maxZ)
    {
        if (maxX - minX < Epsilon || maxY - minY < Epsilon || maxZ - minZ < Epsilon)
            return Empty();

        int xBits = FindBits(minX, maxX);
        int yBits = FindBits(minY, maxY);
        int zBits = FindBits(minZ, maxZ);

        if (xBits < 0 || yBits < 0 || zBits < 0)
        {
            return new ArrayVoxelShape(
                BLOCK.Shape,
                new ArrayDoubleList(minX, maxX),
                new ArrayDoubleList(minY, maxY),
                new ArrayDoubleList(minZ, maxZ));
        }

        if (xBits == 0 && yBits == 0 && zBits == 0)
            return Block();

        int xSize = 1 << xBits;
        int ySize = 1 << yBits;
        int zSize = 1 << zBits;
        var voxelShape = BitSetDiscreteVoxelShape.WithFilledBounds(
            xSize, ySize, zSize,
            JavaRound(minX * xSize),
            JavaRound(minY * ySize),
            JavaRound(minZ * zSize),
            JavaRound(maxX * xSize),
            JavaRound(maxY * ySize),
            JavaRound(maxZ * zSize));

        return new CubeVoxelShape(voxelShape);
    }

    public static VoxelShape Create(AABB bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        return Create(
            bounds.MinX, bounds.MinY, bounds.MinZ,
            bounds.MaxX, bounds.MaxY, bounds.MaxZ);
    }

    public static int FindBits(double min, double max)
    {
        if (!(min < -Epsilon) && !(max > 1.0000001))
        {
            for (int bits = 0; bits <= 3; bits++)
            {
                int intervals = 1 << bits;
                double scaledMin = min * intervals;
                double scaledMax = max * intervals;
                bool foundMin = Math.Abs(scaledMin - JavaRound(scaledMin)) < Epsilon * intervals;
                bool foundMax = Math.Abs(scaledMax - JavaRound(scaledMax)) < Epsilon * intervals;
                if (foundMin && foundMax)
                    return bits;
            }
        }

        return -1;
    }

    public static long Lcm(int first, int second) =>
        IndexMergers.LeastCommonMultiple(first, second);

    public static VoxelShape Or(VoxelShape first, VoxelShape second) =>
        Join(first, second, BooleanOp.OR);

    public static VoxelShape Or(VoxelShape first, params VoxelShape[] tail)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(tail);
        VoxelShape result = first;
        foreach (VoxelShape shape in tail)
            result = Or(result, shape);
        return result;
    }

    public static VoxelShape Join(VoxelShape first, VoxelShape second, BooleanOp operation) =>
        JoinUnoptimized(first, second, operation).Optimize();

    public static VoxelShape JoinUnoptimized(
        VoxelShape first,
        VoxelShape second,
        BooleanOp operation)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.Apply(false, false))
            throw new ArgumentException(string.Empty);

        if (ReferenceEquals(first, second))
            return operation.Apply(true, true) ? first : Empty();

        bool firstOnlyMatters = operation.Apply(true, false);
        bool secondOnlyMatters = operation.Apply(false, true);

        if (first.IsEmpty())
            return secondOnlyMatters ? second : Empty();
        if (second.IsEmpty())
            return firstOnlyMatters ? first : Empty();

        IndexMerger xMerger = CreateIndexMerger(
            1,
            first.GetCoords(Direction.Axis.X),
            second.GetCoords(Direction.Axis.X),
            firstOnlyMatters,
            secondOnlyMatters);
        IndexMerger yMerger = CreateIndexMerger(
            xMerger.Size() - 1,
            first.GetCoords(Direction.Axis.Y),
            second.GetCoords(Direction.Axis.Y),
            firstOnlyMatters,
            secondOnlyMatters);
        IndexMerger zMerger = CreateIndexMerger(
            (xMerger.Size() - 1) * (yMerger.Size() - 1),
            first.GetCoords(Direction.Axis.Z),
            second.GetCoords(Direction.Axis.Z),
            firstOnlyMatters,
            secondOnlyMatters);

        BitSetDiscreteVoxelShape resultShape = BitSetDiscreteVoxelShape.Join(
            first.Shape, second.Shape, xMerger, yMerger, zMerger, operation);

        return xMerger is DiscreteCubeMerger &&
               yMerger is DiscreteCubeMerger &&
               zMerger is DiscreteCubeMerger
            ? new CubeVoxelShape(resultShape)
            : new ArrayVoxelShape(
                resultShape,
                xMerger.GetList(),
                yMerger.GetList(),
                zMerger.GetList());
    }

    public static bool JoinIsNotEmpty(VoxelShape first, VoxelShape second, BooleanOp operation)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.Apply(false, false))
            throw new ArgumentException(string.Empty);

        bool firstEmpty = first.IsEmpty();
        bool secondEmpty = second.IsEmpty();

        if (!firstEmpty && !secondEmpty)
        {
            if (ReferenceEquals(first, second))
                return operation.Apply(true, true);

            bool firstOnlyMatters = operation.Apply(true, false);
            bool secondOnlyMatters = operation.Apply(false, true);

            foreach (Direction.Axis axis in new[] { Direction.Axis.X, Direction.Axis.Y, Direction.Axis.Z })
            {
                if (first.Max(axis) < second.Min(axis) - Epsilon ||
                    second.Max(axis) < first.Min(axis) - Epsilon)
                {
                    return firstOnlyMatters || secondOnlyMatters;
                }
            }

            IndexMerger xMerger = CreateIndexMerger(
                1,
                first.GetCoords(Direction.Axis.X),
                second.GetCoords(Direction.Axis.X),
                firstOnlyMatters,
                secondOnlyMatters);
            IndexMerger yMerger = CreateIndexMerger(
                xMerger.Size() - 1,
                first.GetCoords(Direction.Axis.Y),
                second.GetCoords(Direction.Axis.Y),
                firstOnlyMatters,
                secondOnlyMatters);
            IndexMerger zMerger = CreateIndexMerger(
                (xMerger.Size() - 1) * (yMerger.Size() - 1),
                first.GetCoords(Direction.Axis.Z),
                second.GetCoords(Direction.Axis.Z),
                firstOnlyMatters,
                secondOnlyMatters);

            return JoinIsNotEmpty(
                xMerger, yMerger, zMerger, first.Shape, second.Shape, operation);
        }

        return operation.Apply(!firstEmpty, !secondEmpty);
    }

    public static IndexMerger CreateIndexMerger(
        int cost,
        DoubleList first,
        DoubleList second,
        bool firstOnlyMatters,
        bool secondOnlyMatters) =>
        IndexMergers.Create(cost, first, second, firstOnlyMatters, secondOnlyMatters);

    private static bool JoinIsNotEmpty(
        IndexMerger xMerger,
        IndexMerger yMerger,
        IndexMerger zMerger,
        DiscreteVoxelShape first,
        DiscreteVoxelShape second,
        BooleanOp operation) =>
        !xMerger.ForMergedIndexes((x1, x2, _) =>
            yMerger.ForMergedIndexes((y1, y2, _) =>
                zMerger.ForMergedIndexes((z1, z2, _) =>
                    !operation.Apply(
                        first.IsFullWide(x1, y1, z1),
                        second.IsFullWide(x2, y2, z2)))));

    public static double Collide(
        Direction.Axis axis,
        AABB moving,
        IEnumerable<VoxelShape> shapes,
        double distance)
    {
        ArgumentNullException.ThrowIfNull(axis);
        ArgumentNullException.ThrowIfNull(moving);
        ArgumentNullException.ThrowIfNull(shapes);

        foreach (VoxelShape shape in shapes)
        {
            ArgumentNullException.ThrowIfNull(shape);
            if (Math.Abs(distance) < Epsilon)
                return 0.0;

            distance = shape.Collide(axis, moving, distance);
        }

        return distance;
    }

    public static bool BlockOccludes(
        VoxelShape shape,
        VoxelShape occluder,
        Direction direction)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(occluder);
        ArgumentNullException.ThrowIfNull(direction);

        if (ReferenceEquals(shape, Block()) && ReferenceEquals(occluder, Block()))
            return true;

        if (occluder.IsEmpty())
            return false;

        Direction.Axis axis = direction.GetAxis();
        bool positive = ReferenceEquals(
            direction.GetAxisDirection(),
            Direction.AxisDirection.POSITIVE);
        VoxelShape first = positive ? shape : occluder;
        VoxelShape second = positive ? occluder : shape;
        BooleanOp operation = positive ? BooleanOp.ONLY_FIRST : BooleanOp.ONLY_SECOND;

        return FuzzyEquals(first.Max(axis), 1.0, Epsilon) &&
               FuzzyEquals(second.Min(axis), 0.0, Epsilon) &&
               !JoinIsNotEmpty(
                   new SliceShape(first, axis, first.Shape.GetSize(axis) - 1),
                   new SliceShape(second, axis, 0),
                   operation);
    }

    public static bool MergedFaceOccludes(
        VoxelShape shape,
        VoxelShape occluder,
        Direction direction)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(occluder);
        ArgumentNullException.ThrowIfNull(direction);

        if (ReferenceEquals(shape, Block()) || ReferenceEquals(occluder, Block()))
            return true;

        Direction.Axis axis = direction.GetAxis();
        bool positive = ReferenceEquals(
            direction.GetAxisDirection(),
            Direction.AxisDirection.POSITIVE);
        VoxelShape first = positive ? shape : occluder;
        VoxelShape second = positive ? occluder : shape;

        if (!FuzzyEquals(first.Max(axis), 1.0, Epsilon))
            first = Empty();

        if (!FuzzyEquals(second.Min(axis), 0.0, Epsilon))
            second = Empty();

        VoxelShape mergedFace = JoinUnoptimized(
            new SliceShape(first, axis, first.Shape.GetSize(axis) - 1),
            new SliceShape(second, axis, 0),
            BooleanOp.OR);

        return !JoinIsNotEmpty(Block(), mergedFace, BooleanOp.ONLY_FIRST);
    }

    public static bool FaceShapeOccludes(VoxelShape shape, VoxelShape occluder)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(occluder);

        if (ReferenceEquals(shape, Block()) || ReferenceEquals(occluder, Block()))
            return true;

        if (shape.IsEmpty() && occluder.IsEmpty())
            return false;

        return !JoinIsNotEmpty(
            Block(),
            JoinUnoptimized(shape, occluder, BooleanOp.OR),
            BooleanOp.ONLY_FIRST);
    }

    public static bool Equal(VoxelShape first, VoxelShape second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return !JoinIsNotEmpty(first, second, BooleanOp.NOT_SAME);
    }

    public static VoxelShape Rotate(VoxelShape shape, OctahedralGroup rotation) =>
        Rotate(shape, rotation, BLOCK_CENTER);

    public static VoxelShape Rotate(
        VoxelShape shape,
        OctahedralGroup rotation,
        Vec3 rotationPoint)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(rotation);
        ArgumentNullException.ThrowIfNull(rotationPoint);

        if (ReferenceEquals(rotation, OctahedralGroup.IDENTITY))
            return shape;

        DiscreteVoxelShape rotatedDiscrete = shape.Shape.Rotate(rotation);
        if (shape is CubeVoxelShape && BLOCK_CENTER.Equals(rotationPoint))
            return new CubeVoxelShape(rotatedDiscrete);

        Direction.Axis newX = rotation.Permutation.PermuteAxis(Direction.Axis.X);
        Direction.Axis newY = rotation.Permutation.PermuteAxis(Direction.Axis.Y);
        Direction.Axis newZ = rotation.Permutation.PermuteAxis(Direction.Axis.Z);

        DoubleList newXs = shape.GetCoords(newX);
        DoubleList newYs = shape.GetCoords(newY);
        DoubleList newZs = shape.GetCoords(newZ);

        return new ArrayVoxelShape(
            rotatedDiscrete,
            FlipAxisIfNeeded(newXs, rotation.Inverts(Direction.Axis.X), rotationPoint.Get(newX), rotationPoint.X),
            FlipAxisIfNeeded(newYs, rotation.Inverts(Direction.Axis.Y), rotationPoint.Get(newY), rotationPoint.Y),
            FlipAxisIfNeeded(newZs, rotation.Inverts(Direction.Axis.Z), rotationPoint.Get(newZ), rotationPoint.Z));
    }

    public static DoubleList FlipAxisIfNeeded(
        DoubleList axis,
        bool flip,
        double newRelative,
        double oldRelative)
    {
        ArgumentNullException.ThrowIfNull(axis);
        if (!flip && newRelative == oldRelative)
            return axis;

        var values = new double[axis.Count];
        if (flip)
        {
            int target = 0;
            for (int index = axis.Count - 1; index >= 0; index--)
                values[target++] = -(axis.GetDouble(index) - newRelative) + oldRelative;
        }
        else
        {
            for (int index = 0; index < axis.Count; index++)
                values[index] = axis.GetDouble(index) - newRelative + oldRelative;
        }

        return new ArrayDoubleList(values);
    }

    public static IReadOnlyDictionary<Direction.Axis, VoxelShape> RotateHorizontalAxis(VoxelShape zAxis) =>
        RotateHorizontalAxis(zAxis, BLOCK_CENTER);

    public static IReadOnlyDictionary<Direction.Axis, VoxelShape> RotateHorizontalAxis(
        VoxelShape zAxis,
        Vec3 rotationCenter)
    {
        ArgumentNullException.ThrowIfNull(zAxis);
        ArgumentNullException.ThrowIfNull(rotationCenter);
        return new Dictionary<Direction.Axis, VoxelShape>
        {
            [Direction.Axis.Z] = zAxis,
            [Direction.Axis.X] = Rotate(zAxis, OctahedralGroup.BLOCK_ROT_Y_90, rotationCenter)
        };
    }

    public static IReadOnlyDictionary<Direction.Axis, VoxelShape> RotateAllAxis(VoxelShape north) =>
        RotateAllAxis(north, BLOCK_CENTER);

    public static IReadOnlyDictionary<Direction.Axis, VoxelShape> RotateAllAxis(
        VoxelShape north,
        Vec3 rotationCenter)
    {
        ArgumentNullException.ThrowIfNull(north);
        ArgumentNullException.ThrowIfNull(rotationCenter);
        return new Dictionary<Direction.Axis, VoxelShape>
        {
            [Direction.Axis.Z] = north,
            [Direction.Axis.X] = Rotate(north, OctahedralGroup.BLOCK_ROT_Y_90, rotationCenter),
            [Direction.Axis.Y] = Rotate(north, OctahedralGroup.BLOCK_ROT_X_90, rotationCenter)
        };
    }

    public static IReadOnlyDictionary<Direction, VoxelShape> RotateHorizontal(VoxelShape north) =>
        RotateHorizontal(north, OctahedralGroup.IDENTITY, BLOCK_CENTER);

    public static IReadOnlyDictionary<Direction, VoxelShape> RotateHorizontal(
        VoxelShape north,
        OctahedralGroup initial) =>
        RotateHorizontal(north, initial, BLOCK_CENTER);

    public static IReadOnlyDictionary<Direction, VoxelShape> RotateHorizontal(
        VoxelShape north,
        OctahedralGroup initial,
        Vec3 rotationCenter)
    {
        ArgumentNullException.ThrowIfNull(north);
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(rotationCenter);
        return new Dictionary<Direction, VoxelShape>
        {
            [Direction.NORTH] = Rotate(north, initial),
            [Direction.EAST] = Rotate(north, OctahedralGroup.BLOCK_ROT_Y_90.Compose(initial), rotationCenter),
            [Direction.SOUTH] = Rotate(north, OctahedralGroup.BLOCK_ROT_Y_180.Compose(initial), rotationCenter),
            [Direction.WEST] = Rotate(north, OctahedralGroup.BLOCK_ROT_Y_270.Compose(initial), rotationCenter)
        };
    }

    public static IReadOnlyDictionary<Direction, VoxelShape> RotateAll(VoxelShape north) =>
        RotateAll(north, OctahedralGroup.IDENTITY, BLOCK_CENTER);

    public static IReadOnlyDictionary<Direction, VoxelShape> RotateAll(
        VoxelShape north,
        Vec3 rotationCenter) =>
        RotateAll(north, OctahedralGroup.IDENTITY, rotationCenter);

    public static IReadOnlyDictionary<Direction, VoxelShape> RotateAll(
        VoxelShape north,
        OctahedralGroup initial,
        Vec3 rotationCenter)
    {
        ArgumentNullException.ThrowIfNull(north);
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(rotationCenter);
        return new Dictionary<Direction, VoxelShape>
        {
            [Direction.NORTH] = Rotate(north, initial),
            [Direction.EAST] = Rotate(north, OctahedralGroup.BLOCK_ROT_Y_90.Compose(initial), rotationCenter),
            [Direction.SOUTH] = Rotate(north, OctahedralGroup.BLOCK_ROT_Y_180.Compose(initial), rotationCenter),
            [Direction.WEST] = Rotate(north, OctahedralGroup.BLOCK_ROT_Y_270.Compose(initial), rotationCenter),
            [Direction.UP] = Rotate(north, OctahedralGroup.BLOCK_ROT_X_270.Compose(initial), rotationCenter),
            [Direction.DOWN] = Rotate(north, OctahedralGroup.BLOCK_ROT_X_90.Compose(initial), rotationCenter)
        };
    }

    private static bool FuzzyEquals(double first, double second, double tolerance) =>
        Math.Abs(first - second) <= tolerance;

    private static int JavaRound(double value)
    {
        if (double.IsNaN(value)) return 0;
        if (value >= int.MaxValue) return int.MaxValue;
        if (value <= int.MinValue) return int.MinValue;
        return (int)Math.Floor(value + 0.5);
    }
}
