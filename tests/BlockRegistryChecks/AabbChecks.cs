using System.Numerics;
using Asteria.Core;

internal static class AabbChecks
{
    public static void Run()
    {
        CheckConstructionAndBounds();
        CheckDirectionalGrowth();
        CheckCombinationAndMovement();
        CheckIntersectionsAndContainment();
        CheckClippingAndCollision();
        CheckDistancesAndMeasurements();
        CheckNaNEqualityHashAndFormatting();
        CheckBuilder();
    }

    private static void CheckConstructionAndBounds()
    {
        var reversed = new AABB(5.0, 6.0, 7.0, -1.0, -2.0, -3.0);
        Require(reversed.MinX == -1.0 && reversed.MinY == -2.0 && reversed.MinZ == -3.0,
            "AABB construction must normalize every minimum coordinate.");
        Require(reversed.MaxX == 5.0 && reversed.MaxY == 6.0 && reversed.MaxZ == 7.0,
            "AABB construction must normalize every maximum coordinate.");

        var blockBox = new AABB(new BlockPos(-2, 4, 6));
        Require(blockBox.Equals(new AABB(-2, 4, 6, -1, 5, 7)),
            "Block-position construction must cover exactly one block.");

        var vectorBox = new AABB(new Vec3(2, 3, 4), new Vec3(-1, -2, -3));
        Require(vectorBox.Equals(new AABB(-1, -2, -3, 2, 3, 4)),
            "Vec3 endpoint construction must normalize both corners.");

        Require(
            AABB.UnitCubeFromLowerCorner(new Vec3(-1.5, 2.0, 3.0)).Equals(new AABB(-1.5, 2.0, 3.0, -0.5, 3.0, 4.0)),
            "Unit-cube factory must extend one unit from the lower corner.");

        Require(
            AABB.EncapsulatingFullBlocks(new BlockPos(-1, 2, 3), new BlockPos(4, -2, 1))
                .Equals(new AABB(-1, -2, 1, 5, 3, 4)),
            "Full-block encapsulation must include both endpoint blocks.");

        Require(reversed.SetMinX(-4).Equals(new AABB(-4, -2, -3, 5, 6, 7)),
            "SetMinX must replace only the requested bound.");
        Require(reversed.SetMinY(-5).MinY == -5 && reversed.SetMinY(-5).MaxY == 6,
            "SetMinY must preserve all other bounds.");
        Require(reversed.SetMinZ(-6).MinZ == -6, "SetMinZ must replace the lower Z bound.");
        Require(reversed.SetMaxX(8).MaxX == 8, "SetMaxX must replace the upper X bound.");
        Require(reversed.SetMaxY(9).MaxY == 9, "SetMaxY must replace the upper Y bound.");
        Require(reversed.SetMaxZ(10).MaxZ == 10, "SetMaxZ must replace the upper Z bound.");

        Require(reversed.Min(Direction.Axis.X) == -1 && reversed.Max(Direction.Axis.X) == 5,
            "Axis lookup must return the matching X limits.");
        Require(reversed.Min(Direction.Axis.Y) == -2 && reversed.Max(Direction.Axis.Y) == 6,
            "Axis lookup must return the matching Y limits.");
        Require(reversed.Min(Direction.Axis.Z) == -3 && reversed.Max(Direction.Axis.Z) == 7,
            "Axis lookup must return the matching Z limits.");
    }

    private static void CheckDirectionalGrowth()
    {
        var box = new AABB(0, 0, 0, 1, 1, 1);
        Require(
            box.Contract(-0.25, 0.5, 0).Equals(new AABB(0.25, 0, 0, 1, 0.5, 1)),
            "Contract must move only the face opposite the sign of each contraction component.");
        Require(
            box.ExpandTowards(-2, 3, -4).Equals(new AABB(-2, 0, -4, 1, 4, 1)),
            "ExpandTowards must extend only the face selected by each delta sign.");
        Require(
            box.Inflate(0.5).Equals(new AABB(-0.5, -0.5, -0.5, 1.5, 1.5, 1.5)),
            "Inflate must expand all six faces symmetrically.");
        Require(
            box.Inflate(0.25, 0.5, 0.75).Equals(new AABB(-0.25, -0.5, -0.75, 1.25, 1.5, 1.75)),
            "Per-axis inflate must use the supplied amount on each axis.");
        Require(
            box.Deflate(0.25).Equals(new AABB(0.25, 0.25, 0.25, 0.75, 0.75, 0.75)),
            "Deflate must delegate to negative inflation.");
        Require(
            box.Deflate(0.25, 0.5, 0.75).Equals(new AABB(0.25, 0.5, 0.75, 0.75, 0.5, 0.25)),
            "Per-axis deflate must preserve constructor bound normalization after inversion.");
    }

