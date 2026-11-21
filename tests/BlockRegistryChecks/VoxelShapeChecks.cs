using Asteria.Core;
using Asteria.Core.Shapes;

internal static class VoxelShapeChecks
{
    public static void Run()
    {
        CheckCubeAndArrayRepresentations();
        CheckShapeFactoriesAndGridQuantization();
        CheckBoundsMovementAndBoxEnumeration();
        CheckCoordinateAwareBounds();
        CheckBooleanJoinOperations();
        CheckJoinShortcutsAndNonEmptyQueries();
        CheckFaceShapesAndSlices();
        CheckContinuousEdgesAndCollision();
        CheckAxisCycleAndDiscreteAxisQueries();
    }

    private static void CheckCubeAndArrayRepresentations()
    {
        VoxelShape empty = Shapes.Empty();
        VoxelShape block = Shapes.Block();

        Require(empty.IsEmpty(), "Shapes.Empty must return an empty discrete grid.");
        Require(block is CubeVoxelShape && !block.IsEmpty(), "Shapes.Block must be a non-empty cube-backed shape.");
        Require(ReferenceEquals(Shapes.Block(), block), "The full block shape must be a canonical singleton.");
        Require(ReferenceEquals(Shapes.Empty(), empty), "The empty shape must be a canonical singleton.");

        Require(block.GetCoords(Direction.Axis.X).SequenceEqual(new[] { 0.0, 1.0 }),
            "Cube voxel shapes must expose evenly divided X coordinates.");
        Require(block.GetCoords(Direction.Axis.Y).SequenceEqual(new[] { 0.0, 1.0 }),
            "Cube voxel shapes must expose evenly divided Y coordinates.");
        Require(block.GetCoords(Direction.Axis.Z).SequenceEqual(new[] { 0.0, 1.0 }),
            "Cube voxel shapes must expose evenly divided Z coordinates.");

        var grid = new BitSetDiscreteVoxelShape(2, 3, 4);
        grid.Fill(1, 2, 3);
        var cube = new CubeVoxelShape(grid);
        Require(cube.GetCoords(Direction.Axis.X).SequenceEqual(new[] { 0.0, 0.5, 1.0 }),
            "CubeVoxelShape coordinates must use the discrete X subdivision count.");
        Require(cube.GetCoords(Direction.Axis.Y).SequenceEqual(new[] { 0.0, 1.0 / 3.0, 2.0 / 3.0, 1.0 }),
            "CubeVoxelShape coordinates must use the discrete Y subdivision count.");
        Require(cube.GetCoords(Direction.Axis.Z).SequenceEqual(new[] { 0.0, 0.25, 0.5, 0.75, 1.0 }),
            "CubeVoxelShape coordinates must use the discrete Z subdivision count.");

        var arrayShape = new ArrayVoxelShape(
            new BitSetDiscreteVoxelShape(1, 1, 1),
            new ArrayDoubleList(0.1, 0.9),
            new ArrayDoubleList(0.2, 0.8),
            new ArrayDoubleList(0.3, 0.7));
        Require(arrayShape is ArrayVoxelShape, "Explicit point grids must use ArrayVoxelShape.");
        Require(arrayShape.GetCoords(Direction.Axis.X).SequenceEqual(new[] { 0.1, 0.9 }),
            "ArrayVoxelShape must retain the exact supplied coordinate list.");
        Require(
            arrayShape.GetCoords(Direction.Axis.Y).SequenceEqual(new[] { 0.2, 0.8 }) &&
            arrayShape.GetCoords(Direction.Axis.Z).SequenceEqual(new[] { 0.3, 0.7 }),
            "ArrayVoxelShape must preserve Y and Z coordinate lists independently.");

        ArgumentException mismatch = Capture<ArgumentException>(
            () => _ = new ArrayVoxelShape(
                new BitSetDiscreteVoxelShape(1, 1, 1),
                new ArrayDoubleList(0.0),
                new ArrayDoubleList(0.0, 1.0),
                new ArrayDoubleList(0.0, 1.0)),
            "ArrayVoxelShape must reject coordinate-list lengths that disagree with grid dimensions.");
        Require(
            mismatch.Message == "Lengths of point arrays must be consistent with the size of the VoxelShape.",
            "ArrayVoxelShape must retain the source length-mismatch diagnostic.");

        var copiedArrays = new ArrayVoxelShape(
            new BitSetDiscreteVoxelShape(1, 1, 1),
            new[] { 0.25, 0.75, 1.0 },
            new[] { 0.0, 0.5 },
            new[] { 0.0, 1.0 });
        Require(
            copiedArrays.GetCoords(Direction.Axis.X).SequenceEqual(new[] { 0.25, 0.75 }),
            "Array-based constructor must copy only dimension-plus-one coordinates.");
        Require(
            copiedArrays.GetCoords(Direction.Axis.Y).SequenceEqual(new[] { 0.0, 0.5 }) &&
            copiedArrays.GetCoords(Direction.Axis.Z).SequenceEqual(new[] { 0.0, 1.0 }),
            "Array-based constructor must trim each axis independently.");
    }

