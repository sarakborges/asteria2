using System.Text.Json;
using Asteria.Core;
using Asteria.Serialization;

internal static class DirectionChecks
{
    public static void Run()
    {
        CheckDirectionCodecs();
        CheckAxisCodecs();
        CheckMathPrimitives();
        CheckCanonicalOrderAndIds();
        CheckAxesAndPlanes();
        CheckRotations();
        CheckNearestDirection();
    }

    private static void CheckDirectionCodecs()
    {
        DynamicOps<JsonElement> normalOps = JsonOps.INSTANCE;
        DynamicOps<JsonElement> compressedOps = JsonOps.COMPRESSED;
        Direction[] expected = { Direction.DOWN, Direction.UP, Direction.NORTH, Direction.SOUTH, Direction.WEST, Direction.EAST };

        for (int index = 0; index < expected.Length; index++)
        {
            Direction direction = expected[index];
            JsonElement encodedName = Direction.CODEC.EncodeStart(normalOps, direction).GetOrThrow();
            Require(encodedName.ValueKind == JsonValueKind.String &&
                    encodedName.GetString() == direction.GetSerializedName() &&
                    ReferenceEquals(Direction.CODEC.Parse(normalOps, encodedName).GetOrThrow(), direction),
                "Direction.CODEC must round-trip canonical names using uncompressed DynamicOps.");

            JsonElement encodedId = Direction.CODEC.EncodeStart(compressedOps, direction).GetOrThrow();
            Require(encodedId.ValueKind == JsonValueKind.Number &&
                    encodedId.GetInt32() == index &&
                    ReferenceEquals(Direction.CODEC.Parse(compressedOps, encodedId).GetOrThrow(), direction),
                "Direction.CODEC must round-trip declaration ordinals using compressed DynamicOps.");
        }

        DataResult<Direction> unknownName = Direction.CODEC.Parse(normalOps, normalOps.CreateString("north_east"));
        Require(unknownName.IsError && unknownName.ErrorMessage == "Unknown element: north_east",
            "Direction.CODEC must reject unknown canonical names.");
        DataResult<Direction> unknownId = Direction.CODEC.Parse(compressedOps, compressedOps.CreateInt(6));
        Require(unknownId.IsError && unknownId.ErrorMessage == "Unknown element id: 6",
            "Direction.CODEC must reject compressed ordinals outside the declaration range.");
        Require(Direction.CODEC.Parse(normalOps, normalOps.CreateInt(2)).IsError &&
                Direction.CODEC.Parse(compressedOps, compressedOps.CreateString("north")).IsError,
            "Direction.CODEC must select the normal or compressed representation from DynamicOps.");

        Require(ReferenceEquals(Direction.VERTICAL_CODEC.Parse(normalOps, normalOps.CreateString("up")).GetOrThrow(), Direction.UP) &&
                ReferenceEquals(Direction.VERTICAL_CODEC.Parse(normalOps, normalOps.CreateString("down")).GetOrThrow(), Direction.DOWN),
            "Direction.VERTICAL_CODEC must accept the two vertical directions.");
        DataResult<Direction> horizontal = Direction.VERTICAL_CODEC.Parse(normalOps, normalOps.CreateString("east"));
        Require(horizontal.IsError && horizontal.ErrorMessage == "Expected a vertical direction" &&
                !horizontal.HasResultOrPartial,
            "Direction.VERTICAL_CODEC must reject horizontal directions without a partial result.");
    }

    private static void CheckAxisCodecs()
    {
        DynamicOps<JsonElement> normalOps = JsonOps.INSTANCE;
        DynamicOps<JsonElement> compressedOps = JsonOps.COMPRESSED;
        Direction.Axis[] expected = { Direction.Axis.X, Direction.Axis.Y, Direction.Axis.Z };

        for (int index = 0; index < expected.Length; index++)
        {
            Direction.Axis axis = expected[index];
            JsonElement encodedName = Direction.Axis.CODEC.EncodeStart(normalOps, axis).GetOrThrow();
            Require(encodedName.ValueKind == JsonValueKind.String &&
                    encodedName.GetString() == axis.GetSerializedName() &&
                    ReferenceEquals(Direction.Axis.CODEC.Parse(normalOps, encodedName).GetOrThrow(), axis),
                "Direction.Axis.CODEC must round-trip canonical names.");

            JsonElement encodedId = Direction.Axis.CODEC.EncodeStart(compressedOps, axis).GetOrThrow();
            Require(encodedId.ValueKind == JsonValueKind.Number &&
                    encodedId.GetInt32() == index &&
                    ReferenceEquals(Direction.Axis.CODEC.Parse(compressedOps, encodedId).GetOrThrow(), axis),
                "Direction.Axis.CODEC must round-trip declaration-order IDs when maps are compressed.");
        }

        Require(Direction.Axis.CODEC.Parse(normalOps, normalOps.CreateString("w")).IsError &&
                Direction.Axis.CODEC.Parse(compressedOps, compressedOps.CreateInt(-1)).IsError,
            "Direction.Axis.CODEC must reject unknown names and IDs.");
    }