    private static void CheckCombinationAndMovement()
    {
        var first = new AABB(0, 0, 0, 2, 2, 2);
        var second = new AABB(1, -1, 0.5, 3, 1, 4);
        Require(
            first.Intersect(second).Equals(new AABB(1, 0, 0.5, 2, 1, 2)),
            "Intersect must combine component-wise maxima of minima and minima of maxima.");
        Require(
            first.MinMax(second).Equals(new AABB(0, -1, 0, 3, 2, 4)),
            "MinMax must construct the enclosing union bounds.");

        Require(first.Move(2, -3, 4).Equals(new AABB(2, -3, 4, 4, -1, 6)),
            "Move by components must translate both corners.");
        Require(first.Move(new BlockPos(1, 2, 3)).Equals(new AABB(1, 2, 3, 3, 4, 5)),
            "Move by BlockPos must use integer coordinates.");
        Require(first.Move(new Vec3(0.5, 1.5, 2.5)).Equals(new AABB(0.5, 1.5, 2.5, 2.5, 3.5, 4.5)),
            "Move by Vec3 must use real coordinates.");
        Require(first.Move(new Vector3(1, 2, 3)).Equals(new AABB(1, 2, 3, 3, 4, 5)),
            "Move by float vector must preserve its float components.");
    }

    private static void CheckIntersectionsAndContainment()
    {
        var box = new AABB(0, 0, 0, 1, 1, 1);
        Require(box.Intersects(new AABB(0.5, 0.5, 0.5, 2, 2, 2)),
            "Strict overlap must be detected.");
        Require(!box.Intersects(new AABB(1, 0, 0, 2, 1, 1)),
            "Boxes that only touch at a face must not intersect.");
        Require(!box.Intersects(new AABB(1, 1, 1, 2, 2, 2)),
            "Boxes that only touch at a corner must not intersect.");
        Require(box.Intersects(-1, 0.25, 0.25, 0.5, 0.75, 0.75),
            "Coordinate intersection must use strict overlap on all three axes.");
        Require(box.Intersects(new Vec3(0.75, 0.75, 0.75), new Vec3(-1, -1, -1)),
            "Vec3 intersection must normalize reversed endpoints.");
        Require(box.Intersects(new BlockPos(0, 0, 0)),
            "Block intersection must compare against the full unit block.");
        Require(!box.Intersects(new BlockPos(1, 0, 0)),
            "A block touching only at a face must not intersect.");

        Require(box.Contains(0, 0, 0), "AABB contains must include the minimum corner.");
        Require(box.Contains(0.5, 0.5, 0.5), "AABB contains must include interior coordinates.");
        Require(!box.Contains(1, 0.5, 0.5), "AABB contains must exclude the maximum X face.");
        Require(!box.Contains(0.5, 1, 0.5), "AABB contains must exclude the maximum Y face.");
        Require(!box.Contains(0.5, 0.5, 1), "AABB contains must exclude the maximum Z face.");
        Require(box.Contains(new Vec3(0.25, 0.25, 0.25)), "Vec3 containment must delegate to coordinate containment.");
    }

    private static void CheckClippingAndCollision()
    {
        var box = new AABB(0, 0, 0, 1, 1, 1);
        Vec3? hit = box.Clip(new Vec3(-1, 0.5, 0.5), new Vec3(2, 0.5, 0.5));
        Require(hit is not null && hit.Equals(new Vec3(0, 0.5, 0.5)),
            "Segment clipping must return the nearest hit on the west face.");

        Vec3? verticalHit = box.Clip(new Vec3(0.5, -2, 0.5), new Vec3(0.5, 2, 0.5));
        Require(verticalHit is not null && verticalHit.Equals(new Vec3(0.5, 0, 0.5)),
            "Segment clipping must test the vertical faces after the X faces.");

        Require(box.Clip(new Vec3(-1, 2, 0.5), new Vec3(2, 2, 0.5)) is null,
            "A segment outside the other two face ranges must not clip.");
        Require(box.Clip(new Vec3(-1, 0.5, 0.5), new Vec3(0, 0.5, 0.5)) is null,
            "A hit exactly at the segment endpoint must be excluded by the strict scale comparison.");

        Require(
            box.CollidedAlongVector(new Vec3(2, 0, 0), new[] { new AABB(2, 0, 0, 3, 1, 1) }),
            "CollidedAlongVector must detect a segment crossing an inflated shape part.");
        Require(
            box.CollidedAlongVector(Vec3.ZERO, new[] { new AABB(0.5, 0, 0, 2, 1, 1) }),
            "CollidedAlongVector must detect when an endpoint is contained in an inflated shape part.");
        Require(
            !box.CollidedAlongVector(new Vec3(0, 3, 0), new[] { new AABB(2, 0, 0, 3, 1, 1) }),
            "CollidedAlongVector must reject non-intersecting paths.");
    }