    private static void CheckShapeFactoriesAndGridQuantization()
    {
        Require(ReferenceEquals(Shapes.Box(0, 0, 0, 1, 1, 1), Shapes.Block()),
            "The full unit cube must reuse the canonical block shape.");
        Require(ReferenceEquals(Shapes.Create(0, 0, 0, 1, 1, 1), Shapes.Block()),
            "The create factory must reuse the block shape for zero-bit bounds.");

        VoxelShape halfBlock = Shapes.Box(0, 0, 0, 0.5, 1, 1);
        Require(halfBlock is CubeVoxelShape, "Grid-aligned half-block bounds must use cube voxel storage.");
        Require(halfBlock.Shape.GetXSize() == 2 && halfBlock.Shape.GetYSize() == 1 && halfBlock.Shape.GetZSize() == 1,
            "Grid-aligned boxes must use the smallest exact per-axis subdivision.");
        Require(
            halfBlock.GetCoords(Direction.Axis.X).SequenceEqual(new[] { 0.0, 0.5, 1.0 }),
            "Half-block X coordinates must use two equal intervals.");

        VoxelShape quarter = Shapes.Box(0.25, 0, 0, 0.75, 1, 1);
        Require(quarter is CubeVoxelShape && quarter.Shape.GetXSize() == 4,
            "Quarter-grid bounds must use four X intervals.");

        VoxelShape unaligned = Shapes.Box(0.1, 0.2, 0.3, 0.9, 0.8, 0.7);
        Require(unaligned is ArrayVoxelShape,
            "Bounds that do not align to a one/two/four/eight subdivision must use explicit coordinate lists.");
        Require(
            unaligned.GetCoords(Direction.Axis.X).SequenceEqual(new[] { 0.1, 0.9 }) &&
            unaligned.GetCoords(Direction.Axis.Y).SequenceEqual(new[] { 0.2, 0.8 }) &&
            unaligned.GetCoords(Direction.Axis.Z).SequenceEqual(new[] { 0.3, 0.7 }),
            "Unaligned bounds must preserve their exact supplied coordinates.");

        Require(Shapes.FindBits(0, 1) == 0, "Unit bounds must require zero subdivision bits.");
        Require(Shapes.FindBits(0, 0.5) == 1, "Half-unit boundaries must require one subdivision bit.");
        Require(Shapes.FindBits(0.25, 0.75) == 2, "Quarter boundaries must require two subdivision bits.");
        Require(Shapes.FindBits(0.125, 0.875) == 3, "Eighth boundaries must require three subdivision bits.");
        Require(Shapes.FindBits(0.1, 0.9) == -1, "Non-grid boundaries must require array coordinates.");
        Require(Shapes.FindBits(-0.00000005, 1.00000005) == 0,
            "findBits must preserve its inclusive outer epsilon around the unit interval.");

        Require(ReferenceEquals(Shapes.Box(0, 0, 0, 0, 1, 1), Shapes.Empty()),
            "A box thinner than EPSILON on any axis must become empty.");
        Require(ReferenceEquals(Shapes.Create(0, 0, 0, 1, 0.00000005, 1), Shapes.Empty()),
            "The create factory must reject a dimension thinner than EPSILON.");
        RequireThrows<ArgumentException>(
            () => Shapes.Box(1, 0, 0, 0, 1, 1),
            "The box factory must reject a minimum greater than the maximum.");

        VoxelShape fromAabb = Shapes.Create(new AABB(0.25, 0, 0, 0.75, 1, 1));
        Require(fromAabb is CubeVoxelShape && fromAabb.GetCoords(Direction.Axis.X).SequenceEqual(new[] { 0.0, 0.25, 0.5, 0.75, 1.0 }),
            "AABB factory must delegate through exact discrete-grid selection.");

        Require(double.IsNegativeInfinity(Shapes.INFINITY.Min(Direction.Axis.X)) &&
                double.IsPositiveInfinity(Shapes.INFINITY.Max(Direction.Axis.X)),
            "INFINITY must retain unbounded coordinates on every axis.");
    }

