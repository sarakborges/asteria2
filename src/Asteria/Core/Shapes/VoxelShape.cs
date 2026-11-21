using Asteria.Core;

namespace Asteria.Core.Shapes;

/// <summary>
/// Continuous geometry layered over a discrete voxel occupancy grid.
/// </summary>
public abstract class VoxelShape
{
    public DiscreteVoxelShape Shape { get; }

    protected VoxelShape(DiscreteVoxelShape shape) =>
        Shape = shape ?? throw new ArgumentNullException(nameof(shape));

    public double Min(Direction.Axis axis)
    {
        int index = Shape.FirstFull(axis);
        return index >= Shape.GetSize(axis)
            ? double.PositiveInfinity
            : Get(axis, index);
    }

    public double Max(Direction.Axis axis)
    {
        int index = Shape.LastFull(axis);
        return index <= 0
            ? double.NegativeInfinity
            : Get(axis, index);
    }

    public AABB Bounds()
    {
        if (IsEmpty())
            throw new NotSupportedException("No bounds for empty shape.");

        return new AABB(
            Min(Direction.Axis.X),
            Min(Direction.Axis.Y),
            Min(Direction.Axis.Z),
            Max(Direction.Axis.X),
            Max(Direction.Axis.Y),
            Max(Direction.Axis.Z));
    }

    public VoxelShape SingleEncompassing() =>
        IsEmpty()
            ? Shapes.Empty()
            : Shapes.Box(
                Min(Direction.Axis.X),
                Min(Direction.Axis.Y),
                Min(Direction.Axis.Z),
                Max(Direction.Axis.X),
                Max(Direction.Axis.Y),
                Max(Direction.Axis.Z));

    protected double Get(Direction.Axis axis, int index) =>
        GetCoords(axis).GetDouble(index);

    public abstract DoubleList GetCoords(Direction.Axis axis);

    public bool IsEmpty() => Shape.IsEmpty();

    public VoxelShape Move(Vec3 delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        return Move(delta.X, delta.Y, delta.Z);
    }

    public VoxelShape Move(Vec3i delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        return Move(delta.GetX(), delta.GetY(), delta.GetZ());
    }

    public VoxelShape Move(double dx, double dy, double dz) =>
        IsEmpty()
            ? Shapes.Empty()
            : new ArrayVoxelShape(
                Shape,
                new OffsetDoubleList(GetCoords(Direction.Axis.X), dx),
                new OffsetDoubleList(GetCoords(Direction.Axis.Y), dy),
                new OffsetDoubleList(GetCoords(Direction.Axis.Z), dz));

    public VoxelShape Optimize()
    {
        VoxelShape result = Shapes.Empty();
        ForAllBoxes((x1, y1, z1, x2, y2, z2) =>
            result = Shapes.JoinUnoptimized(
                result,
                Shapes.Box(x1, y1, z1, x2, y2, z2),
                BooleanOp.OR));
        return result;
    }

    public void ForAllBoxes(Action<double, double, double, double, double, double> consumer)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        DoubleList xCoords = GetCoords(Direction.Axis.X);
        DoubleList yCoords = GetCoords(Direction.Axis.Y);
        DoubleList zCoords = GetCoords(Direction.Axis.Z);

        BitSetDiscreteVoxelShape.ForAllBoxes(
            Shape,
            (x1, y1, z1, x2, y2, z2) => consumer(
                xCoords.GetDouble(x1),
                yCoords.GetDouble(y1),
                zCoords.GetDouble(z1),
                xCoords.GetDouble(x2),
                yCoords.GetDouble(y2),
                zCoords.GetDouble(z2)),
            mergeNeighbors: true);
    }

    public IReadOnlyList<AABB> ToAabbs()
    {
        var boxes = new List<AABB>();
        ForAllBoxes((x1, y1, z1, x2, y2, z2) =>
            boxes.Add(new AABB(x1, y1, z1, x2, y2, z2)));
        return boxes;
    }

    public double Min(Direction.Axis axis, double b, double c)
    {
        Direction.Axis bAxis = AxisCycle.Forward.Cycle(axis);
        Direction.Axis cAxis = AxisCycle.Backward.Cycle(axis);
        int bi = FindIndex(bAxis, b);
        int ci = FindIndex(cAxis, c);
        int index = Shape.FirstFull(axis, bi, ci);
        return index >= Shape.GetSize(axis)
            ? double.PositiveInfinity
            : Get(axis, index);
    }

    public double Max(Direction.Axis axis, double b, double c)
    {
        Direction.Axis bAxis = AxisCycle.Forward.Cycle(axis);
        Direction.Axis cAxis = AxisCycle.Backward.Cycle(axis);
        int bi = FindIndex(bAxis, b);
        int ci = FindIndex(cAxis, c);
        int index = Shape.LastFull(axis, bi, ci);
        return index <= 0
            ? double.NegativeInfinity
            : Get(axis, index);
    }

    protected virtual int FindIndex(Direction.Axis axis, double coordinate)
    {
        int start = 0;
        int end = Shape.GetSize(axis) + 1;

        while (start < end)
        {
            int middle = (start + end) >> 1;
            if (coordinate < Get(axis, middle))
                end = middle;
            else
                start = middle + 1;
        }

        return start - 1;
    }

    public Vec3? ClosestPointTo(Vec3 point)
    {
        ArgumentNullException.ThrowIfNull(point);
        if (IsEmpty())
            return null;

        Vec3? closest = null;
        double closestDistance = double.PositiveInfinity;
        ForAllBoxes((x1, y1, z1, x2, y2, z2) =>
        {
            var candidate = new Vec3(
                Mth.Clamp(point.X, x1, x2),
                Mth.Clamp(point.Y, y1, y2),
                Mth.Clamp(point.Z, z1, z2));
            double distance = point.DistanceToSquared(candidate);
            if (closest is null || distance < closestDistance)
            {
                closest = candidate;
                closestDistance = distance;
            }
        });
        return closest;
    }

    public bool IsCubeLike()
    {
        return IsCubeLikeAlong(Direction.Axis.X) &&
               IsCubeLikeAlong(Direction.Axis.Y) &&
               IsCubeLikeAlong(Direction.Axis.Z);
    }

    protected bool IsCubeLikeAlong(Direction.Axis axis)
    {
        DoubleList coordinates = GetCoords(axis);
        return coordinates.Count == 2 &&
               Math.Abs(coordinates.GetDouble(0)) <= Shapes.Epsilon &&
               Math.Abs(coordinates.GetDouble(1) - 1.0) <= Shapes.Epsilon;
    }

    public override string ToString() =>
        IsEmpty() ? "EMPTY" : $"VoxelShape[{Bounds()}]";
}
