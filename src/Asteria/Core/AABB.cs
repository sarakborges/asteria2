using System.Globalization;
using System.Numerics;

namespace Asteria.Core;

/// <summary>
/// Immutable axis-aligned bounding box with the interval, overlap and segment-clipping
/// semantics used by the 26.3 spatial geometry implementation.
/// </summary>
public sealed class AABB : IEquatable<AABB>
{
    private const double Epsilon = 1.0E-7;

    public double MinX { get; }
    public double MinY { get; }
    public double MinZ { get; }
    public double MaxX { get; }
    public double MaxY { get; }
    public double MaxZ { get; }

    public AABB(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        MinX = Math.Min(minX, maxX);
        MinY = Math.Min(minY, maxY);
        MinZ = Math.Min(minZ, maxZ);
        MaxX = Math.Max(minX, maxX);
        MaxY = Math.Max(minY, maxY);
        MaxZ = Math.Max(minZ, maxZ);
    }

    public AABB(BlockPos position)
        : this(
            (position ?? throw new ArgumentNullException(nameof(position))).GetX(),
            position.GetY(),
            position.GetZ(),
            position.GetX() + 1.0,
            position.GetY() + 1.0,
            position.GetZ() + 1.0)
    {
    }

    public AABB(Vec3 begin, Vec3 end)
        : this(
            (begin ?? throw new ArgumentNullException(nameof(begin))).X,
            begin.Y,
            begin.Z,
            (end ?? throw new ArgumentNullException(nameof(end))).X,
            end.Y,
            end.Z)
    {
    }

    public static AABB UnitCubeFromLowerCorner(Vec3 position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return new AABB(
            position.X, position.Y, position.Z,
            position.X + 1.0, position.Y + 1.0, position.Z + 1.0);
    }

    public static AABB EncapsulatingFullBlocks(BlockPos position0, BlockPos position1)
    {
        ArgumentNullException.ThrowIfNull(position0);
        ArgumentNullException.ThrowIfNull(position1);
        return new AABB(
            Math.Min(position0.GetX(), position1.GetX()),
            Math.Min(position0.GetY(), position1.GetY()),
            Math.Min(position0.GetZ(), position1.GetZ()),
            Math.Max(position0.GetX(), position1.GetX()) + 1.0,
            Math.Max(position0.GetY(), position1.GetY()) + 1.0,
            Math.Max(position0.GetZ(), position1.GetZ()) + 1.0);
    }

    public AABB SetMinX(double minX) => new(minX, MinY, MinZ, MaxX, MaxY, MaxZ);
    public AABB SetMinY(double minY) => new(MinX, minY, MinZ, MaxX, MaxY, MaxZ);
    public AABB SetMinZ(double minZ) => new(MinX, MinY, minZ, MaxX, MaxY, MaxZ);
    public AABB SetMaxX(double maxX) => new(MinX, MinY, MinZ, maxX, MaxY, MaxZ);
    public AABB SetMaxY(double maxY) => new(MinX, MinY, MinZ, MaxX, maxY, MaxZ);
    public AABB SetMaxZ(double maxZ) => new(MinX, MinY, MinZ, MaxX, MaxY, maxZ);

    public double Min(Direction.Axis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        return axis.Choose(MinX, MinY, MinZ);
    }

    public double Max(Direction.Axis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        return axis.Choose(MaxX, MaxY, MaxZ);
    }

    public AABB Contract(double x, double y, double z)
    {
        double minX = MinX;
        double minY = MinY;
        double minZ = MinZ;
        double maxX = MaxX;
        double maxY = MaxY;
        double maxZ = MaxZ;

        if (x < 0.0) minX -= x;
        else if (x > 0.0) maxX -= x;

        if (y < 0.0) minY -= y;
        else if (y > 0.0) maxY -= y;

        if (z < 0.0) minZ -= z;
        else if (z > 0.0) maxZ -= z;

        return new AABB(minX, minY, minZ, maxX, maxY, maxZ);
    }

    public AABB ExpandTowards(Vec3 delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        return ExpandTowards(delta.X, delta.Y, delta.Z);
    }

    public AABB ExpandTowards(double x, double y, double z)
    {
        double minX = MinX;
        double minY = MinY;
        double minZ = MinZ;
        double maxX = MaxX;
        double maxY = MaxY;
        double maxZ = MaxZ;

        if (x < 0.0) minX += x;
        else if (x > 0.0) maxX += x;

        if (y < 0.0) minY += y;
        else if (y > 0.0) maxY += y;

        if (z < 0.0) minZ += z;
        else if (z > 0.0) maxZ += z;

        return new AABB(minX, minY, minZ, maxX, maxY, maxZ);
    }