    private static void CheckMathPrimitives()
    {
        Require(Mth.Sin(0.0) == 0.0f, "The sine lookup must return zero at zero.");
        Require(Mth.Cos(0.0) == 1.0f, "The cosine lookup must return one at zero.");
        Require(Mth.Sin(Math.PI / 2.0) == 1.0f, "The sine lookup must preserve the quarter-turn table value.");
        Require(Mth.Cos(Math.PI) == -1.0f, "The cosine lookup must preserve the half-turn table value.");
        Require(Mth.Floor(1.9) == 1, "Floor must truncate positive fractions downward.");
        Require(Mth.Floor(-1.1) == -2, "Floor must round negative fractions toward negative infinity.");
        Require(Mth.Floor(double.NaN) == 0, "Java numeric conversion of a NaN floor result must yield zero.");
        Require(Mth.Floor(double.PositiveInfinity) == int.MaxValue, "Floor must saturate positive infinity at the maximum integer.");
        Require(Mth.Floor(double.NegativeInfinity) == int.MinValue, "Floor must saturate negative infinity at the minimum integer.");
    }

    private static void CheckCanonicalOrderAndIds()
    {
        Direction[] expected =
        {
            Direction.DOWN,
            Direction.UP,
            Direction.NORTH,
            Direction.SOUTH,
            Direction.WEST,
            Direction.EAST
        };

        Direction[] values = Direction.GetValues();
        Require(values.SequenceEqual(expected), "Direction values must preserve the six reference enum ordinals.");

        for (int index = 0; index < expected.Length; index++)
        {
            Direction direction = expected[index];
            Require(direction.Get3DDataValue() == index, "3D data values must match enum ordinals.");
            Require(ReferenceEquals(Direction.From3DDataValue(index), direction), "3D lookup must use the sorted 3D data table.");
            Require(ReferenceEquals(direction.GetOpposite().GetOpposite(), direction), "Opposite directions must form involutions.");
            Require(direction.GetUnitVec3i().GetX() == direction.GetStepX(), "Unit vector X must match the X step.");
            Require(direction.GetUnitVec3i().GetY() == direction.GetStepY(), "Unit vector Y must match the Y step.");
            Require(direction.GetUnitVec3i().GetZ() == direction.GetStepZ(), "Unit vector Z must match the Z step.");
            Require(
                direction.GetUnitVec3().Equals(new Vec3(direction.GetStepX(), direction.GetStepY(), direction.GetStepZ())),
                "Real-valued direction vectors must match their integer unit vectors.");

            Require(ReferenceEquals(Direction.ByName(direction.GetName()), direction), "Names must resolve to their canonical direction instances.");
        }

        Direction[] horizontalOrder =
        {
            Direction.SOUTH,
            Direction.WEST,
            Direction.NORTH,
            Direction.EAST
        };

        for (int index = 0; index < horizontalOrder.Length; index++)
        {
            Require(horizontalOrder[index].Get2DDataValue() == index, "Horizontal data values must use the reference SOUTH/WEST/NORTH/EAST order.");
            Require(ReferenceEquals(Direction.From2DDataValue(index), horizontalOrder[index]), "2D lookup must use the sorted horizontal table.");
        }

        Require(ReferenceEquals(Direction.From3DDataValue(-1), Direction.UP), "Negative 3D values must use absolute remainder indexing.");
        Require(ReferenceEquals(Direction.From3DDataValue(7), Direction.UP), "3D values must wrap by six.");
        Require(ReferenceEquals(Direction.From2DDataValue(-1), Direction.WEST), "Negative 2D values must use absolute remainder indexing.");
        Require(ReferenceEquals(Direction.From2DDataValue(5), Direction.WEST), "2D values must wrap by four.");
        Require(Direction.ByName(null) is null && Direction.ByName("invalid") is null, "Unknown direction names must return null.");

        Require(ReferenceEquals(Direction.FromYRot(0), Direction.SOUTH), "Zero yaw must face south.");
        Require(ReferenceEquals(Direction.FromYRot(90), Direction.WEST), "Positive 90-degree yaw must face west.");
        Require(ReferenceEquals(Direction.FromYRot(180), Direction.NORTH), "180-degree yaw must face north.");
        Require(ReferenceEquals(Direction.FromYRot(270), Direction.EAST), "270-degree yaw must face east.");
        Require(ReferenceEquals(Direction.FromYRot(-90), Direction.EAST), "Negative 90-degree yaw must face east.");
        Require(ReferenceEquals(Direction.FromYRot(45), Direction.WEST), "Yaw half-way between south and west must round toward west.");
        Require(ReferenceEquals(Direction.FromYRot(-45), Direction.SOUTH), "Yaw half-way between east and south must follow floor-after-half rounding.");

        Require(Direction.NORTH.ToYRot() == 180.0f, "North yaw must be 180 degrees.");
        Require(Direction.SOUTH.ToYRot() == 0.0f, "South yaw must be zero degrees.");
        Require(Direction.WEST.ToYRot() == 90.0f, "West yaw must be 90 degrees.");
        Require(Direction.EAST.ToYRot() == 270.0f, "East yaw must be 270 degrees.");
        Require(Direction.GetYRot(Direction.EAST) == -90.0f, "Direction.GetYRot must preserve its signed east yaw.");
        RequireThrows<InvalidOperationException>(() => Direction.GetYRot(Direction.UP), "Vertical directions must not have a Y rotation.");
    }

