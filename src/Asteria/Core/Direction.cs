using System.Collections;

namespace Asteria.Core;

/// <summary>
/// Six axis-aligned directions with the same ID tables, axis metadata and rotational
/// relationships as the 26.3 core Direction enum.
/// </summary>
public sealed class Direction
{
    public sealed class Axis
    {
        public static readonly Axis X = new("x");
        public static readonly Axis Y = new("y");
        public static readonly Axis Z = new("z");
        private static readonly Axis[] VALUES = { X, Y, Z };

        public string Name { get; }

        private Axis(string name) => Name = name;

        public string GetName() => Name;

        public static Axis[] GetValues() => (Axis[])VALUES.Clone();

        public static Axis? ByName(string? name) =>
            name is null ? null : VALUES.FirstOrDefault(axis => string.Equals(axis.Name, name, StringComparison.Ordinal));

        public bool IsVertical() => ReferenceEquals(this, Y);

        public bool IsHorizontal() => ReferenceEquals(this, X) || ReferenceEquals(this, Z);

        public Direction GetPositive() =>
            ReferenceEquals(this, X) ? EAST :
            ReferenceEquals(this, Y) ? UP : SOUTH;

        public Direction GetNegative() =>
            ReferenceEquals(this, X) ? WEST :
            ReferenceEquals(this, Y) ? DOWN : NORTH;

        public Direction[] GetDirections() => new[] { GetPositive(), GetNegative() };

        public bool Test(Direction? input) =>
            input is not null && ReferenceEquals(input.GetAxis(), this);

        public Plane GetPlane() =>
            ReferenceEquals(this, Y) ? Plane.VERTICAL : Plane.HORIZONTAL;

        public string GetSerializedName() => Name;

        public int Choose(int x, int y, int z) =>
            ReferenceEquals(this, X) ? x : ReferenceEquals(this, Y) ? y : z;

        public double Choose(double x, double y, double z) =>
            ReferenceEquals(this, X) ? x : ReferenceEquals(this, Y) ? y : z;

        public bool Choose(bool x, bool y, bool z) =>
            ReferenceEquals(this, X) ? x : ReferenceEquals(this, Y) ? y : z;

        public override string ToString() => Name;
    }

    public sealed class AxisDirection
    {
        public static readonly AxisDirection POSITIVE = new(1, "Towards positive");
        public static readonly AxisDirection NEGATIVE = new(-1, "Towards negative");

        public int Step { get; }
        public string Name { get; }

        public int GetStep() => Step;

        public string GetName() => Name;

        private AxisDirection(int step, string name)
        {
            Step = step;
            Name = name;
        }

        public AxisDirection Opposite() =>
            ReferenceEquals(this, POSITIVE) ? NEGATIVE : POSITIVE;

        public override string ToString() => Name;
    }

    public sealed class Plane : IEnumerable<Direction>
    {
        public static readonly Plane HORIZONTAL =
            new(new[] { NORTH, EAST, SOUTH, WEST }, new[] { Axis.X, Axis.Z });
        public static readonly Plane VERTICAL =
            new(new[] { UP, DOWN }, new[] { Axis.Y });

        private readonly Direction[] _faces;
        private readonly Axis[] _axes;

        public int Length => _faces.Length;

        private Plane(Direction[] faces, Axis[] axes)
        {
            _faces = faces;
            _axes = axes;
        }

        public Direction[] GetDirections() => (Direction[])_faces.Clone();

        public Axis[] GetAxes() => (Axis[])_axes.Clone();

        public bool Test(Direction? input) =>
            input is not null && ReferenceEquals(input.GetAxis().GetPlane(), this);

        public IEnumerable<Direction> Stream() => _faces;

        public IEnumerator<Direction> GetEnumerator() => ((IEnumerable<Direction>)_faces).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() =>
            ReferenceEquals(this, HORIZONTAL) ? "HORIZONTAL" : "VERTICAL";
    }

    public static readonly Direction DOWN = new(
        0, 1, -1, "down", AxisDirection.NEGATIVE, Axis.Y, new Vec3i(0, -1, 0));
    public static readonly Direction UP = new(
        1, 0, -1, "up", AxisDirection.POSITIVE, Axis.Y, new Vec3i(0, 1, 0));
    public static readonly Direction NORTH = new(
        2, 3, 2, "north", AxisDirection.NEGATIVE, Axis.Z, new Vec3i(0, 0, -1));
    public static readonly Direction SOUTH = new(
        3, 2, 0, "south", AxisDirection.POSITIVE, Axis.Z, new Vec3i(0, 0, 1));
    public static readonly Direction WEST = new(
        4, 5, 1, "west", AxisDirection.NEGATIVE, Axis.X, new Vec3i(-1, 0, 0));
    public static readonly Direction EAST = new(
        5, 4, 3, "east", AxisDirection.POSITIVE, Axis.X, new Vec3i(1, 0, 0));

