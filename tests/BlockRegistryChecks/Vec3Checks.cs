using Asteria.Core;

internal static class Vec3Checks
{
    public static void Run()
    {
        CheckConstructionAndFactories();
        CheckArithmeticAndDistances();
        CheckNormalizationAndLengths();
        CheckRotationsAndDirection();
        CheckAxisOperationsAndProjection();
        CheckIdentityEqualityHashAndFormatting();
    }

    private static void CheckConstructionAndFactories()
    {
        var origin = new Vec3(1.0, 2.0, 3.0);
        Require(origin.X == 1.0 && origin.Y == 2.0 && origin.Z == 3.0, "Vec3 construction must retain exact double coordinates.");
        Require(origin.GetX() == 1.0 && origin.GetY() == 2.0 && origin.GetZ() == 3.0, "Vec3 must implement the real-valued Position accessors.");
        Require(ReferenceEquals(Vec3.ZERO, new Vec3(0.0, 0.0, 0.0).Normalize()), "Normalizing a zero vector must return the shared ZERO instance.");

        var blockPosition = new Vec3i(2, -3, 4);
        Require(Vec3.AtLowerCornerOf(blockPosition).Equals(new Vec3(2.0, -3.0, 4.0)), "Lower-corner factory must preserve integer coordinates.");
        Require(
            Vec3.AtLowerCornerWithOffset(blockPosition, 0.25, 0.5, 0.75).Equals(new Vec3(2.25, -2.5, 4.75)),
            "Offset lower-corner factory must add each component independently.");
        Require(Vec3.AtCenterOf(blockPosition).Equals(new Vec3(2.5, -2.5, 4.5)), "Center factory must offset all three axes by one half.");
        Require(Vec3.AtCenterOfWithY(blockPosition, 12.25).Equals(new Vec3(2.5, 12.25, 4.5)), "Center-with-Y factory must replace only the Y coordinate.");
        Require(Vec3.AtBottomCenterOf(blockPosition).Equals(new Vec3(2.5, -3.0, 4.5)), "Bottom-center factory must offset only X and Z.");
        Require(Vec3.UpFromBottomCenterOf(blockPosition, 1.75).Equals(new Vec3(2.5, -1.25, 4.5)), "Up-from-bottom-center factory must apply the supplied Y offset.");

        Require(Vec3.ZERO.Equals(new Vec3(0.0, 0.0, 0.0)), "ZERO must contain zero coordinates.");
        Require(Vec3.X_AXIS.Equals(new Vec3(1.0, 0.0, 0.0)), "X_AXIS must be the positive X unit vector.");
        Require(Vec3.Y_AXIS.Equals(new Vec3(0.0, 1.0, 0.0)), "Y_AXIS must be the positive Y unit vector.");
        Require(Vec3.Z_AXIS.Equals(new Vec3(0.0, 0.0, 1.0)), "Z_AXIS must be the positive Z unit vector.");
    }

    private static void CheckArithmeticAndDistances()
    {
        var origin = new Vec3(1.0, 2.0, 3.0);
        var other = new Vec3(4.0, 6.0, 8.0);
        Require(origin.VectorTo(other).Equals(new Vec3(3.0, 4.0, 5.0)), "VectorTo must return destination minus source.");
        Require(origin.Dot(other) == 41.0, "Dot product must sum component products.");
        Require(
            origin.Cross(other).Equals(new Vec3(-2.0, 4.0, -2.0)),
            "Cross product must preserve the right-handed component order.");
        Require(origin.Add(2.0).Equals(new Vec3(3.0, 4.0, 5.0)), "Scalar Add must affect every component.");
        Require(origin.Add(other).Equals(new Vec3(5.0, 8.0, 11.0)), "Vector Add must add corresponding components.");
        Require(origin.Add(1.0, -2.0, 3.0).Equals(new Vec3(2.0, 0.0, 6.0)), "Component Add must apply each supplied component.");
        Require(origin.Subtract(1.0).Equals(new Vec3(0.0, 1.0, 2.0)), "Scalar Subtract must affect every component.");
        Require(origin.Subtract(other).Equals(new Vec3(-3.0, -4.0, -5.0)), "Vector Subtract must subtract corresponding components.");
        Require(origin.Subtract(1.0, 0.5, 3.0).Equals(new Vec3(0.0, 1.5, 0.0)), "Component Subtract must apply each supplied component.");
        Require(origin.Scale(2.0).Equals(new Vec3(2.0, 4.0, 6.0)), "Scale must multiply all components.");
        Require(origin.Reverse().Equals(new Vec3(-1.0, -2.0, -3.0)), "Reverse must scale by negative one.");
        Require(origin.Multiply(new Vec3(2.0, 3.0, 4.0)).Equals(new Vec3(2.0, 6.0, 12.0)), "Vector Multiply must be component-wise.");
        Require(origin.Multiply(2.0, 3.0, 4.0).Equals(new Vec3(2.0, 6.0, 12.0)), "Component Multiply must be component-wise.");
        Require(origin.Horizontal().Equals(new Vec3(1.0, 0.0, 3.0)), "Horizontal must zero the Y coordinate.");

        Require(origin.DistanceTo(other) == Math.Sqrt(50.0), "DistanceTo must return Euclidean distance.");
        Require(origin.DistanceToSquared(other) == 50.0, "DistanceToSquared must return the sum of squared deltas.");
        Require(origin.DistanceToSquared(4.0, 6.0, 8.0) == 50.0, "Coordinate distance overload must match vector distance.");
        Require(origin.CloserThan(other, 9.0), "CloserThan(Position) must compare squared distance against the squared limit.");
        Require(!origin.CloserThan(other, Math.Sqrt(50.0)), "CloserThan(Position) must use a strict inequality at the boundary.");
        Require(origin.CloserThan(new Vec3(2.5, 2.0, 3.0), 2.0, 1.0), "Planar CloserThan must test horizontal distance and vertical delta independently.");
        Require(!origin.CloserThan(new Vec3(3.0, 2.0, 3.0), 2.0, 1.0), "Planar CloserThan must use a strict horizontal inequality.");
        Require(!origin.CloserThan(new Vec3(2.5, 3.0, 3.0), 2.0, 1.0), "Planar CloserThan must use a strict vertical inequality.");
        Require(origin.Lerp(other, 0.5).Equals(new Vec3(2.5, 4.0, 5.5)), "Lerp must delegate all components to Mth.Lerp.");
    }