    public AABB Inflate(double x, double y, double z) =>
        new(MinX - x, MinY - y, MinZ - z, MaxX + x, MaxY + y, MaxZ + z);

    public AABB Inflate(double amount) => Inflate(amount, amount, amount);

    public AABB Intersect(AABB other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new AABB(
            Math.Max(MinX, other.MinX),
            Math.Max(MinY, other.MinY),
            Math.Max(MinZ, other.MinZ),
            Math.Min(MaxX, other.MaxX),
            Math.Min(MaxY, other.MaxY),
            Math.Min(MaxZ, other.MaxZ));
    }

    public AABB MinMax(AABB other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new AABB(
            Math.Min(MinX, other.MinX),
            Math.Min(MinY, other.MinY),
            Math.Min(MinZ, other.MinZ),
            Math.Max(MaxX, other.MaxX),
            Math.Max(MaxY, other.MaxY),
            Math.Max(MaxZ, other.MaxZ));
    }

    public AABB Move(double x, double y, double z) =>
        new(MinX + x, MinY + y, MinZ + z, MaxX + x, MaxY + y, MaxZ + z);

    public AABB Move(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return Move(position.GetX(), position.GetY(), position.GetZ());
    }

    public AABB Move(Vec3 position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return Move(position.X, position.Y, position.Z);
    }

    public AABB Move(Vector3 position) =>
        Move(position.X, position.Y, position.Z);

    public bool Intersects(AABB other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Intersects(other.MinX, other.MinY, other.MinZ, other.MaxX, other.MaxY, other.MaxZ);
    }

    public bool Intersects(
        double minX, double minY, double minZ,
        double maxX, double maxY, double maxZ) =>
        MinX < maxX && MaxX > minX &&
        MinY < maxY && MaxY > minY &&
        MinZ < maxZ && MaxZ > minZ;

    public bool Intersects(Vec3 min, Vec3 max)
    {
        ArgumentNullException.ThrowIfNull(min);
        ArgumentNullException.ThrowIfNull(max);
        return Intersects(
            Math.Min(min.X, max.X), Math.Min(min.Y, max.Y), Math.Min(min.Z, max.Z),
            Math.Max(min.X, max.X), Math.Max(min.Y, max.Y), Math.Max(min.Z, max.Z));
    }

    public bool Intersects(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return Intersects(
            position.GetX(), position.GetY(), position.GetZ(),
            position.GetX() + 1.0, position.GetY() + 1.0, position.GetZ() + 1.0);
    }

    public bool Contains(Vec3 position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return Contains(position.X, position.Y, position.Z);
    }

    public bool Contains(double x, double y, double z) =>
        x >= MinX && x < MaxX &&
        y >= MinY && y < MaxY &&
        z >= MinZ && z < MaxZ;

    public double GetSize() => (GetXSize() + GetYSize() + GetZSize()) / 3.0;
    public double GetXSize() => MaxX - MinX;
    public double GetYSize() => MaxY - MinY;
    public double GetZSize() => MaxZ - MinZ;

    public AABB Deflate(double x, double y, double z) => Inflate(-x, -y, -z);
    public AABB Deflate(double amount) => Inflate(-amount);

    public Vec3? Clip(Vec3 from, Vec3 to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        return Clip(MinX, MinY, MinZ, MaxX, MaxY, MaxZ, from, to);
    }

    public static Vec3? Clip(
        double minX, double minY, double minZ,
        double maxX, double maxY, double maxZ,
        Vec3 from, Vec3 to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var scaleReference = new[] { 1.0 };
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double dz = to.Z - from.Z;

        Direction? direction = GetDirection(
            minX, minY, minZ, maxX, maxY, maxZ,
            from, scaleReference, null, dx, dy, dz);
        if (direction is null)
            return null;

        double scale = scaleReference[0];
        return from.Add(scale * dx, scale * dy, scale * dz);
    }

    /// <summary>
    /// Clips a segment against a collection of local AABBs and returns the nearest block hit.
    /// </summary>
    public static BlockHitResult? Clip(
        IEnumerable<AABB> boxes,
        Vec3 from,
        Vec3 to,
        BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(position);

        var scaleReference = new[] { 1.0 };
        Direction? direction = null;
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double dz = to.Z - from.Z;

        foreach (AABB box in boxes)
        {
            ArgumentNullException.ThrowIfNull(box);
            AABB worldBox = box.Move(position);
            direction = GetDirection(
                worldBox.MinX, worldBox.MinY, worldBox.MinZ,
                worldBox.MaxX, worldBox.MaxY, worldBox.MaxZ,
                from, scaleReference, direction, dx, dy, dz);
        }

        if (direction is null)
            return null;

        double scale = scaleReference[0];
        return new BlockHitResult(
            from.Add(scale * dx, scale * dy, scale * dz),
            direction,
            position,
            inside: false);
    }