    private static void CheckBoundsMovementAndBoxEnumeration()
    {
        VoxelShape block = Shapes.Block();
        Require(block.Min(Direction.Axis.X) == 0.0 && block.Max(Direction.Axis.X) == 1.0,
            "Cube bounds must derive from firstFull and lastFull.");
        Require(block.Min(Direction.Axis.Y) == 0.0 && block.Max(Direction.Axis.Y) == 1.0,
            "Y cube bounds must be unit length.");
        Require(block.Min(Direction.Axis.Z) == 0.0 && block.Max(Direction.Axis.Z) == 1.0,
            "Z cube bounds must be unit length.");
        Require(block.Bounds().Equals(new AABB(0, 0, 0, 1, 1, 1)),
            "Bounds must combine all three coordinate minima and maxima.");
        Require(block.SingleEncompassing().ToAabbs().SequenceEqual(new[] { new AABB(0, 0, 0, 1, 1, 1) }),
            "SingleEncompassing must return one box spanning the entire shape.");
        RequireThrows<NotSupportedException>(
            () => Shapes.Empty().Bounds(),
            "Bounds must reject an empty shape.");
        Require(Shapes.Empty().SingleEncompassing().IsEmpty(),
            "SingleEncompassing on an empty shape must return the canonical empty shape.");

        var boxes = new List<(double, double, double, double, double, double)>();
        block.ForAllBoxes((x1, y1, z1, x2, y2, z2) => boxes.Add((x1, y1, z1, x2, y2, z2)));
        Require(boxes.Count == 1 && boxes[0] == (0, 0, 0, 1, 1, 1),
            "ForAllBoxes must map discrete merged bounds back to continuous coordinates.");
        Require(block.ToAabbs().SequenceEqual(new[] { new AABB(0, 0, 0, 1, 1, 1) }),
            "ToAabbs must materialize each merged box in traversal order.");

        VoxelShape moved = block.Move(0.25, -0.5, 2.0);
        Require(moved is ArrayVoxelShape && !moved.IsEmpty(),
            "Moving a non-empty shape must use offset coordinate lists.");
        Require(moved.Bounds().Equals(new AABB(0.25, -0.5, 2, 1.25, 0.5, 3)),
            "Move must offset each coordinate list independently.");
        Require(ReferenceEquals(Shapes.Empty().Move(4, 5, 6), Shapes.Empty()),
            "Moving an empty shape must return the canonical empty shape.");
        Require(block.Move(new Vec3(1, 2, 3)).Bounds().Equals(new AABB(1, 2, 3, 2, 3, 4)),
            "Vec3 movement must delegate to the three-component translation.");
        Require(block.Move(new Vec3i(-1, 0, 2)).Bounds().Equals(new AABB(-1, 0, 2, 0, 1, 3)),
            "Vec3i movement must preserve integer translation semantics.");

        Vec3? closest = block.ClosestPointTo(new Vec3(2, 0.5, 0.5));
        Require(closest is not null && closest.Equals(new Vec3(1, 0.5, 0.5)),
            "ClosestPointTo must clamp the query to the nearest point on each merged box.");
        Require(block.ClosestPointTo(new Vec3(0.5, 0.5, 0.5))?.Equals(new Vec3(0.5, 0.5, 0.5)) == true,
            "ClosestPointTo must preserve a point inside the shape.");
        Require(Shapes.Empty().ClosestPointTo(Vec3.ZERO) is null,
            "ClosestPointTo on an empty shape must return no point.");

        VoxelShape optimized = moved.Optimize();
        Require(optimized.ToAabbs().SequenceEqual(new[] { new AABB(0.25, -0.5, 2, 1.25, 0.5, 3) }),
            "Optimize must rebuild the shape from its merged continuous boxes.");
    }