    private static void CheckNormalizationAndLengths()
    {
        var vector = new Vec3(3.0, 4.0, 12.0);
        Require(vector.LengthSquared() == 169.0 && vector.Length() == 13.0, "Length and squared length must use all three coordinates.");
        Require(vector.HorizontalDistanceSquared() == 153.0 && vector.HorizontalDistance() == Math.Sqrt(153.0), "Horizontal distance must use X and Z only.");
        Require(vector.Normalize().Equals(new Vec3(3.0 / 13.0, 4.0 / 13.0, 12.0 / 13.0)), "Normalize must divide by Euclidean length.");
        Require(ReferenceEquals(new Vec3(0.000001, 0.0, 0.0).Normalize(), Vec3.ZERO), "Vectors shorter than the source epsilon must normalize to the shared ZERO instance.");
        Require(!new Vec3(double.PositiveInfinity, 0.0, 0.0).IsFinite(), "A vector containing infinity must not be finite.");
        Require(!new Vec3(0.0, double.NaN, 0.0).IsFinite(), "A vector containing NaN must not be finite.");
        Require(new Vec3(-1.0, 0.0, 2.0).IsFinite(), "Finite coordinates must pass the finite check.");

        Require(Mth.Lerp(0.25, 4.0, 12.0) == 6.0, "Mth.Lerp must preserve p0 + alpha * (p1 - p0).");
        Require(Mth.Square(-3.5) == 12.25, "Mth.Square must multiply the input by itself.");
        Require(Mth.LengthSquared(3.0, 4.0) == 25.0, "Two-dimensional length squared must sum both squares.");
        Require(Mth.LengthSquared(2.0, 3.0, 6.0) == 49.0, "Three-dimensional length squared must sum all squares.");
        Require(Mth.Length(3.0, 4.0) == 5.0 && Mth.Length(2.0, 3.0, 6.0) == 7.0, "Mth length overloads must square-root their squared counterparts.");
    }

    private static void CheckRotationsAndDirection()
    {
        const float halfPi = (float)(Math.PI / 2.0);
        Vec3 vector = new(0.0, 1.0, 0.0);
        Vec3 xRotated = vector.XRot(halfPi);
        Require(Math.Abs(xRotated.Y) < 0.00001 && Math.Abs(xRotated.Z + 1.0) < 0.00001, "X rotation must use the quantized Mth sine/cosine table.");
        Vec3 yRotated = Vec3.X_AXIS.YRot(halfPi);
        Require(Math.Abs(yRotated.X) < 0.00001 && Math.Abs(yRotated.Z + 1.0) < 0.00001, "Y rotation must rotate positive X toward negative Z.");
        Vec3 zRotated = Vec3.X_AXIS.ZRot(halfPi);
        Require(Math.Abs(zRotated.X) < 0.00001 && Math.Abs(zRotated.Y + 1.0) < 0.00001, "Z rotation must rotate positive X toward negative Y.");
        Require(new Vec3(1.0, 2.0, 3.0).RotateClockwise90().Equals(new Vec3(-3.0, 2.0, 1.0)), "Clockwise 90-degree rotation must map (-Z, Y, X).");

        Vec3 facing = Vec3.DirectionFromRotation(0.0f, 0.0f);
        Require(Math.Abs(facing.X) < 0.00001 && Math.Abs(facing.Y) < 0.00001 && Math.Abs(facing.Z - 1.0) < 0.00001, "Zero pitch/yaw must face positive Z.");
        Vec3 down = Vec3.DirectionFromRotation(90.0f, 0.0f);
        Require(Math.Abs(down.Y + 1.0) < 0.00001, "Positive 90-degree pitch must face down.");
        Require(ReferenceEquals(Direction.GetApproximateNearest(new Vec3(0.0, 0.0, 5.0)), Direction.SOUTH), "Direction nearest lookup must accept real-valued vectors.");
    }

