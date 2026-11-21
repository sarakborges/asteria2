using System.Globalization;

namespace Asteria.Core;

/// <summary>
/// Immutable real-valued three-dimensional vector, ported from the 26.3 spatial vector contract.
/// </summary>
public sealed class Vec3 : Position, IEquatable<Vec3>
{
    public static readonly Vec3 ZERO = new(0.0, 0.0, 0.0);
    public static readonly Vec3 X_AXIS = new(1.0, 0.0, 0.0);
    public static readonly Vec3 Y_AXIS = new(0.0, 1.0, 0.0);
    public static readonly Vec3 Z_AXIS = new(0.0, 0.0, 1.0);

    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public Vec3(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Vec3(Vec3i vector)
        : this(
            (vector ?? throw new ArgumentNullException(nameof(vector))).GetX(),
            vector.GetY(),
            vector.GetZ())
    {
    }

    public double GetX() => X;
    public double GetY() => Y;
    public double GetZ() => Z;

    public static Vec3 AtLowerCornerOf(Vec3i position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return new Vec3(position.GetX(), position.GetY(), position.GetZ());
    }

    public static Vec3 AtLowerCornerWithOffset(Vec3i position, double x, double y, double z)
    {
        ArgumentNullException.ThrowIfNull(position);
        return new Vec3(position.GetX() + x, position.GetY() + y, position.GetZ() + z);
    }

    public static Vec3 AtCenterOf(Vec3i position) =>
        AtLowerCornerWithOffset(position, 0.5, 0.5, 0.5);

    public static Vec3 AtCenterOfWithY(Vec3i position, double y)
    {
        ArgumentNullException.ThrowIfNull(position);
        return new Vec3(position.GetX() + 0.5, y, position.GetZ() + 0.5);
    }

    public static Vec3 AtBottomCenterOf(Vec3i position) =>
        AtLowerCornerWithOffset(position, 0.5, 0.0, 0.5);

    public static Vec3 UpFromBottomCenterOf(Vec3i position, double yOffset) =>
        AtLowerCornerWithOffset(position, 0.5, yOffset, 0.5);

    public Vec3 VectorTo(Vec3 vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return new Vec3(vector.X - X, vector.Y - Y, vector.Z - Z);
    }

    public Vec3 Normalize()
    {
        double distance = Math.Sqrt(X * X + Y * Y + Z * Z);
        return distance < 1.0E-5F
            ? ZERO
            : new Vec3(X / distance, Y / distance, Z / distance);
    }

    public double Dot(Vec3 vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return X * vector.X + Y * vector.Y + Z * vector.Z;
    }

    public Vec3 Cross(Vec3 vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return new Vec3(
            Y * vector.Z - Z * vector.Y,
            Z * vector.X - X * vector.Z,
            X * vector.Y - Y * vector.X);
    }

    public Vec3 Subtract(Vec3 vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return Subtract(vector.X, vector.Y, vector.Z);
    }

    public Vec3 Subtract(double value) => Subtract(value, value, value);

    public Vec3 Subtract(double x, double y, double z) =>
        Add(-x, -y, -z);

    public Vec3 Add(double value) => Add(value, value, value);

    public Vec3 Add(Vec3 vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return Add(vector.X, vector.Y, vector.Z);
    }

    public Vec3 Add(double x, double y, double z) =>
        new(X + x, Y + y, Z + z);

    public bool CloserThan(Position position, double distance)
    {
        ArgumentNullException.ThrowIfNull(position);
        return DistanceToSquared(position.GetX(), position.GetY(), position.GetZ()) < distance * distance;
    }

    public double DistanceTo(Vec3 vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        double dx = vector.X - X;
        double dy = vector.Y - Y;
        double dz = vector.Z - Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public double DistanceToSquared(Vec3 vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        double dx = vector.X - X;
        double dy = vector.Y - Y;
        double dz = vector.Z - Z;
        return dx * dx + dy * dy + dz * dz;
    }

    public double DistanceToSquared(double x, double y, double z)
    {
        double dx = x - X;
        double dy = y - Y;
        double dz = z - Z;
        return dx * dx + dy * dy + dz * dz;
    }

    public bool CloserThan(Vec3 vector, double distanceXZ, double distanceY)
    {
        ArgumentNullException.ThrowIfNull(vector);
        double dx = vector.X - X;
        double dy = vector.Y - Y;
        double dz = vector.Z - Z;
        return Mth.LengthSquared(dx, dz) < Mth.Square(distanceXZ) &&
            Math.Abs(dy) < distanceY;
    }

    public Vec3 Scale(double scale) => Multiply(scale, scale, scale);

    public Vec3 Reverse() => Scale(-1.0);

    public Vec3 Multiply(Vec3 scale)
    {
        ArgumentNullException.ThrowIfNull(scale);
        return Multiply(scale.X, scale.Y, scale.Z);
    }

    public Vec3 Multiply(double xScale, double yScale, double zScale) =>
        new(X * xScale, Y * yScale, Z * zScale);

    public Vec3 Horizontal() => new(X, 0.0, Z);

    public double Length() => Math.Sqrt(X * X + Y * Y + Z * Z);

    public double LengthSquared() => X * X + Y * Y + Z * Z;

    public double HorizontalDistance() => Math.Sqrt(X * X + Z * Z);

    public double HorizontalDistanceSquared() => X * X + Z * Z;

    public Vec3 Lerp(Vec3 vector, double alpha)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return new Vec3(
            Mth.Lerp(alpha, X, vector.X),
            Mth.Lerp(alpha, Y, vector.Y),
            Mth.Lerp(alpha, Z, vector.Z));
    }

    public Vec3 XRot(float radians)
    {
        float cos = Mth.Cos(radians);
        float sin = Mth.Sin(radians);
        double x = X;
        double y = Y * cos + Z * sin;
        double z = Z * cos - Y * sin;
        return new Vec3(x, y, z);
    }

    public Vec3 YRot(float radians)
    {
        float cos = Mth.Cos(radians);
        float sin = Mth.Sin(radians);
        double x = X * cos + Z * sin;
        double y = Y;
        double z = Z * cos - X * sin;
        return new Vec3(x, y, z);
    }

    public Vec3 ZRot(float radians)
    {
        float cos = Mth.Cos(radians);
        float sin = Mth.Sin(radians);
        double x = X * cos + Y * sin;
        double y = Y * cos - X * sin;
        double z = Z;
        return new Vec3(x, y, z);
    }

    public Vec3 RotateClockwise90() => new(-Z, Y, X);

    public static Vec3 DirectionFromRotation(float rotX, float rotY)
    {
        float yCos = Mth.Cos(-rotY * (float)(Math.PI / 180.0) - (float)Math.PI);
        float ySin = Mth.Sin(-rotY * (float)(Math.PI / 180.0) - (float)Math.PI);
        float xCos = -Mth.Cos(-rotX * (float)(Math.PI / 180.0));
        float xSin = Mth.Sin(-rotX * (float)(Math.PI / 180.0));
        return new Vec3(ySin * xCos, xSin, yCos * xCos);
    }

    public Vec3 Align(IReadOnlySet<Direction.Axis> axes)
    {
        ArgumentNullException.ThrowIfNull(axes);
        double x = axes.Contains(Direction.Axis.X) ? Mth.Floor(X) : X;
        double y = axes.Contains(Direction.Axis.Y) ? Mth.Floor(Y) : Y;
        double z = axes.Contains(Direction.Axis.Z) ? Mth.Floor(Z) : Z;
        return new Vec3(x, y, z);
    }

    public double Get(Direction.Axis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        return axis.Choose(X, Y, Z);
    }

    public Vec3 With(Direction.Axis axis, double value)
    {
        ArgumentNullException.ThrowIfNull(axis);
        double x = ReferenceEquals(axis, Direction.Axis.X) ? value : X;
        double y = ReferenceEquals(axis, Direction.Axis.Y) ? value : Y;
        double z = ReferenceEquals(axis, Direction.Axis.Z) ? value : Z;
        return new Vec3(x, y, z);
    }

    public Vec3 Relative(Direction direction, double distance)
    {
        ArgumentNullException.ThrowIfNull(direction);
        Vec3i normal = direction.GetUnitVec3i();
        return new Vec3(
            X + distance * normal.GetX(),
            Y + distance * normal.GetY(),
            Z + distance * normal.GetZ());
    }

    public Vec3 ProjectedOn(Vec3 onto)
    {
        ArgumentNullException.ThrowIfNull(onto);
        double lengthSquared = onto.LengthSquared();
        return lengthSquared == 0.0
            ? onto
            : onto.Scale(Dot(onto)).Scale(1.0 / lengthSquared);
    }

    public bool IsFinite() =>
        double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);

