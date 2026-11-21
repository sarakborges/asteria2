namespace Asteria.Core;

/// <summary>
/// Immutable-by-default three-dimensional integer vector, with protected mutation
/// for coordinate types that expose an explicit mutable view.
/// </summary>
public class Vec3i : IComparable<Vec3i>, IEquatable<Vec3i>
{
    public enum Axis
    {
        X,
        Y,
        Z
    }

    public static readonly Vec3i Zero = new(0, 0, 0);

    private int _x;
    private int _y;
    private int _z;

    public int X => GetX();
    public int Y => GetY();
    public int Z => GetZ();

    public Vec3i(int x, int y, int z)
    {
        _x = x;
        _y = y;
        _z = z;
    }

    public int GetX() => _x;

    public int GetY() => _y;

    public int GetZ() => _z;

    protected Vec3i SetX(int x)
    {
        _x = x;
        return this;
    }

    protected Vec3i SetY(int y)
    {
        _y = y;
        return this;
    }

    protected Vec3i SetZ(int z)
    {
        _z = z;
        return this;
    }

    public virtual Vec3i Offset(int x, int y, int z) =>
        x == 0 && y == 0 && z == 0
            ? this
            : new Vec3i(unchecked(GetX() + x), unchecked(GetY() + y), unchecked(GetZ() + z));

    public virtual Vec3i Offset(Vec3i vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return Offset(vector.GetX(), vector.GetY(), vector.GetZ());
    }

    public virtual Vec3i Subtract(Vec3i vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return Offset(
            unchecked(-vector.GetX()),
            unchecked(-vector.GetY()),
            unchecked(-vector.GetZ()));
    }

    public virtual Vec3i Multiply(int scale) =>
        scale == 1
            ? this
            : scale == 0
                ? Zero
                : new Vec3i(
                    unchecked(GetX() * scale),
                    unchecked(GetY() * scale),
                    unchecked(GetZ() * scale));

    public Vec3i Multiply(int xScale, int yScale, int zScale) =>
        new(
            unchecked(GetX() * xScale),
            unchecked(GetY() * yScale),
            unchecked(GetZ() * zScale));

    public virtual Vec3i Above() => Above(1);

    public virtual Vec3i Above(int steps) => Offset(0, steps, 0);

    public virtual Vec3i Below() => Below(1);

    public virtual Vec3i Below(int steps) => Offset(0, unchecked(-steps), 0);

    public virtual Vec3i North() => North(1);

    public virtual Vec3i North(int steps) => Offset(0, 0, unchecked(-steps));

    public virtual Vec3i South() => South(1);

    public virtual Vec3i South(int steps) => Offset(0, 0, steps);

    public virtual Vec3i West() => West(1);

    public virtual Vec3i West(int steps) => Offset(unchecked(-steps), 0, 0);

    public virtual Vec3i East() => East(1);

    public virtual Vec3i East(int steps) => Offset(steps, 0, 0);

    public virtual Vec3i Relative(Axis axis, int steps)
    {
        if (steps == 0)
            return this;

        return axis switch
        {
            Axis.X => Offset(steps, 0, 0),
            Axis.Y => Offset(0, steps, 0),
            Axis.Z => Offset(0, 0, steps),
            _ => throw new ArgumentOutOfRangeException(nameof(axis))
        };
    }

    public virtual Vec3i Cross(Vec3i vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        return new Vec3i(
            unchecked(GetY() * vector.GetZ() - GetZ() * vector.GetY()),
            unchecked(GetZ() * vector.GetX() - GetX() * vector.GetZ()),
            unchecked(GetX() * vector.GetY() - GetY() * vector.GetX()));
    }

    public bool CloserThan(Vec3i position, double distance)
    {
        ArgumentNullException.ThrowIfNull(position);
        return DistSqr(position) < distance * distance;
    }

    public double DistSqr(Vec3i position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return DistToLowCornerSqr(position.GetX(), position.GetY(), position.GetZ());
    }

    public double DistToCenterSqr(double x, double y, double z)
    {
        double dx = GetX() + 0.5 - x;
        double dy = GetY() + 0.5 - y;
        double dz = GetZ() + 0.5 - z;
        return dx * dx + dy * dy + dz * dz;
    }

    public double DistToLowCornerSqr(double x, double y, double z)
    {
        double dx = GetX() - x;
        double dy = GetY() - y;
        double dz = GetZ() - z;
        return dx * dx + dy * dy + dz * dz;
    }

    public int DistManhattan(Vec3i position)
    {
        ArgumentNullException.ThrowIfNull(position);

        int dx = JavaAbs(unchecked(position.GetX() - GetX()));
        int dy = JavaAbs(unchecked(position.GetY() - GetY()));
        int dz = JavaAbs(unchecked(position.GetZ() - GetZ()));
        return unchecked((int)((float)dx + (float)dy + (float)dz));
    }

    public int DistChessboard(Vec3i position)
    {
        ArgumentNullException.ThrowIfNull(position);

        int dx = JavaAbs(unchecked(GetX() - position.GetX()));
        int dy = JavaAbs(unchecked(GetY() - position.GetY()));
        int dz = JavaAbs(unchecked(GetZ() - position.GetZ()));
        return Math.Max(Math.Max(dx, dy), dz);
    }

    public bool DiffersHorizontally(Vec3i position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return GetX() != position.GetX() || GetZ() != position.GetZ();
    }

    public int Get(Axis axis) =>
        axis switch
        {
            Axis.X => GetX(),
            Axis.Y => GetY(),
            Axis.Z => GetZ(),
            _ => throw new ArgumentOutOfRangeException(nameof(axis))
        };

    public int CompareTo(Vec3i? position)
    {
        ArgumentNullException.ThrowIfNull(position);

        if (GetY() == position.GetY())
        {
            return GetZ() == position.GetZ()
                ? unchecked(GetX() - position.GetX())
                : unchecked(GetZ() - position.GetZ());
        }

        return unchecked(GetY() - position.GetY());
    }

    public bool Equals(Vec3i? other) =>
        other is not null &&
        GetX() == other.GetX() &&
        GetY() == other.GetY() &&
        GetZ() == other.GetZ();

    public override bool Equals(object? obj) =>
        obj is Vec3i other && Equals(other);

    public override int GetHashCode() =>
        unchecked((GetY() + GetZ() * 31) * 31 + GetX());

    public override string ToString() =>
        $"{GetType().Name}{{x={GetX()}, y={GetY()}, z={GetZ()}}}";

    public string ToShortString() =>
        $"{GetX()}, {GetY()}, {GetZ()}";

    private static int JavaAbs(int value) =>
        value < 0 ? unchecked(-value) : value;
}