    private static void CheckCoordinateAwareBounds()
    {
        var cells = new BitSetDiscreteVoxelShape(2, 3, 4);
        cells.Fill(1, 2, 3);
        var shape = new CubeVoxelShape(cells);

        Require(shape.Min(Direction.Axis.X, 0.8, 0.8) == 0.5,
            "Axis-aware min must find the first occupied X cell at the selected Y/Z position.");
        Require(shape.Max(Direction.Axis.X, 0.8, 0.8) == 1.0,
            "Axis-aware max must return the selected occupied X cell's upper boundary.");
        Require(
            shape.Min(Direction.Axis.Y, 0.8, 0.8) == 2.0 / 3.0 &&
            shape.Max(Direction.Axis.Y, 0.8, 0.8) == 1.0,
            "Axis-aware Y bounds must permute the transverse Z/X coordinates correctly.");
        Require(
            shape.Min(Direction.Axis.Z, 0.8, 0.8) == 0.75 &&
            shape.Max(Direction.Axis.Z, 0.8, 0.8) == 1.0,
            "Axis-aware Z bounds must permute the transverse X/Y coordinates correctly.");

    }

    private static void CheckBooleanJoinOperations()
    {
        VoxelShape first = Shapes.Box(0, 0, 0, 0.75, 1, 1);
        VoxelShape second = Shapes.Box(0.25, 0, 0, 1, 1, 1);

        VoxelShape union = Shapes.JoinUnoptimized(first, second, BooleanOp.OR);
        Require(union is CubeVoxelShape && union.Shape.GetXSize() == 4,
            "JoinUnoptimized must choose cube storage when every axis merger is a discrete cube merger.");
        Require(
            union.ToAabbs().SequenceEqual(new[] { new AABB(0, 0, 0, 1, 1, 1) }),
            "OR must fill the union of both overlapping voxel shapes.");

        VoxelShape intersection = Shapes.JoinUnoptimized(first, second, BooleanOp.AND);
        Require(
            intersection.ToAabbs().SequenceEqual(new[] { new AABB(0.25, 0, 0, 0.75, 1, 1) }),
            "AND must retain only cells occupied in both shapes.");

        VoxelShape onlyFirst = Shapes.JoinUnoptimized(first, second, BooleanOp.ONLY_FIRST);
        Require(
            onlyFirst.ToAabbs().SequenceEqual(new[] { new AABB(0, 0, 0, 0.25, 1, 1) }),
            "ONLY_FIRST must subtract the second shape from the first.");

        VoxelShape onlySecond = Shapes.JoinUnoptimized(first, second, BooleanOp.ONLY_SECOND);
        Require(
            onlySecond.ToAabbs().SequenceEqual(new[] { new AABB(0.75, 0, 0, 1, 1, 1) }),
            "ONLY_SECOND must subtract the first shape from the second.");

        VoxelShape same = Shapes.JoinUnoptimized(first, first, BooleanOp.AND);
        Require(ReferenceEquals(same, first), "Joining a shape with itself must preserve its instance when the operation keeps true/true.");
        Require(
            ReferenceEquals(Shapes.JoinUnoptimized(first, first, BooleanOp.NOT_SAME), Shapes.Empty()),
            "Joining a shape with itself through XOR must return the canonical empty shape.");

        VoxelShape optimized = Shapes.Join(first, second, BooleanOp.OR);
        Require(ReferenceEquals(optimized, Shapes.Block()),
            "Optimized union of adjacent unit intervals must collapse to the canonical block shape.");

        VoxelShape separated = Shapes.JoinUnoptimized(
            Shapes.Box(0, 0, 0, 0.2, 1, 1),
            Shapes.Box(0.8, 0, 0, 1, 1, 1),
            BooleanOp.OR);
        Require(separated is ArrayVoxelShape,
            "A join using non-overlapping coordinate mergers must retain their explicit merged coordinate list.");
        Require(
            separated.ToAabbs().SequenceEqual(new[]
            {
                new AABB(0, 0, 0, 0.2, 1, 1),
                new AABB(0.8, 0, 0, 1, 1, 1)
            }),
            "Non-overlapping merger joins must preserve both separated boxes and their gap.");

        VoxelShape unalignedFirst = Shapes.Box(0.1, 0, 0, 0.6, 1, 1);
        VoxelShape unalignedSecond = Shapes.Box(0.4, 0, 0, 0.9, 1, 1);
        VoxelShape indirect = Shapes.JoinUnoptimized(unalignedFirst, unalignedSecond, BooleanOp.OR);
        Require(indirect is ArrayVoxelShape && indirect.ToAabbs().SequenceEqual(new[]
        {
            new AABB(0.1, 0, 0, 0.9, 1, 1)
        }), "Indirect coordinate mergers must join overlapping non-grid-aligned shapes.");
    }

