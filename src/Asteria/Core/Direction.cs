using System.Collections;
using Asteria.Serialization;
using Asteria.World.LevelGen;

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
        public static readonly Codec<Axis> CODEC = CreateStringRepresentableCodec(
            VALUES,
            static axis => axis.GetSerializedName(),
            ByName,
            static axis => Array.IndexOf(VALUES, axis));

        public string Name { get; }

        private Axis(string name) => Name = name;

        public string GetName() => Name;

        public static Axis[] GetValues() => (Axis[])VALUES.Clone();

        public static Axis? ByName(string? name) =>
            name is null ? null : VALUES.FirstOrDefault(axis => string.Equals(axis.Name, name, StringComparison.Ordinal));

        public bool IsVertical() => ReferenceEquals(this, Y);

        public bool IsHorizontal() => ReferenceEquals(this, X) || ReferenceEquals(this, Z);

        public static Axis GetRandom(IRandomSource random)
        {
            ArgumentNullException.ThrowIfNull(random);
            return VALUES[random.NextInt(VALUES.Length)];
        }

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

        public Direction GetRandomDirection(IRandomSource random)
        {
            ArgumentNullException.ThrowIfNull(random);
            return _faces[random.NextInt(_faces.Length)];
        }

        public Axis GetRandomAxis(IRandomSource random)
        {
            ArgumentNullException.ThrowIfNull(random);
            return _axes[random.NextInt(_axes.Length)];
        }

        public List<Direction> ShuffledCopy(IRandomSource random)
        {
            ArgumentNullException.ThrowIfNull(random);
            List<Direction> copy = new(_faces);
            Shuffle(copy, random);
            return copy;
        }

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

    /// <summary>
    /// Direction names in normal data and declaration-order IDs in compressed data,
    /// matching StringRepresentable.EnumCodec for Minecraft Java 26.3.
    /// </summary>
    public static readonly Codec<Direction> CODEC = CreateStringRepresentableCodec(
        VALUES,
        static direction => direction.GetSerializedName(),
        ByName,
        static direction => direction._data3d);

    public static readonly Codec<Direction> VERTICAL_CODEC = CODEC.Validate(
        static direction => direction.IsVertical()
            ? DataResult<Direction>.Success(direction)
            : DataResult<Direction>.Error("Expected a vertical direction"));

    private static readonly IReadOnlyList<Axis> YXZ_AXIS_ORDER =
        Array.AsReadOnly(new[] { Axis.Y, Axis.X, Axis.Z });
    private static readonly IReadOnlyList<Axis> YZX_AXIS_ORDER =
        Array.AsReadOnly(new[] { Axis.Y, Axis.Z, Axis.X });

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
        _normalVec3 = Vec3.AtLowerCornerOf(normal);
    }

    public static Direction GetRandom(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return VALUES[random.NextInt(VALUES.Length)];
    }

    public static List<Direction> AllShuffled(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        List<Direction> copy = new(VALUES);
        Shuffle(copy, random);
        return copy;
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

    public static Direction GetApproximateNearest(Vec3 vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return GetApproximateNearest(vector.X, vector.Y, vector.Z);
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

    public static IReadOnlyList<Axis> AxisStepOrder(Vec3 movement)
    {
        ArgumentNullException.ThrowIfNull(movement);
        return Math.Abs(movement.X) < Math.Abs(movement.Z)
            ? YZX_AXIS_ORDER
            : YXZ_AXIS_ORDER;
    }

    private static void Shuffle<T>(IList<T> values, IRandomSource random)
    {
        for (int size = values.Count; size > 1; size--)
        {
            int swapTo = random.NextInt(size);
            (values[size - 1], values[swapTo]) = (values[swapTo], values[size - 1]);
        }
    }

    private static int JavaAbs(int value) =>
        value < 0 ? unchecked(-value) : value;


    private static Codec<TValue> CreateStringRepresentableCodec<TValue>(
        IReadOnlyList<TValue> values,
        Func<TValue, string> getSerializedName,
        Func<string, TValue?> byName,
        Func<TValue, int> getId)
        where TValue : class
    {
        Codec<TValue> normal = Codecs.STRING.FlatXMap(
            name => byName(name) is { } value
                ? DataResult<TValue>.Success(value)
                : DataResult<TValue>.Error(() => "Unknown element: " + name),
            value => DataResult<string>.Success(getSerializedName(value)));

        Codec<TValue> compressed = Codecs.INT.FlatXMap(
            id => id >= 0 && id < values.Count
                ? DataResult<TValue>.Success(values[id])
                : DataResult<TValue>.Error(() => "Unknown element id: " + id),
            value =>
            {
                int id = getId(value);
                return id == -1
                    ? DataResult<int>.Error(() => "Element with unknown id: " + value)
                    : DataResult<int>.Success(id);
            });

        return new CompressedEnumCodec<TValue>(normal, compressed);
    }

    private sealed class CompressedEnumCodec<TValue> : Codec<TValue>
    {
        private readonly Codec<TValue> _normal;
        private readonly Codec<TValue> _compressed;

        public CompressedEnumCodec(Codec<TValue> normal, Codec<TValue> compressed)
        {
            _normal = normal;
            _compressed = compressed;
        }

        public override DataResult<TData> Encode<TData>(
            TValue input,
            DynamicOps<TData> ops,
            TData prefix) =>
            (ops.CompressMaps ? _compressed : _normal).Encode(input, ops, prefix);

        public override DataResult<Pair<TValue, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input) =>
            (ops.CompressMaps ? _compressed : _normal).Decode(ops, input);
    }

    public override string ToString() => _name;
}