    private static void CheckDistancesAndMeasurements()
    {
        var box = new AABB(0, 0, 0, 2, 4, 6);
        Require(box.GetXSize() == 2 && box.GetYSize() == 4 && box.GetZSize() == 6,
            "Axis sizes must be upper bound minus lower bound.");
        Require(box.GetSize() == 4.0, "GetSize must average the three axis sizes.");
        Require(box.DistanceToSqr(new Vec3(1, 2, 3)) == 0,
            "Distance from any contained point must be zero.");
        Require(box.DistanceToSqr(new Vec3(3, 2, 3)) == 1,
            "Point distance must measure only the outside-axis deltas.");
        Require(box.DistanceToSqr(new AABB(3, 5, 8, 4, 6, 9)) == 1 + 1 + 4,
            "Box distance must sum the squared gaps on each axis.");
        Require(box.DistanceToSqr(new AABB(1, 1, 1, 3, 3, 3)) == 0,
            "Overlapping boxes must have zero distance.");

        Require(box.GetCenter().Equals(new Vec3(1, 2, 3)), "Center must use component-wise 0.5 lerp.");
        Require(box.GetBottomCenter().Equals(new Vec3(1, 0, 3)),
            "Bottom center must preserve the minimum Y coordinate.");
        Require(box.GetMinPosition().Equals(new Vec3(0, 0, 0)), "Minimum position must expose the lower corner.");
        Require(box.GetMaxPosition().Equals(new Vec3(2, 4, 6)), "Maximum position must expose the upper corner.");

        Require(
            AABB.OfSize(new Vec3(1, 2, 3), 2, 4, 6).Equals(box),
            "OfSize must treat its vector as the box center.");
        AABB deflated = box.NextDeflated();
        Require(
            deflated.MinX == Math.BitIncrement(box.MinX) &&
            deflated.MinY == Math.BitIncrement(box.MinY) &&
            deflated.MinZ == Math.BitIncrement(box.MinZ) &&
            deflated.MaxX == Math.BitDecrement(box.MaxX) &&
            deflated.MaxY == Math.BitDecrement(box.MaxY) &&
            deflated.MaxZ == Math.BitDecrement(box.MaxZ),
            "NextDeflated must move each face to the adjacent representable double.");
    }

    private static void CheckNaNEqualityHashAndFormatting()
    {
        var first = new AABB(0, 0, 0, 1, 1, 1);
        var second = new AABB(0, 0, 0, 1, 1, 1);
        Require(first.Equals(second), "AABB equality must compare all normalized coordinates.");
        Require(first.GetHashCode() == second.GetHashCode(), "Equal boxes must have equal hashes.");

        var positiveZero = new AABB(+0.0, 0, 0, 1, 1, 1);
        var negativeZero = new AABB(-0.0, 0, 0, 1, 1, 1);
        Require(!positiveZero.Equals(negativeZero),
            "Double.compare semantics must distinguish positive and negative zero.");

        var nanA = new AABB(double.NaN, 0, 0, 1, 1, 1);
        var nanB = new AABB(BitConverter.Int64BitsToDouble(unchecked((long)0x7ff0000000000001UL)), 0, 0, 1, 1, 1);
        Require(nanA.HasNaN() && nanB.HasNaN(), "NaN in any bound must be detected.");
        Require(nanA.Equals(nanB), "AABB equality must canonicalize NaN payloads.");
        Require(nanA.GetHashCode() == nanB.GetHashCode(), "AABB hashing must canonicalize NaN payloads.");

        Require(
            first.ToString() == "AABB[0.0, 0.0, 0.0] -> [1.0, 1.0, 1.0]",
            "AABB formatting must preserve Java double string conversion.");
        Require(
            new AABB(1.0E7, 0, 0, 1.0E7 + 1.0, 1, 1).ToString().StartsWith("AABB[1.0E7,"),
            "AABB formatting must preserve the Java scientific notation threshold.");
    }

    private static void CheckBuilder()
    {
        var builder = new AABB.Builder();
        Require(!builder.IsDefined(), "AABB builder must begin undefined.");
        RequireThrows<InvalidOperationException>(() => builder.Build(),
            "Building an undefined AABB must fail.");

        builder.Include(new Vector3(-2.5f, 1.0f, 3.25f));
        builder.Include(new Vector3(4.5f, -3.0f, 0.5f));
        Require(builder.IsDefined(), "Including a point must mark the builder defined.");
        Require(
            builder.Build().Equals(new AABB(-2.5, -3, 0.5, 4.5, 1, 3.25)),
            "Builder must accumulate float extrema and preserve the reference float-backed bounds.");
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