    private static readonly Direction[] VALUES = { DOWN, UP, NORTH, SOUTH, WEST, EAST };
    private static readonly Direction[] BY_3D_DATA =
        VALUES.OrderBy(direction => direction._data3d).ToArray();
    private static readonly Direction[] BY_2D_DATA =
        VALUES.Where(direction => direction._axis.IsHorizontal())
            .OrderBy(direction => direction._data2d)
            .ToArray();

    private readonly int _data3d;
    private readonly int _oppositeIndex;
    private readonly int _data2d;
    private readonly string _name;
    private readonly Axis _axis;
    private readonly AxisDirection _axisDirection;
    private readonly Vec3i _normal;
    private readonly Vec3 _normalVec3;

    private Direction(
        int data3d,
        int oppositeIndex,
        int data2d,
        string name,
        AxisDirection axisDirection,
        Axis axis,
        Vec3i normal)
    {
        _data3d = data3d;
        _oppositeIndex = oppositeIndex;
        _data2d = data2d;
        _name = name;
        _axisDirection = axisDirection;
        _axis = axis;
        _normal = normal;
    }

    public static Direction[] GetValues() => (Direction[])VALUES.Clone();

    public static Direction? ByName(string? name) =>
        name is null ? null : VALUES.FirstOrDefault(
            direction => string.Equals(direction._name, name, StringComparison.Ordinal));

    public int Get3DDataValue() => _data3d;

    public int Get2DDataValue() => _data2d;

    public Axis GetAxis() => _axis;

    public AxisDirection GetAxisDirection() => _axisDirection;

    public string GetName() => _name;

    public string GetSerializedName() => _name;

    public Vec3i GetStep() => GetUnitVec3i();

    public Vec3i GetUnitVec3i() => _normal;

    public Vec3 GetUnitVec3() => _normalVec3;

    public int GetStepX() => _normal.GetX();

    public int GetStepY() => _normal.GetY();

    public int GetStepZ() => _normal.GetZ();

    public bool IsVertical() => _axis.IsVertical();

    public bool IsHorizontal() => _axis.IsHorizontal();

    public Plane GetPlane() => _axis.GetPlane();

    public Direction GetOpposite() => From3DDataValue(_oppositeIndex);

    public static Direction From3DDataValue(int data) =>
        BY_3D_DATA[JavaAbs(data % BY_3D_DATA.Length)];

    public static Direction From2DDataValue(int data) =>
        BY_2D_DATA[JavaAbs(data % BY_2D_DATA.Length)];

    public static Direction FromYRot(double yRot)
    {
        return From2DDataValue(Mth.Floor(yRot / 90.0 + 0.5) & 3);
    }

    public static Direction FromAxisAndDirection(Axis axis, AxisDirection direction)
    {
        ArgumentNullException.ThrowIfNull(axis);
        ArgumentNullException.ThrowIfNull(direction);

        if (ReferenceEquals(axis, Axis.X))
            return ReferenceEquals(direction, AxisDirection.POSITIVE) ? EAST : WEST;
        if (ReferenceEquals(axis, Axis.Y))
            return ReferenceEquals(direction, AxisDirection.POSITIVE) ? UP : DOWN;
        return ReferenceEquals(direction, AxisDirection.POSITIVE) ? SOUTH : NORTH;
    }

    public static Direction Get(AxisDirection axisDirection, Axis axis)
    {
        ArgumentNullException.ThrowIfNull(axisDirection);
        ArgumentNullException.ThrowIfNull(axis);

        foreach (Direction direction in VALUES)
        {
            if (ReferenceEquals(direction._axisDirection, axisDirection) &&
                ReferenceEquals(direction._axis, axis))
                return direction;
        }

        throw new ArgumentException("No such direction: " + axisDirection + " " + axis);
    }

    public float ToYRot() => (_data2d & 3) * 90.0f;

    public static float GetYRot(Direction direction)
    {
        ArgumentNullException.ThrowIfNull(direction);
        if (ReferenceEquals(direction, NORTH)) return 180.0f;
        if (ReferenceEquals(direction, SOUTH)) return 0.0f;
        if (ReferenceEquals(direction, WEST)) return 90.0f;
        if (ReferenceEquals(direction, EAST)) return -90.0f;
        throw new InvalidOperationException("No y-Rot for vertical axis: " + direction);
    }