    private static void CheckJoinShortcutsAndNonEmptyQueries()
    {
        VoxelShape first = Shapes.Box(0, 0, 0, 0.25, 1, 1);
        VoxelShape second = Shapes.Box(0.75, 0, 0, 1, 1, 1);

        Require(Shapes.JoinIsNotEmpty(first, second, BooleanOp.OR),
            "OR is non-empty for two disjoint non-empty shapes.");
        Require(!Shapes.JoinIsNotEmpty(first, second, BooleanOp.AND),
            "AND is empty for shapes separated on an axis.");
        Require(Shapes.JoinIsNotEmpty(first, second, BooleanOp.ONLY_FIRST),
            "ONLY_FIRST must remain non-empty for separated shapes.");
        Require(Shapes.JoinIsNotEmpty(first, second, BooleanOp.ONLY_SECOND),
            "ONLY_SECOND must remain non-empty for separated shapes.");
        Require(Shapes.JoinIsNotEmpty(first, second, BooleanOp.NOT_SAME),
            "XOR must be non-empty for two disjoint non-empty shapes.");

        Require(Shapes.JoinIsNotEmpty(Shapes.Empty(), first, BooleanOp.OR),
            "OR against an empty shape must return the non-empty operand occupancy.");
        Require(!Shapes.JoinIsNotEmpty(Shapes.Empty(), first, BooleanOp.AND),
            "AND against an empty shape must be empty.");
        Require(Shapes.JoinIsNotEmpty(Shapes.Empty(), first, BooleanOp.ONLY_SECOND),
            "ONLY_SECOND against an empty first shape must preserve the second.");
        Require(!Shapes.JoinIsNotEmpty(Shapes.Empty(), first, BooleanOp.ONLY_FIRST),
            "ONLY_FIRST against an empty first shape must remain empty.");

        Require(
            Shapes.JoinUnoptimized(Shapes.Empty(), first, BooleanOp.OR) == first,
            "Joining with an empty first shape must preserve the second instance when only it matters.");
        Require(
            Shapes.JoinUnoptimized(first, Shapes.Empty(), BooleanOp.ONLY_FIRST) == first,
            "Joining with an empty second shape must preserve the first instance when only it matters.");

        RequireThrows<ArgumentException>(
            () => Shapes.JoinUnoptimized(first, second, BooleanOp.NOT_OR),
            "Shape join must reject BooleanOps that return true for empty/empty.");
        RequireThrows<ArgumentException>(
            () => Shapes.JoinIsNotEmpty(first, second, BooleanOp.TRUE),
            "JoinIsNotEmpty must reject BooleanOps that return true for empty/empty.");
    }