    private static void CheckAxesAndPlanes()
    {
        Direction.Axis[] axes = Direction.Axis.GetValues();
        Require(axes.SequenceEqual(new[] { Direction.Axis.X, Direction.Axis.Y, Direction.Axis.Z }), "Axes must preserve X/Y/Z enum order.");
        Require(ReferenceEquals(Direction.Axis.ByName("x"), Direction.Axis.X), "Axis names must be case-sensitive canonical lookups.");
        Require(Direction.Axis.ByName("X") is null, "Unknown axis names must return null.");
        Require(Direction.Axis.ByName(null) is null, "Null axis names must return null.");

        Require(ReferenceEquals(Direction.Axis.X.GetPositive(), Direction.EAST), "Positive X must be east.");
        Require(ReferenceEquals(Direction.Axis.X.GetNegative(), Direction.WEST), "Negative X must be west.");
        Require(ReferenceEquals(Direction.Axis.Y.GetPositive(), Direction.UP), "Positive Y must be up.");
        Require(ReferenceEquals(Direction.Axis.Y.GetNegative(), Direction.DOWN), "Negative Y must be down.");
        Require(ReferenceEquals(Direction.Axis.Z.GetPositive(), Direction.SOUTH), "Positive Z must be south.");
        Require(ReferenceEquals(Direction.Axis.Z.GetNegative(), Direction.NORTH), "Negative Z must be north.");

        foreach (Direction.Axis axis in axes)
        {
            Direction[] expected = { axis.GetPositive(), axis.GetNegative() };
            Require(axis.GetDirections().SequenceEqual(expected), "Axis direction arrays must be positive then negative.");
            Require(axis.IsVertical() == ReferenceEquals(axis, Direction.Axis.Y), "Only Y is vertical.");
            Require(axis.IsHorizontal() == !ReferenceEquals(axis, Direction.Axis.Y), "X and Z are horizontal.");
            Require(axis.Test(axis.GetPositive()) && axis.Test(axis.GetNegative()), "Axis predicate must accept both directions on its axis.");
            Require(!axis.Test(null), "Axis predicate must reject null.");
        }

        Require(Direction.Axis.X.Choose(1, 2, 3) == 1, "Axis X must select the X integer.");
        Require(Direction.Axis.Y.Choose(1.0, 2.0, 3.0) == 2.0, "Axis Y must select the Y double.");
        Require(Direction.Axis.Z.Choose(false, false, true), "Axis Z must select the Z boolean.");
        Require(Direction.AxisDirection.POSITIVE.Step == 1 && Direction.AxisDirection.NEGATIVE.Step == -1, "Axis direction steps must be signed.");
        Require(ReferenceEquals(Direction.AxisDirection.POSITIVE.Opposite(), Direction.AxisDirection.NEGATIVE), "Positive axis direction must oppose negative.");
        Require(ReferenceEquals(Direction.AxisDirection.NEGATIVE.Opposite(), Direction.AxisDirection.POSITIVE), "Negative axis direction must oppose positive.");

        Direction.Plane horizontal = Direction.Plane.HORIZONTAL;
        Direction.Plane vertical = Direction.Plane.VERTICAL;
        Require(horizontal.GetDirections().SequenceEqual(new[] { Direction.NORTH, Direction.EAST, Direction.SOUTH, Direction.WEST }), "Horizontal plane order must be north/east/south/west.");
        Require(vertical.GetDirections().SequenceEqual(new[] { Direction.UP, Direction.DOWN }), "Vertical plane order must be up/down.");
        Require(horizontal.GetAxes().SequenceEqual(new[] { Direction.Axis.X, Direction.Axis.Z }), "Horizontal plane axes must be X then Z.");
        Require(vertical.GetAxes().SequenceEqual(new[] { Direction.Axis.Y }), "Vertical plane axis must be Y.");
        Require(horizontal.Stream().SequenceEqual(horizontal.GetDirections()), "Plane stream must preserve the backing face order.");
        Require(horizontal.SequenceEqual(horizontal.GetDirections()), "Plane enumeration must preserve the backing face order.");
        Require(horizontal.Length == 4 && vertical.Length == 2, "Plane lengths must match their face counts.");
        Require(horizontal.Test(Direction.EAST) && !horizontal.Test(Direction.UP), "Horizontal plane predicate must accept only horizontal directions.");
        Require(vertical.Test(Direction.DOWN) && !vertical.Test(Direction.NORTH), "Vertical plane predicate must accept only vertical directions.");
        Require(!vertical.Test(null), "Plane predicate must reject null.");

        Require(ReferenceEquals(Direction.FromAxisAndDirection(Direction.Axis.X, Direction.AxisDirection.POSITIVE), Direction.EAST), "Axis/direction lookup must resolve positive X.");
        Require(ReferenceEquals(Direction.FromAxisAndDirection(Direction.Axis.Y, Direction.AxisDirection.NEGATIVE), Direction.DOWN), "Axis/direction lookup must resolve negative Y.");
        Require(ReferenceEquals(Direction.Get(Direction.AxisDirection.POSITIVE, Direction.Axis.X), Direction.EAST), "Direction.Get must resolve positive X.");
        Require(ReferenceEquals(Direction.Get(Direction.AxisDirection.NEGATIVE, Direction.Axis.X), Direction.WEST), "Direction.Get must resolve negative X.");
        Require(ReferenceEquals(Direction.Get(Direction.AxisDirection.POSITIVE, Direction.Axis.Y), Direction.UP), "Direction.Get must resolve positive Y.");
        Require(ReferenceEquals(Direction.Get(Direction.AxisDirection.NEGATIVE, Direction.Axis.Y), Direction.DOWN), "Direction.Get must resolve negative Y.");
        Require(ReferenceEquals(Direction.Get(Direction.AxisDirection.POSITIVE, Direction.Axis.Z), Direction.SOUTH), "Direction.Get must resolve positive Z.");
        Require(ReferenceEquals(Direction.Get(Direction.AxisDirection.NEGATIVE, Direction.Axis.Z), Direction.NORTH), "Direction.Get must resolve negative Z.");
        RequireThrows<ArgumentNullException>(
            () => Direction.FromAxisAndDirection(null!, Direction.AxisDirection.POSITIVE),
            "Axis/direction lookup must reject a null axis.");
        RequireThrows<ArgumentNullException>(
            () => Direction.Get(null!, Direction.Axis.Y),
            "Direction.Get must reject a null axis direction.");
    }