    public Direction GetClockWise(Axis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        if (ReferenceEquals(axis, Axis.X))
            return this != WEST && this != EAST ? GetClockWiseX() : this;
        if (ReferenceEquals(axis, Axis.Y))
            return this != UP && this != DOWN ? GetClockWise() : this;
        return this != NORTH && this != SOUTH ? GetClockWiseZ() : this;
    }

    public Direction GetCounterClockWise(Axis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        if (ReferenceEquals(axis, Axis.X))
            return this != WEST && this != EAST ? GetCounterClockWiseX() : this;
        if (ReferenceEquals(axis, Axis.Y))
            return this != UP && this != DOWN ? GetCounterClockWise() : this;
        return this != NORTH && this != SOUTH ? GetCounterClockWiseZ() : this;
    }

    public Direction GetClockWise() =>
        ReferenceEquals(this, NORTH) ? EAST :
        ReferenceEquals(this, SOUTH) ? WEST :
        ReferenceEquals(this, WEST) ? NORTH :
        ReferenceEquals(this, EAST) ? SOUTH :
        throw new InvalidOperationException("Unable to get Y-rotated facing of " + this);

    private Direction GetClockWiseX() =>
        ReferenceEquals(this, DOWN) ? SOUTH :
        ReferenceEquals(this, UP) ? NORTH :
        ReferenceEquals(this, NORTH) ? DOWN :
        ReferenceEquals(this, SOUTH) ? UP :
        throw new InvalidOperationException("Unable to get X-rotated facing of " + this);

    private Direction GetCounterClockWiseX() =>
        ReferenceEquals(this, DOWN) ? NORTH :
        ReferenceEquals(this, UP) ? SOUTH :
        ReferenceEquals(this, NORTH) ? UP :
        ReferenceEquals(this, SOUTH) ? DOWN :
        throw new InvalidOperationException("Unable to get X-rotated facing of " + this);

    private Direction GetClockWiseZ() =>
        ReferenceEquals(this, DOWN) ? WEST :
        ReferenceEquals(this, UP) ? EAST :
        ReferenceEquals(this, WEST) ? UP :
        ReferenceEquals(this, EAST) ? DOWN :
        throw new InvalidOperationException("Unable to get Z-rotated facing of " + this);

    private Direction GetCounterClockWiseZ() =>
        ReferenceEquals(this, DOWN) ? EAST :
        ReferenceEquals(this, UP) ? WEST :
        ReferenceEquals(this, WEST) ? DOWN :
        ReferenceEquals(this, EAST) ? UP :
        throw new InvalidOperationException("Unable to get Z-rotated facing of " + this);

    public Direction GetCounterClockWise() =>
        ReferenceEquals(this, NORTH) ? WEST :
        ReferenceEquals(this, SOUTH) ? EAST :
        ReferenceEquals(this, WEST) ? SOUTH :
        ReferenceEquals(this, EAST) ? NORTH :
        throw new InvalidOperationException("Unable to get CCW facing of " + this);

    public bool IsFacingAngle(float yAngle)
    {
        float radians = yAngle * (float)(Math.PI / 180.0);
        float dx = -Mth.Sin(radians);
        float dz = Mth.Cos(radians);
        return _normal.GetX() * dx + _normal.GetZ() * dz > 0.0f;
    }

    public static Direction GetApproximateNearest(double dx, double dy, double dz) =>
        GetApproximateNearest((float)dx, (float)dy, (float)dz);

    public static Direction GetApproximateNearest(float dx, float dy, float dz)
    {
        Direction result = NORTH;
        float highestDot = float.Epsilon;

        foreach (Direction direction in VALUES)
        {
            float dot = dx * direction._normal.GetX() +
                dy * direction._normal.GetY() +
                dz * direction._normal.GetZ();
            if (dot > highestDot)
            {
                highestDot = dot;
                result = direction;
            }
        }

        return result;
    }

    public static Direction? GetNearest(int x, int y, int z, Direction? orElse)
    {
        int absX = JavaAbs(x);
        int absY = JavaAbs(y);
        int absZ = JavaAbs(z);
        if (absX > absZ && absX > absY)
            return x < 0 ? WEST : EAST;
        if (absZ > absX && absZ > absY)
            return z < 0 ? NORTH : SOUTH;
        if (absY > absX && absY > absZ)
            return y < 0 ? DOWN : UP;
        return orElse;
    }

    public static Direction? GetNearest(Vec3i vector, Direction? orElse)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return GetNearest(vector.GetX(), vector.GetY(), vector.GetZ(), orElse);
    }

    private static int JavaAbs(int value) =>
        value < 0 ? unchecked(-value) : value;

    public override string ToString() => _name;
}
