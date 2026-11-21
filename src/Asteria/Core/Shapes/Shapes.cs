using Asteria.Core;

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

    private static int JavaRound(double value)
    {
        if (double.IsNaN(value)) return 0;
        if (value >= int.MaxValue) return int.MaxValue;
        if (value <= int.MinValue) return int.MinValue;
        return (int)Math.Floor(value + 0.5);
    }
}
