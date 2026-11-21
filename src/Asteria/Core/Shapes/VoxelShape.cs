using Asteria.Core;

namespace Asteria.Core.Shapes;

/// <summary>
/// Continuous geometry layered over a discrete voxel occupancy grid.
/// </summary>
public abstract class VoxelShape
{
    public DiscreteVoxelShape Shape { get; }

    private VoxelShape[]? _faces;

    protected VoxelShape(DiscreteVoxelShape shape) =>
        Shape = shape ?? throw new ArgumentNullException(nameof(shape));

    /// <summary>
    /// Gets the cached boundary shape facing the supplied direction.
    /// Empty and full-block shapes return themselves, matching the reference fast paths.
    /// </summary>
    public VoxelShape GetFaceShape(Direction direction)
    {
        ArgumentNullException.ThrowIfNull(direction);

        if (IsEmpty() || ReferenceEquals(this, Shapes.Block()))
            return this;

        _faces ??= new VoxelShape[6];
        int index = direction.Get3DDataValue();
        VoxelShape? cached = _faces[index];
        if (cached is not null)
            return cached;

        VoxelShape face = CalculateFace(direction);
        _faces[index] = face;
        return face;
    }

    private VoxelShape CalculateFace(Direction direction)
    {
        Direction.Axis axis = direction.GetAxis();
        if (IsCubeLikeAlong(axis))
            return this;

        double coordinate = ReferenceEquals(
            direction.GetAxisDirection(),
            Direction.AxisDirection.POSITIVE)
            ? 0.9999999
            : 1.0E-7;
        int sliceIndex = FindIndex(axis, coordinate);
        var slice = new SliceShape(this, axis, sliceIndex);
        if (slice.IsEmpty())
            return Shapes.Empty();

        return slice.IsCubeLike() ? Shapes.Block() : slice;
    }

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

    /// <summary>
    /// Emits the merged edges of this shape in continuous coordinates.
    /// </summary>
    public void ForAllEdges(Action<double, double, double, double, double, double> consumer)
    {
        ArgumentNullException.ThrowIfNull(consumer);

        DoubleList xCoords = GetCoords(Direction.Axis.X);
        DoubleList yCoords = GetCoords(Direction.Axis.Y);
        DoubleList zCoords = GetCoords(Direction.Axis.Z);

        Shape.ForAllEdges(
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

    /// <summary>
    /// Clips movement along one axis against occupied cells in this shape.
    /// </summary>
    public double Collide(Direction.Axis axis, AABB moving, double distance)
    {
        ArgumentNullException.ThrowIfNull(axis);
        ArgumentNullException.ThrowIfNull(moving);
        return CollideX(AxisCycleExtensions.Between(axis, Direction.Axis.X), moving, distance);
    }

    private double CollideX(AxisCycle transform, AABB moving, double distance)
    {
        if (IsEmpty())
            return distance;

        if (Math.Abs(distance) < 1.0E-7)
            return 0.0;

        AxisCycle inverse = transform.Inverse();
        Direction.Axis aAxis = inverse.Cycle(Direction.Axis.X);
        Direction.Axis bAxis = inverse.Cycle(Direction.Axis.Y);
        Direction.Axis cAxis = inverse.Cycle(Direction.Axis.Z);

        double maxA = moving.Max(aAxis);
        double minA = moving.Min(aAxis);
        int aMin = FindIndex(aAxis, minA + 1.0E-7);
        int aMax = FindIndex(aAxis, maxA - 1.0E-7);
        int bMin = Math.Max(0, FindIndex(bAxis, moving.Min(bAxis) + 1.0E-7));
        int bMax = Math.Min(
            Shape.GetSize(bAxis),
            FindIndex(bAxis, moving.Max(bAxis) - 1.0E-7) + 1);
        int cMin = Math.Max(0, FindIndex(cAxis, moving.Min(cAxis) + 1.0E-7));
        int cMax = Math.Min(
            Shape.GetSize(cAxis),
            FindIndex(cAxis, moving.Max(cAxis) - 1.0E-7) + 1);
        int aSize = Shape.GetSize(aAxis);

        if (distance > 0.0)
        {
            for (int a = aMax + 1; a < aSize; a++)
            {
                for (int b = bMin; b < bMax; b++)
                {
                    for (int c = cMin; c < cMax; c++)
                    {
                        if (Shape.IsFullWide(inverse, a, b, c))
                        {
                            double newDistance = Get(aAxis, a) - maxA;
                            if (newDistance >= -1.0E-7)
                                distance = Math.Min(distance, newDistance);

                            return distance;
                        }
                    }
                }
            }
        }
        else if (distance < 0.0)
        {
            for (int a = aMin - 1; a >= 0; a--)
            {
                for (int b = bMin; b < bMax; b++)
                {
                    for (int c = cMin; c < cMax; c++)
                    {
                        if (Shape.IsFullWide(inverse, a, b, c))
                        {
                            double newDistance = Get(aAxis, a + 1) - minA;
                            if (newDistance <= 1.0E-7)
                                distance = Math.Max(distance, newDistance);

                            return distance;
                        }
                    }
                }
            }
        }

        return distance;
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