    public bool Equals(Vec3? other) =>
        other is not null &&
        JavaDoubleCompare(X, other.X) == 0 &&
        JavaDoubleCompare(Y, other.Y) == 0 &&
        JavaDoubleCompare(Z, other.Z) == 0;

    public override bool Equals(object? obj) =>
        ReferenceEquals(this, obj) || obj is Vec3 other && Equals(other);

    public override int GetHashCode()
    {
        long bits = DoubleToLongBits(X);
        int result = unchecked((int)(bits ^ (long)((ulong)bits >> 32)));
        bits = DoubleToLongBits(Y);
        result = unchecked(31 * result + (int)(bits ^ (long)((ulong)bits >> 32)));
        bits = DoubleToLongBits(Z);
        return unchecked(31 * result + (int)(bits ^ (long)((ulong)bits >> 32)));
    }

    public override string ToString() =>
        "(" + JavaDoubleToString(X) + ", " + JavaDoubleToString(Y) + ", " + JavaDoubleToString(Z) + ")";

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

    private static string JavaDoubleToString(double value)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsPositiveInfinity(value)) return "Infinity";
        if (double.IsNegativeInfinity(value)) return "-Infinity";
        if (value == 0.0)
            return BitConverter.DoubleToInt64Bits(value) < 0 ? "-0.0" : "0.0";

        string raw = Math.Abs(value).ToString("R", CultureInfo.InvariantCulture);
        bool negative = value < 0.0;
        int exponentIndex = raw.IndexOfAny(new[] { 'E', 'e' });
        string mantissa = exponentIndex >= 0 ? raw[..exponentIndex] : raw;
        int explicitExponent = exponentIndex >= 0
            ? int.Parse(raw[(exponentIndex + 1)..], CultureInfo.InvariantCulture)
            : 0;

        int decimalPoint = mantissa.IndexOf('.');
        int decimalPosition = (decimalPoint >= 0 ? decimalPoint : mantissa.Length) + explicitExponent;
        string digits = mantissa.Replace(".", string.Empty, StringComparison.Ordinal);
        int leadingZeros = 0;
        while (leadingZeros < digits.Length - 1 && digits[leadingZeros] == '0')
            leadingZeros++;

        digits = digits[leadingZeros..];
        decimalPosition -= leadingZeros;

        string result;
        double absolute = Math.Abs(value);
        if (absolute >= 1.0E-3 && absolute < 1.0E7)
        {
            if (decimalPosition <= 0)
                result = "0." + new string('0', -decimalPosition) + digits;
            else if (decimalPosition >= digits.Length)
                result = digits + new string('0', decimalPosition - digits.Length) + ".0";
            else
                result = digits[..decimalPosition] + "." + digits[decimalPosition..];
        }
        else
        {
            string significantDigits = digits.TrimEnd('0');
            if (significantDigits.Length == 0)
                significantDigits = "0";
            string fraction = significantDigits.Length > 1 ? significantDigits[1..] : "0";
            result = significantDigits[0] + "." + fraction + "E" + (decimalPosition - 1).ToString(CultureInfo.InvariantCulture);
        }

        return negative ? "-" + result : result;
    }
}