    private static void CheckFaceShapesAndSlices()
    {
        VoxelShape block = Shapes.Block();
        VoxelShape empty = Shapes.Empty();

        Require(ReferenceEquals(block.GetFaceShape(Direction.NORTH), block),
            "The canonical block shape must return itself for every face.");
        Require(ReferenceEquals(empty.GetFaceShape(Direction.UP), empty),
            "The empty shape must return itself for every face.");

        VoxelShape halfHeight = Shapes.Box(0, 0, 0, 1, 0.5, 1);
        Require(
            ReferenceEquals(halfHeight.GetFaceShape(Direction.DOWN), Shapes.Block()),
            "The filled bottom slice of a half-height box must canonicalize to the block shape.");
        Require(
            ReferenceEquals(halfHeight.GetFaceShape(Direction.UP), Shapes.Empty()),
            "The unoccupied top slice of a half-height box must canonicalize to the empty shape.");

        VoxelShape edgeBox = Shapes.Box(0.0, 0.0, 0.25, 0.5, 1.0, 0.75);
        VoxelShape westFace = edgeBox.GetFaceShape(Direction.WEST);
        Require(!ReferenceEquals(westFace, edgeBox) && !westFace.IsEmpty(),
            "A non-cube boundary face must materialize a non-empty SliceShape.");
        Require(
            CoordinatesEqual(westFace.GetCoords(Direction.Axis.X), 0.0, 1.0) &&
            CoordinatesEqual(westFace.GetCoords(Direction.Axis.Y), 0.0, 1.0) &&
            CoordinatesEqual(westFace.GetCoords(Direction.Axis.Z), 0.0, 0.25, 0.5, 0.75, 1.0),
            "A slice must use unit coordinates along its normal and delegate the other coordinate lists.");
        Require(ReferenceEquals(edgeBox.GetFaceShape(Direction.WEST), westFace),
            "Face shapes must be cached per direction.");
        Require(westFace.ToAabbs().Count > 0,
            "Sliced face occupancy must remain usable by the existing box enumeration.");
    }

    private static bool CoordinatesEqual(DoubleList coordinates, params double[] expected)
    {
        if (coordinates.Count != expected.Length)
            return false;

        for (int index = 0; index < expected.Length; index++)
        {
            if (Math.Abs(coordinates.GetDouble(index) - expected[index]) > 1.0E-7)
                return false;
        }

        return true;
    }