    private static Direction? GetDirection(
        double minX, double minY, double minZ,
        double maxX, double maxY, double maxZ,
        Vec3 from, double[] scaleReference, Direction? direction,
        double dx, double dy, double dz)
    {
        if (dx > Epsilon)
            direction = ClipPoint(scaleReference, direction, dx, dy, dz,
                minX, minY, maxY, minZ, maxZ, Direction.WEST, from.X, from.Y, from.Z);
        else if (dx < -Epsilon)
            direction = ClipPoint(scaleReference, direction, dx, dy, dz,
                maxX, minY, maxY, minZ, maxZ, Direction.EAST, from.X, from.Y, from.Z);

        if (dy > Epsilon)
            direction = ClipPoint(scaleReference, direction, dy, dz, dx,
                minY, minZ, maxZ, minX, maxX, Direction.DOWN, from.Y, from.Z, from.X);
        else if (dy < -Epsilon)
            direction = ClipPoint(scaleReference, direction, dy, dz, dx,
                maxY, minZ, maxZ, minX, maxX, Direction.UP, from.Y, from.Z, from.X);

        if (dz > Epsilon)
            direction = ClipPoint(scaleReference, direction, dz, dx, dy,
                minZ, minX, maxX, minY, maxY, Direction.NORTH, from.Z, from.X, from.Y);
        else if (dz < -Epsilon)
            direction = ClipPoint(scaleReference, direction, dz, dx, dy,
                maxZ, minX, maxX, minY, maxY, Direction.SOUTH, from.Z, from.X, from.Y);

        return direction;
    }

    private static Direction? ClipPoint(
        double[] scaleReference, Direction? direction,
        double da, double db, double dc,
        double point,
        double minB, double maxB, double minC, double maxC,
        Direction newDirection,
        double fromA, double fromB, double fromC)
    {
        double scale = (point - fromA) / da;
        double pointB = fromB + scale * db;
        double pointC = fromC + scale * dc;

        if (0.0 < scale && scale < scaleReference[0] &&
            minB - Epsilon < pointB && pointB < maxB + Epsilon &&
            minC - Epsilon < pointC && pointC < maxC + Epsilon)
        {
            scaleReference[0] = scale;
            return newDirection;
        }

        return direction;
    }

    public bool CollidedAlongVector(Vec3 vector, IReadOnlyList<AABB> boxes)
    {
        ArgumentNullException.ThrowIfNull(vector);
        ArgumentNullException.ThrowIfNull(boxes);

        Vec3 from = GetCenter();
        Vec3 to = from.Add(vector);
        foreach (AABB shapePart in boxes)
        {
            ArgumentNullException.ThrowIfNull(shapePart);
            AABB inflated = shapePart.Inflate(
                GetXSize() * 0.5 - Epsilon,
                GetYSize() * 0.5 - Epsilon,
                GetZSize() * 0.5 - Epsilon);
            if (inflated.Contains(to) || inflated.Contains(from))
                return true;

            if (inflated.Clip(from, to) is not null)
                return true;
        }

        return false;
    }

    public double DistanceToSqr(Vec3 point)
    {
        ArgumentNullException.ThrowIfNull(point);
        double dx = Math.Max(Math.Max(MinX - point.X, point.X - MaxX), 0.0);
        double dy = Math.Max(Math.Max(MinY - point.Y, point.Y - MaxY), 0.0);
        double dz = Math.Max(Math.Max(MinZ - point.Z, point.Z - MaxZ), 0.0);
        return Mth.LengthSquared(dx, dy, dz);
    }

    public double DistanceToSqr(AABB other)
    {
        ArgumentNullException.ThrowIfNull(other);
        double dx = Math.Max(Math.Max(MinX - other.MaxX, other.MinX - MaxX), 0.0);
        double dy = Math.Max(Math.Max(MinY - other.MaxY, other.MinY - MaxY), 0.0);
        double dz = Math.Max(Math.Max(MinZ - other.MaxZ, other.MinZ - MaxZ), 0.0);
        return Mth.LengthSquared(dx, dy, dz);
    }

    public bool HasNaN() =>
        double.IsNaN(MinX) || double.IsNaN(MinY) || double.IsNaN(MinZ) ||
        double.IsNaN(MaxX) || double.IsNaN(MaxY) || double.IsNaN(MaxZ);

    public Vec3 GetCenter() =>
        new(Mth.Lerp(0.5, MinX, MaxX), Mth.Lerp(0.5, MinY, MaxY), Mth.Lerp(0.5, MinZ, MaxZ));