    private static void CheckAxisOperationsAndProjection()
    {
        var vector = new Vec3(-1.2, 2.9, -3.1);
        Require(
            vector.Align(new HashSet<Direction.Axis> { Direction.Axis.X, Direction.Axis.Z }).Equals(new Vec3(-2.0, 2.9, -4.0)),
            "Align must floor only the requested axes, including negative coordinates.");
        Require(vector.Get(Direction.Axis.Y) == 2.9, "Get(axis) must select the matching component.");
        Require(vector.With(Direction.Axis.X, 7.5).Equals(new Vec3(7.5, 2.9, -3.1)), "With(axis) must replace only the requested component.");
        Require(vector.Relative(Direction.NORTH, 2.0).Equals(new Vec3(-1.2, 2.9, -5.1)), "Relative must offset by the direction's unit integer vector.");

        Vec3 projected = new Vec3(1.0, 2.0, 3.0).ProjectedOn(new Vec3(2.0, 0.0, 0.0));
        Require(projected.Equals(new Vec3(1.0, 0.0, 0.0)), "ProjectedOn must scale the target by dot/target-length-squared.");
        Vec3 zeroTarget = new Vec3(0.0, 0.0, 0.0);
        Require(ReferenceEquals(new Vec3(1.0, 2.0, 3.0).ProjectedOn(zeroTarget), zeroTarget), "Projection onto a zero vector must return the target instance unchanged.");

        IReadOnlyList<Direction.Axis> yzx = Direction.AxisStepOrder(new Vec3(1.0, 0.0, 2.0));
        IReadOnlyList<Direction.Axis> yxz = Direction.AxisStepOrder(new Vec3(2.0, 0.0, 2.0));
        Require(yzx.SequenceEqual(new[] { Direction.Axis.Y, Direction.Axis.Z, Direction.Axis.X }), "AxisStepOrder must choose YZX when |X| is less than |Z|.");
        Require(yxz.SequenceEqual(new[] { Direction.Axis.Y, Direction.Axis.X, Direction.Axis.Z }), "AxisStepOrder must choose YXZ on the equality boundary.");
    }

    private static void CheckIdentityEqualityHashAndFormatting()
    {
        Require(new Vec3(1.0, 2.0, 3.0).Equals(new Vec3(1.0, 2.0, 3.0)), "Equal coordinate vectors must compare equal.");
        Require(!new Vec3(+0.0, 0.0, 0.0).Equals(new Vec3(-0.0, 0.0, 0.0)), "Double comparison must distinguish positive and negative zero.");
        var nanA = new Vec3(double.NaN, 0.0, 0.0);
        var nanB = new Vec3(BitConverter.Int64BitsToDouble(unchecked((long)0x7ff0000000000001UL)), 0.0, 0.0);
        Require(nanA.Equals(nanB), "Double.compare semantics must canonicalize distinct NaN payloads.");
        Require(nanA.GetHashCode() == nanB.GetHashCode(), "Double hashing must canonicalize NaN payloads.");
        Require(new Vec3(1.0, 2.0, 3.0).GetHashCode() == new Vec3(1.0, 2.0, 3.0).GetHashCode(), "Equal vectors must have equal Java-compatible hashes.");
        Require(new Vec3(1.0, 2.0, 3.0).ToString() == "(1.0, 2.0, 3.0)", "Vec3 text must preserve Java's integral-double decimal suffix.");
        Require(new Vec3(1.0E7, 0.0, 0.0).ToString() == "(1.0E7, 0.0, 0.0)", "Vec3 text must use Java's exponent threshold and exponent format.");
        Require(new Vec3(1.23E7, 0.0, 0.0).ToString() == "(1.23E7, 0.0, 0.0)", "Scientific notation must trim insignificant integer zeroes while preserving the decimal mantissa.");
        Require(new Vec3(0.001, 0.0, 0.0).ToString() == "(0.001, 0.0, 0.0)", "Vec3 text must use plain notation at the lower Java exponent boundary.");
        Require(new Vec3(0.0001, 0.0, 0.0).ToString() == "(1.0E-4, 0.0, 0.0)", "Vec3 text must use exponent notation below the Java lower exponent boundary.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