    private static void CheckContinuousEdgesAndCollision()
    {
        VoxelShape block = Shapes.Block();

        var edges = new List<(double X1, double Y1, double Z1, double X2, double Y2, double Z2)>();
        block.ForAllEdges((x1, y1, z1, x2, y2, z2) =>
            edges.Add((x1, y1, z1, x2, y2, z2)));
        Require(edges.Count == 12, "A unit block shape must expose twelve merged continuous edges.");
        Require(
            edges.All(edge =>
                Math.Abs(edge.X2 - edge.X1) +
                Math.Abs(edge.Y2 - edge.Y1) +
                Math.Abs(edge.Z2 - edge.Z1) == 1.0),
            "Every unit-block continuous edge must have unit length.");

        Require(
            Math.Abs(block.Collide(Direction.Axis.X, new AABB(-2, 0.2, 0.2, -1, 0.8, 0.8), 5.0) - 1.0) < 1.0E-7,
            "Positive X movement must stop at the block's west boundary.");
        Require(
            Math.Abs(block.Collide(Direction.Axis.X, new AABB(2, 0.2, 0.2, 3, 0.8, 0.8), -5.0) + 1.0) < 1.0E-7,
            "Negative X movement must stop at the block's east boundary.");
        Require(
            Math.Abs(block.Collide(Direction.Axis.Y, new AABB(0.2, -2, 0.2, 0.8, -1, 0.8), 5.0) - 1.0) < 1.0E-7,
            "Positive Y movement must use the axis-cycle transform.");
        Require(
            Math.Abs(block.Collide(Direction.Axis.Z, new AABB(0.2, 0.2, 2, 0.8, 0.8, 3), -5.0) + 1.0) < 1.0E-7,
            "Negative Z movement must use the inverse axis-cycle transform.");

        Require(
            block.Collide(Direction.Axis.X, new AABB(-2, 2, 2, -1, 3, 3), 5.0) == 5.0,
            "Movement outside the shape's transverse bounds must remain unrestricted.");
        Require(
            Shapes.Empty().Collide(Direction.Axis.X, new AABB(-2, 0, 0, -1, 1, 1), 5.0) == 5.0,
            "An empty shape must not restrict movement.");
        Require(
            block.Collide(Direction.Axis.X, new AABB(-2, 0, 0, -1, 1, 1), 1.0E-8) == 0.0,
            "Movement smaller than the collision epsilon must resolve to zero.");
    }

    private static void CheckAxisCycleAndDiscreteAxisQueries()
    {
        Require(ReferenceEquals(AxisCycle.Forward.Cycle(Direction.Axis.X), Direction.Axis.Y),
            "Forward axis cycle must map X to Y.");
        Require(ReferenceEquals(AxisCycle.Forward.Cycle(Direction.Axis.Y), Direction.Axis.Z),
            "Forward axis cycle must map Y to Z.");
        Require(ReferenceEquals(AxisCycle.Forward.Cycle(Direction.Axis.Z), Direction.Axis.X),
            "Forward axis cycle must map Z to X.");
        Require(ReferenceEquals(AxisCycle.Backward.Cycle(Direction.Axis.X), Direction.Axis.Z),
            "Backward axis cycle must map X to Z.");
        Require(AxisCycle.Forward.Inverse() == AxisCycle.Backward,
            "Forward axis cycle inverse must be backward.");
        Require(AxisCycle.Forward.Cycle(1, 2, 3) == (3, 1, 2),
            "Forward axis cycling must select (z,x,y) for X/Y/Z respectively.");
        Require(AxisCycle.Backward.Cycle(1, 2, 3) == (2, 3, 1),
            "Backward axis cycling must select (y,z,x) for X/Y/Z respectively.");

        var shape = new BitSetDiscreteVoxelShape(2, 3, 4);
        shape.Fill(1, 2, 3);
        Require(shape.IsFullWide(AxisCycle.None, 1, 2, 3),
            "Identity axis-cycle occupancy must preserve coordinates.");
        Require(shape.IsFullWide(AxisCycle.Forward, 2, 3, 1),
            "Forward axis-cycle occupancy must remap coordinates in source order.");
        Require(shape.IsFullWide(AxisCycle.Backward, 3, 1, 2),
            "Backward axis-cycle occupancy must remap coordinates in source order.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static TException Capture<TException>(Action action, string message)
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