    public Vec3 GetBottomCenter() =>
        new(Mth.Lerp(0.5, MinX, MaxX), MinY, Mth.Lerp(0.5, MinZ, MaxZ));

    public Vec3 GetMinPosition() => new(MinX, MinY, MinZ);
    public Vec3 GetMaxPosition() => new(MaxX, MaxY, MaxZ);

    public static AABB OfSize(Vec3 center, double sizeX, double sizeY, double sizeZ)
    {
        ArgumentNullException.ThrowIfNull(center);
        return new AABB(
            center.X - sizeX / 2.0,
            center.Y - sizeY / 2.0,
            center.Z - sizeZ / 2.0,
            center.X + sizeX / 2.0,
            center.Y + sizeY / 2.0,
            center.Z + sizeZ / 2.0);
    }

    public AABB NextDeflated() =>
        new(
            Math.BitIncrement(MinX),
            Math.BitIncrement(MinY),
            Math.BitIncrement(MinZ),
            Math.BitDecrement(MaxX),
            Math.BitDecrement(MaxY),
            Math.BitDecrement(MaxZ));

    public bool Equals(AABB? other) =>
        other is not null &&
        JavaDoubleCompare(MinX, other.MinX) == 0 &&
        JavaDoubleCompare(MinY, other.MinY) == 0 &&
        JavaDoubleCompare(MinZ, other.MinZ) == 0 &&
        JavaDoubleCompare(MaxX, other.MaxX) == 0 &&
        JavaDoubleCompare(MaxY, other.MaxY) == 0 &&
        JavaDoubleCompare(MaxZ, other.MaxZ) == 0;

    public override bool Equals(object? obj) =>
        ReferenceEquals(this, obj) || obj is AABB other && Equals(other);

    public override int GetHashCode()
    {
        long bits = DoubleToLongBits(MinX);
        int result = unchecked((int)(bits ^ (long)((ulong)bits >> 32)));
        bits = DoubleToLongBits(MinY);
        result = unchecked(31 * result + (int)(bits ^ (long)((ulong)bits >> 32)));
        bits = DoubleToLongBits(MinZ);
        result = unchecked(31 * result + (int)(bits ^ (long)((ulong)bits >> 32)));
        bits = DoubleToLongBits(MaxX);
        result = unchecked(31 * result + (int)(bits ^ (long)((ulong)bits >> 32)));
        bits = DoubleToLongBits(MaxY);
        result = unchecked(31 * result + (int)(bits ^ (long)((ulong)bits >> 32)));
        bits = DoubleToLongBits(MaxZ);
        return unchecked(31 * result + (int)(bits ^ (long)((ulong)bits >> 32)));
    }

    public override string ToString() =>
        "AABB[" + Vec3.JavaDoubleToString(MinX) + ", " +
        Vec3.JavaDoubleToString(MinY) + ", " +
        Vec3.JavaDoubleToString(MinZ) + "] -> [" +
        Vec3.JavaDoubleToString(MaxX) + ", " +
        Vec3.JavaDoubleToString(MaxY) + ", " +
        Vec3.JavaDoubleToString(MaxZ) + "]";

    private static int JavaDoubleCompare(double left, double right)
    {
        if (left < right) return -1;
        if (left > right) return 1;

        long leftBits = DoubleToLongBits(left);
        long rightBits = DoubleToLongBits(right);
        return leftBits == rightBits ? 0 : leftBits < rightBits ? -1 : 1;
    }

    private static long DoubleToLongBits(double value) =>
        double.IsNaN(value)
            ? unchecked((long)0x7ff8000000000000UL)
            : BitConverter.DoubleToInt64Bits(value);

    public sealed class Builder
    {
        private float _minX = float.PositiveInfinity;
        private float _minY = float.PositiveInfinity;
        private float _minZ = float.PositiveInfinity;
        private float _maxX = float.NegativeInfinity;
        private float _maxY = float.NegativeInfinity;
        private float _maxZ = float.NegativeInfinity;
        private bool _defined;

        public void Include(Vector3 vector)
        {
            _minX = Math.Min(_minX, vector.X);
            _minY = Math.Min(_minY, vector.Y);
            _minZ = Math.Min(_minZ, vector.Z);
            _maxX = Math.Max(_maxX, vector.X);
            _maxY = Math.Max(_maxY, vector.Y);
            _maxZ = Math.Max(_maxZ, vector.Z);
            _defined = true;
        }

        public bool IsDefined() => _defined;

        public AABB Build()
        {
            if (!_defined)
                throw new InvalidOperationException("Cannot build an undefined AABB. Include at least one point.");

            return new AABB(_minX, _minY, _minZ, _maxX, _maxY, _maxZ);
        }
    }
}