    private static void CheckRotations()
    {
        Require(ReferenceEquals(Direction.NORTH.GetClockWise(), Direction.EAST), "North clockwise must be east.");
        Require(ReferenceEquals(Direction.EAST.GetClockWise(), Direction.SOUTH), "East clockwise must be south.");
        Require(ReferenceEquals(Direction.SOUTH.GetClockWise(), Direction.WEST), "South clockwise must be west.");
        Require(ReferenceEquals(Direction.WEST.GetClockWise(), Direction.NORTH), "West clockwise must be north.");
        Require(ReferenceEquals(Direction.NORTH.GetCounterClockWise(), Direction.WEST), "North counter-clockwise must be west.");

        Require(ReferenceEquals(Direction.DOWN.GetClockWise(Direction.Axis.X), Direction.SOUTH), "X-axis clockwise rotation of down must be south.");
        Require(ReferenceEquals(Direction.UP.GetClockWise(Direction.Axis.X), Direction.NORTH), "X-axis clockwise rotation of up must be north.");
        Require(ReferenceEquals(Direction.NORTH.GetClockWise(Direction.Axis.X), Direction.DOWN), "X-axis clockwise rotation of north must be down.");
        Require(ReferenceEquals(Direction.SOUTH.GetCounterClockWise(Direction.Axis.X), Direction.DOWN), "X-axis counter-clockwise rotation of south must be down.");

        Require(ReferenceEquals(Direction.DOWN.GetClockWise(Direction.Axis.Z), Direction.WEST), "Z-axis clockwise rotation of down must be west.");
        Require(ReferenceEquals(Direction.UP.GetClockWise(Direction.Axis.Z), Direction.EAST), "Z-axis clockwise rotation of up must be east.");
        Require(ReferenceEquals(Direction.WEST.GetCounterClockWise(Direction.Axis.Z), Direction.DOWN), "Z-axis counter-clockwise rotation of west must be down.");

        Require(ReferenceEquals(Direction.UP.GetClockWise(Direction.Axis.Y), Direction.UP), "Rotation around a direction's own axis must preserve it.");
        Require(ReferenceEquals(Direction.NORTH.GetClockWise(Direction.Axis.Y), Direction.EAST), "Y-axis clockwise rotation must use horizontal clockwise mapping.");
        Require(ReferenceEquals(Direction.EAST.GetCounterClockWise(Direction.Axis.Y), Direction.NORTH), "Y-axis counter-clockwise rotation must use horizontal counter-clockwise mapping.");
        RequireThrows<InvalidOperationException>(() => Direction.UP.GetClockWise(), "Clockwise horizontal rotation must reject vertical directions.");
        RequireThrows<InvalidOperationException>(() => Direction.DOWN.GetCounterClockWise(), "Counter-clockwise horizontal rotation must reject vertical directions.");

        Require(Direction.SOUTH.IsFacingAngle(0.0f), "South must face yaw zero.");
        Require(Direction.WEST.IsFacingAngle(90.0f), "West must face yaw 90.");
        Require(Direction.NORTH.IsFacingAngle(180.0f), "North must face yaw 180.");
        Require(Direction.EAST.IsFacingAngle(270.0f), "East must face yaw 270.");
    }

    private static void CheckNearestDirection()
    {
        Require(ReferenceEquals(Direction.GetApproximateNearest(1.0f, 0.0f, 0.0f), Direction.EAST), "Approximate nearest must select positive X.");
        Require(ReferenceEquals(Direction.GetApproximateNearest(0.0f, -1.0f, 0.0f), Direction.DOWN), "Approximate nearest must select negative Y.");
        Require(ReferenceEquals(Direction.GetApproximateNearest(0.0, 0.0, 1.0), Direction.SOUTH), "The double overload must delegate through float precision.");
        Require(ReferenceEquals(Direction.GetApproximateNearest(0.0f, 0.0f, 0.0f), Direction.NORTH), "Zero dot products must retain the default north direction.");

        Require(ReferenceEquals(Direction.GetNearest(5, 1, 1, null), Direction.EAST), "Nearest direction must select a unique largest positive X.");
        Require(ReferenceEquals(Direction.GetNearest(-1, 5, 1, null), Direction.UP), "Nearest direction must select a unique largest positive Y.");
        Require(ReferenceEquals(Direction.GetNearest(1, 1, -5, null), Direction.NORTH), "Nearest direction must select a unique largest negative Z.");
        Require(ReferenceEquals(Direction.GetNearest(4, 4, 0, Direction.SOUTH), Direction.SOUTH), "Equal magnitudes must return the supplied fallback.");
        Require(ReferenceEquals(Direction.GetNearest(new Vec3i(0, -8, 0), Direction.NORTH), Direction.DOWN), "Vec3i nearest lookup must delegate to component lookup.");
        Require(ReferenceEquals(Direction.GetNearest(int.MinValue, 0, 0, Direction.WEST), Direction.WEST), "Integer absolute-value overflow must preserve Java semantics.");

        RequireThrows<ArgumentNullException>(
            () => Direction.GetNearest((Vec3i)null!, Direction.NORTH),
            "Nearest lookup must reject a null vector.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static TException RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(message);
    }
}
