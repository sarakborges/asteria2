using Asteria.Core;
using Asteria.World;
using Asteria.World.LevelGen;

internal static class CoordinatePositionChecks
{
    private readonly record struct TestPosition(double X, double Y, double Z) : Position
    {
        public double GetX() => X;
        public double GetY() => Y;
        public double GetZ() => Z;
    }

    public static void Run()
    {
        CheckVec3i();
        CheckBlockPos();
        CheckChunkPos();
        CheckSectionPos();
    }

    private static void CheckVec3i()
    {
        var value = new Vec3i(1, 2, 3);
        Require(value.GetHashCode() == 2946, "Vec3i must preserve the reference hash formula.");
        Require(value.ToShortString() == "1, 2, 3", "Vec3i short formatting must match the reference.");
        Require(value.Equals(new BlockPos(1, 2, 3)), "Vec3i equality must compare coordinates across derived types.");
        Require(value.CompareTo(new Vec3i(99, 3, 0)) < 0, "Vec3i comparison must prioritize Y.");
        Require(value.CompareTo(new Vec3i(99, 2, 4)) < 0, "Vec3i comparison must prioritize Z after Y.");
        Require(value.CompareTo(new Vec3i(2, 2, 3)) < 0, "Vec3i comparison must prioritize X after Y and Z.");
        Require(value.Offset(2, -3, 4).Equals(new Vec3i(3, -1, 7)), "Vec3i offset must add each axis.");
        Require(value.Subtract(new Vec3i(1, 1, 1)).Equals(new Vec3i(0, 1, 2)), "Vec3i subtraction must use component offsets.");
        Require(value.Multiply(2).Equals(new Vec3i(2, 4, 6)), "Vec3i scalar multiplication must match.");
        Require(value.Cross(new Vec3i(0, 1, 0)).Equals(new Vec3i(-3, 0, 1)), "Vec3i cross product must preserve axis order.");
        Require(value.DistSqr(new Vec3i(2, 4, 6)) == 14.0, "Vec3i squared distance must use the low corners.");
        Require(value.DistToCenterSqr(1.5, 2.5, 3.5) == 0.0, "Vec3i center-distance calculation must offset each axis by one half.");
        Require(value.DistManhattan(new Vec3i(4, 6, 8)) == 12, "Vec3i Manhattan distance must add component distances.");
        Require(value.DistChessboard(new Vec3i(4, 6, 8)) == 5, "Vec3i chessboard distance must take the largest axis distance.");
        Require(value.DiffersHorizontally(new Vec3i(1, 99, 3)) == false, "Vertical-only changes must not differ horizontally.");
        Require(value.Get(Vec3i.Axis.Z) == 3, "Vec3i axis selection must return the selected coordinate.");
    }

    private static void CheckBlockPos()
    {
        Require(BlockPos.PackedHorizontalLength == 26, "BlockPos horizontal packed width must be 26 bits.");
        Require(BlockPos.PackedYLength == 12, "BlockPos vertical packed width must be 12 bits.");
        Require(BlockPos.MaxHorizontalCoordinate == 33_554_431, "BlockPos horizontal coordinate limit must match the packed source layout.");

        long known = BlockPos.AsLong(1, 2, 3);
        Require(known == 274_877_919_234L, "BlockPos 26/12/26 packing must match the reference bit offsets.");
        Require(BlockPos.GetX(known) == 1 && BlockPos.GetY(known) == 2 && BlockPos.GetZ(known) == 3, "BlockPos packed accessors must decode the known vector.");
        Require(BlockPos.Of(known).Equals(new BlockPos(1, 2, 3)), "BlockPos packed round-trip must preserve coordinates.");
        Require(BlockPos.AsLong(-1, -1, -1) == -1L, "BlockPos all-negative-one coordinates must fill the packed long.");
        Require(BlockPos.Of(-1L).Equals(new BlockPos(-1, -1, -1)), "BlockPos signed fields must decode correctly.");

        var packedLimits = BlockPos.Of(BlockPos.AsLong(-33_554_432, -2_048, 33_554_431));
        Require(
            packedLimits.Equals(new BlockPos(-33_554_432, -2_048, 33_554_431)),
            "BlockPos must sign-extend all three packed coordinate fields.");
        Require(
            BlockPos.Of(BlockPos.AsLong(33_554_432, 2_048, -33_554_433))
                .Equals(new BlockPos(-33_554_432, -2_048, 33_554_431)),
            "BlockPos packing must retain the reference truncation behavior outside the packed coordinate widths.");

        Require(BlockPos.Containing(-0.01, 0.99, -16.0001).Equals(new BlockPos(-1, 0, -17)), "BlockPos containing must floor negative fractional coordinates.");
        Require(BlockPos.Containing(-1.0, -16.0, -17.0).Equals(new BlockPos(-1, -16, -17)), "BlockPos containing must preserve exact negative integers.");
        Require(
            BlockPos.Containing(new Vec3(-0.01, 0.99, -16.0001)).Equals(new BlockPos(-1, 0, -17)),
            "BlockPos containing(Position) must delegate to the same negative-coordinate floor semantics.");

        Require(BlockPos.Min(new BlockPos(4, -3, 8), new BlockPos(-2, 7, 1)).Equals(new BlockPos(-2, -3, 1)), "BlockPos min must select the minimum per coordinate.");
        Require(BlockPos.Max(new BlockPos(4, -3, 8), new BlockPos(-2, 7, 1)).Equals(new BlockPos(4, 7, 8)), "BlockPos max must select the maximum per coordinate.");

        var origin = new BlockPos(0, 0, 0);
        Require(ReferenceEquals(origin, origin.Offset(0, 0, 0)), "BlockPos zero offset must preserve the same instance.");
        Require(ReferenceEquals(origin, origin.Above(0)), "BlockPos zero directional steps must preserve the same instance.");
        Require(origin.Above(2).Equals(new BlockPos(0, 2, 0)), "BlockPos above must advance positive Y.");
        Require(origin.Below(2).Equals(new BlockPos(0, -2, 0)), "BlockPos below must advance negative Y.");
        Require(origin.North(3).Equals(new BlockPos(0, 0, -3)), "BlockPos north must advance negative Z.");
        Require(origin.South(3).Equals(new BlockPos(0, 0, 3)), "BlockPos south must advance positive Z.");
        Require(origin.West(3).Equals(new BlockPos(-3, 0, 0)), "BlockPos west must advance negative X.");
        Require(origin.East(3).Equals(new BlockPos(3, 0, 0)), "BlockPos east must advance positive X.");
        Require(origin.Relative(Vec3i.Axis.Y, 5).Equals(new BlockPos(0, 5, 0)), "BlockPos axis-relative offset must target one axis.");
        Require(new BlockPos(1, 2, 3).AtY(9).Equals(new BlockPos(1, 9, 3)), "BlockPos atY must replace only Y.");
        Require(new BlockPos(1, 2, 3).Cross(new Vec3i(0, 1, 0)).Equals(new BlockPos(-3, 0, 1)), "BlockPos cross product must return a block position.");
        Require(BlockPos.GetFlatIndex(known) == (known & -16L), "BlockPos flat-index masking must clear the low four bits.");
        Require(
            BlockPos.Offset(known, -1, 2, -3) == BlockPos.AsLong(0, 4, 0),
            "BlockPos packed offset must unpack, offset, and repack coordinates.");

        var mutable = new BlockPos.MutableBlockPos(1, 2, 3);
        BlockPos snapshot = mutable.Immutable();
        mutable.Move(-2, 3, -4).Clamp(Vec3i.Axis.X, -1, 1).SetY(-8);
        Require(snapshot.Equals(new BlockPos(1, 2, 3)), "MutableBlockPos immutable snapshots must not follow later mutations.");
        Require(mutable.Equals(new BlockPos(-1, -8, -1)), "MutableBlockPos move, clamp, and set must update its coordinates.");
        BlockPos offsetFromMutable = mutable.Offset(0, 0, 0);
        mutable.SetX(10);
        Require(offsetFromMutable.Equals(new BlockPos(-1, -8, -1)), "MutableBlockPos offset must return an immutable position.");

        BlockPos[] box = BlockPos.BetweenClosed(-1, 2, 4, 0, 2, 4)
            .Select(position => position.Immutable())
            .ToArray();
        Require(
            box.SequenceEqual(new[] { new BlockPos(-1, 2, 4), new BlockPos(0, 2, 4) }),
            "BlockPos closed-box traversal must advance X before Y and Z.");

        BlockPos[] bounds = BlockPos.BetweenClosed(
            new AABB(-0.1, 0.2, -1.2, 1.0, 1.1, 0.2))
            .Select(position => position.Immutable())
            .ToArray();
        Require(
            bounds.Length == 18 &&
            bounds[0].Equals(new BlockPos(-1, 0, -2)) &&
            bounds[1].Equals(new BlockPos(0, 0, -2)) &&
            bounds[3].Equals(new BlockPos(-1, 1, -2)) &&
            bounds[^1].Equals(new BlockPos(1, 1, 0)),
            "BlockPos closed AABB traversal must floor both inclusive corners and advance X, then Y, then Z.");

        BlockPos[] neighborColumn = BlockPos.NeighborColumn(4, 2, 9, 0)
            .Select(position => position.Immutable())
            .ToArray();
        Require(
            neighborColumn.SequenceEqual(new[]
            {
                new BlockPos(4, 2, 9), new BlockPos(4, 1, 9), new BlockPos(4, 0, 9),
                new BlockPos(4, 2, 8), new BlockPos(4, 1, 8), new BlockPos(4, 0, 8),
                new BlockPos(5, 2, 9), new BlockPos(5, 1, 9), new BlockPos(5, 0, 9),
                new BlockPos(4, 2, 10), new BlockPos(4, 1, 10), new BlockPos(4, 0, 10),
                new BlockPos(3, 2, 9), new BlockPos(3, 1, 9), new BlockPos(3, 0, 9)
            }),
            "BlockPos neighborColumn must visit the center, north, east, south and west columns in source order.");

        BlockPos[] manhattan = BlockPos.WithinManhattan(origin, 1)
            .Select(position => position.Immutable())
            .ToArray();
        Require(
            manhattan.SequenceEqual(new[]
            {
                new BlockPos(0, 0, 0), new BlockPos(-1, 0, 0),
                new BlockPos(0, -1, 0), new BlockPos(0, 0, 1),
                new BlockPos(0, 0, -1), new BlockPos(0, 1, 0),
                new BlockPos(1, 0, 0)
            }),
            "BlockPos Manhattan traversal must emit positions by increasing distance in the source axis order.");
        Require(
            BlockPos.WithinClippedManhattan(origin, 1, 0, 0)
                .Select(position => position.Immutable())
                .SequenceEqual(new[]
                {
                    new BlockPos(0, 0, 0), new BlockPos(-1, 0, 0), new BlockPos(1, 0, 0)
                }),
            "BlockPos clipped Manhattan traversal must respect each axis reach.");
        Require(
            BlockPos.WithinBoxByManhattanDistance(origin, 1, 2, 0).Count() == 15,
            "BlockPos box-by-Manhattan traversal must cover the complete inclusive rectangular reach.");
        using (IEnumerator<BlockPos> manhattanCursor = BlockPos.WithinManhattan(origin, 1).GetEnumerator())
        {
            Require(manhattanCursor.MoveNext(), "BlockPos Manhattan traversal must produce its origin.");
            BlockPos reusedManhattanCursor = manhattanCursor.Current;
            Require(
                manhattanCursor.MoveNext() && ReferenceEquals(reusedManhattanCursor, manhattanCursor.Current),
                "BlockPos Manhattan traversal must reuse its mutable cursor.");
        }

        BlockPos[] spiral = BlockPos.SpiralAround(
                origin, 1, Direction.EAST, Direction.NORTH)
            .Take(3)
            .Select(position => position.Immutable())
            .ToArray();
        Require(
            spiral.SequenceEqual(new[]
            {
                new BlockPos(0, 0, 0),
                new BlockPos(1, 0, 0),
                new BlockPos(1, 0, -1)
            }),
            "BlockPos spiral traversal must begin at the center and follow the supplied direction order.");
        using (IEnumerator<BlockPos.MutableBlockPos> spiralCursor =
            BlockPos.SpiralAround(origin, 1, Direction.EAST, Direction.NORTH).GetEnumerator())
        {
            Require(spiralCursor.MoveNext(), "BlockPos spiral traversal must produce its center.");
            BlockPos.MutableBlockPos reusedSpiralCursor = spiralCursor.Current;
            Require(
                spiralCursor.MoveNext() && ReferenceEquals(reusedSpiralCursor, spiralCursor.Current),
                "BlockPos spiral traversal must reuse its mutable cursor.");
        }
        Require(
            BlockPos.SpiralAround(origin, 0, Direction.EAST, Direction.NORTH).Count() == 1,
            "BlockPos spiral traversal with radius zero must yield only the center.");
        bool rejectedSameAxisSpiral = false;
        try
        {
            _ = BlockPos.SpiralAround(origin, 1, Direction.EAST, Direction.WEST);
        }
        catch (InvalidOperationException)
        {
            rejectedSameAxisSpiral = true;
        }
        Require(
            rejectedSameAxisSpiral,
            "BlockPos spiral traversal must reject directions on the same axis.");

        var graph = new Dictionary<long, BlockPos[]>
        {
            [BlockPos.AsLong(0, 0, 0)] = new[] { new BlockPos(1, 0, 0), new BlockPos(2, 0, 0) },
            [BlockPos.AsLong(1, 0, 0)] = new[] { new BlockPos(2, 0, 0), new BlockPos(4, 0, 0) },
            [BlockPos.AsLong(2, 0, 0)] = new[] { new BlockPos(3, 0, 0) }
        };
        var breadthFirstOrder = new List<BlockPos>();
        int breadthFirstCount = BlockPos.BreadthFirstTraversal(
            origin,
            5,
            20,
            (position, addNeighbor) =>
            {
                if (graph.TryGetValue(position.AsLong(), out BlockPos[]? neighbors))
                    foreach (BlockPos neighbor in neighbors)
                        addNeighbor(neighbor);
            },
            position =>
            {
                breadthFirstOrder.Add(position.Immutable());
                return BlockPos.TraversalNodeStatus.ACCEPT;
            });
        Require(
            breadthFirstCount == 5 &&
            breadthFirstOrder.SequenceEqual(new[]
            {
                new BlockPos(0, 0, 0), new BlockPos(1, 0, 0),
                new BlockPos(2, 0, 0), new BlockPos(4, 0, 0),
                new BlockPos(3, 0, 0)
            }),
            "BlockPos breadth-first traversal must process FIFO order and deduplicate packed positions.");

        Require(
            origin.Relative(Direction.NORTH, 2).Equals(new BlockPos(0, 0, -2)) &&
            origin.Relative(Direction.UP).Equals(new BlockPos(0, 1, 0)),
            "BlockPos direction-relative offsets must multiply the selected unit direction.");
        var directionalMutable = new BlockPos.MutableBlockPos(4, 5, 6);
        directionalMutable.SetWithOffset(origin, Direction.WEST).Move(Direction.UP, 3);
        Require(
            directionalMutable.Equals(new BlockPos(-1, 3, 0)),
            "MutableBlockPos directional offset and movement must update each selected axis.");

        Vec3 clampedLocation = new BlockPos(-2, 3, 5)
            .ClampLocationWithin(new Vec3(-100.0, -100.0, -100.0));
        Require(
            clampedLocation.X > -2.0 && clampedLocation.X < -1.9999 &&
            clampedLocation.Y > 3.0 && clampedLocation.Y < 3.0001 &&
            clampedLocation.Z > 5.0 && clampedLocation.Z < 5.0001,
            "BlockPos clampLocationWithin must keep each component inside the block with the source epsilon.");

        Vec3 clampedMaximum = new BlockPos(-2, 3, 5)
            .ClampLocationWithin(new Vec3(100.0, 100.0, 100.0));
        Require(
            clampedMaximum.X < -1.0 && clampedMaximum.X > -1.0001 &&
            clampedMaximum.Y < 4.0 && clampedMaximum.Y > 3.9999 &&
            clampedMaximum.Z < 6.0 && clampedMaximum.Z > 5.9999,
            "BlockPos clampLocationWithin must preserve the upper float-epsilon boundary on every axis.");

        var random = new WorldgenRandom(new LegacyRandomSource(123));
        BlockPos[] randomPositions = BlockPos.RandomBetweenClosed(
                random, 3, -2, 10, 3, 1, 17, 4)
            .Select(position => position.Immutable())
            .ToArray();
        Require(
            randomPositions.SequenceEqual(new[]
            {
                new BlockPos(0, 11, 4),
                new BlockPos(-1, 12, 4),
                new BlockPos(0, 12, 4)
            }) &&
            random.Count == 9,
            "BlockPos randomBetweenClosed must preserve the source coordinate draw order and random call count.");

        var emptyRandom = new WorldgenRandom(new LegacyRandomSource(123));
        Require(
            !BlockPos.RandomBetweenClosed(emptyRandom, 0, 0, 0, 0, 0, 0, 0).Any() &&
            emptyRandom.Count == 0,
            "BlockPos randomBetweenClosed with a non-positive limit must not consume random values.");

        var reusedRandom = new WorldgenRandom(new LegacyRandomSource(123));
        using (IEnumerator<BlockPos> randomCursor =
            BlockPos.RandomBetweenClosed(reusedRandom, 2, 0, 0, 0, 3, 3, 3).GetEnumerator())
        {
            Require(randomCursor.MoveNext(), "BlockPos randomBetweenClosed must produce a position when its limit is positive.");
            BlockPos reusedRandomCursor = randomCursor.Current;
            Require(
                randomCursor.MoveNext() && ReferenceEquals(reusedRandomCursor, randomCursor.Current),
                "BlockPos randomBetweenClosed must reuse its mutable cursor.");
        }

        breadthFirstOrder.Clear();
        int skipCount = BlockPos.BreadthFirstTraversal(
            origin,
            5,
            20,
            (position, addNeighbor) =>
            {
                if (graph.TryGetValue(position.AsLong(), out BlockPos[]? neighbors))
                    foreach (BlockPos neighbor in neighbors)
                        addNeighbor(neighbor);
            },
            position =>
            {
                breadthFirstOrder.Add(position.Immutable());
                return position.GetX() == 1
                    ? BlockPos.TraversalNodeStatus.SKIP
                    : BlockPos.TraversalNodeStatus.ACCEPT;
            });
        Require(
            skipCount == 3 &&
            breadthFirstOrder.SequenceEqual(new[]
            {
                new BlockPos(0, 0, 0), new BlockPos(1, 0, 0),
                new BlockPos(2, 0, 0), new BlockPos(3, 0, 0)
            }),
            "BlockPos breadth-first SKIP must count neither the node nor its outgoing neighbors.");

        int stopCount = BlockPos.BreadthFirstTraversal(
            origin,
            5,
            20,
            (position, addNeighbor) =>
            {
                if (position.GetX() == 0)
                    addNeighbor(new BlockPos(1, 0, 0));
                addNeighbor(new BlockPos(2, 0, 0));
            },
            position => position.GetX() == 1
                ? BlockPos.TraversalNodeStatus.STOP
                : BlockPos.TraversalNodeStatus.ACCEPT);
        Require(stopCount == 1, "BlockPos breadth-first STOP must terminate before counting the stop node.");
    }

    private static void CheckChunkPos()
    {
        long known = ChunkPos.Pack(1, 2);
        Require(known == 8_589_934_593L, "ChunkPos packing must place Z in the high 32 bits.");
        Require(ChunkPos.GetX(known) == 1 && ChunkPos.GetZ(known) == 2, "ChunkPos packed accessors must decode the known vector.");
        Require(ChunkPos.Unpack(known) == new ChunkPos(1, 2), "ChunkPos unpack must round-trip.");
        Require(ChunkPos.Unpack(ChunkPos.Pack(int.MinValue, int.MaxValue)) == new ChunkPos(int.MinValue, int.MaxValue), "ChunkPos packing must preserve full signed 32-bit coordinates.");
        Require(ChunkPos.Pack(-1, -1) == -1L, "ChunkPos all-negative-one coordinates must fill the packed long.");
        Require(ChunkPos.InvalidChunkPos == ChunkPos.Pack(1_875_066, 1_875_066), "ChunkPos invalid sentinel must preserve the source constant.");

        Require(ChunkPos.Containing(new BlockPos(-1, 0, -17)) == new ChunkPos(-1, -2), "ChunkPos containing must use arithmetic section shifts for negative block coordinates.");
        Require(ChunkPos.Pack(new BlockPos(-1, 0, -17)) == ChunkPos.Pack(-1, -2), "ChunkPos block packing must follow SectionPos block-to-section conversion.");
        Require(ChunkPos.FromSectionNode(SectionPos.AsLong(-3, 7, 5)) == ChunkPos.Pack(-3, 5), "ChunkPos conversion from a packed section must retain X and Z.");

        Require(ChunkPos.MinFromRegion(-1, -2) == new ChunkPos(-32, -64), "ChunkPos min-from-region must use arithmetic-compatible shifts.");
        Require(ChunkPos.MaxFromRegion(-1, -2) == new ChunkPos(-1, -33), "ChunkPos max-from-region must add the region's last index.");
        var negative = new ChunkPos(-33, -1);
        Require(negative.GetRegionX() == -2 && negative.GetRegionZ() == -1, "ChunkPos region coordinates must floor through arithmetic shifts.");
        Require(negative.GetRegionLocalX() == 31 && negative.GetRegionLocalZ() == 31, "ChunkPos region-local coordinates must use the low five bits.");
        Require(ChunkPos.GetRegionX(ChunkPos.Pack(-33, -1)) == -2, "ChunkPos packed region accessor must match instance region calculation.");

        var chunk = new ChunkPos(-2, 3);
        Require(chunk.GetMinBlockX() == -32 && chunk.GetMinBlockZ() == 48, "ChunkPos minimum block coordinates must multiply by sixteen.");
        Require(chunk.GetMaxBlockX() == -17 && chunk.GetMaxBlockZ() == 63, "ChunkPos maximum block coordinates must include offset fifteen.");
        Require(chunk.GetMiddleBlockX() == -24 && chunk.GetMiddleBlockZ() == 56, "ChunkPos middle block coordinates must use offset eight.");
        Require(chunk.GetBlockAt(15, 70, 0).Equals(new BlockPos(-17, 70, 48)), "ChunkPos local block lookup must preserve Y and offset X/Z.");
        Require(chunk.GetMiddleBlockPosition(70).Equals(new BlockPos(-24, 70, 56)), "ChunkPos middle position must combine its center and supplied Y.");
        Require(chunk.Contains(new BlockPos(-17, int.MaxValue, 63)), "ChunkPos containment must ignore block Y.");
        Require(!chunk.Contains(new BlockPos(-16, 70, 63)), "ChunkPos containment must reject coordinates outside its horizontal bounds.");
        Require(chunk.GetWorldPosition().Equals(new BlockPos(-32, 0, 48)), "ChunkPos world position must be the minimum X/Z corner at Y zero.");

        Require(new ChunkPos(1, 2).GetChessboardDistance(new ChunkPos(-2, 8)) == 6, "ChunkPos chessboard distance must use the largest horizontal delta.");
        Require(new ChunkPos(1, 2).DistanceSquared(new ChunkPos(-2, 6)) == 25, "ChunkPos squared distance must sum horizontal squared deltas.");
        Require(new ChunkPos(1, 2).DistanceSquared(ChunkPos.Pack(-2, 6)) == 25, "ChunkPos packed squared-distance overload must match.");
        Require(ChunkPos.Hash(1, 2) == new ChunkPos(1, 2).GetHashCode(), "ChunkPos hash code must use the reference LCG mixing constants.");

        ChunkPos[] forward = ChunkPos.RangeClosed(new ChunkPos(0, 0), new ChunkPos(1, 1)).ToArray();
        Require(
            forward.SequenceEqual(new[] { new ChunkPos(0, 0), new ChunkPos(1, 0), new ChunkPos(0, 1), new ChunkPos(1, 1) }),
            "ChunkPos range traversal must advance X first, then Z.");
        ChunkPos[] reverse = ChunkPos.RangeClosed(new ChunkPos(1, 1), new ChunkPos(0, 0)).ToArray();
        Require(
            reverse.SequenceEqual(new[] { new ChunkPos(1, 1), new ChunkPos(0, 1), new ChunkPos(1, 0), new ChunkPos(0, 0) }),
            "ChunkPos range traversal must follow the requested direction on both axes.");
    }

    private static void CheckSectionPos()
    {
        Require(SectionPos.SectionBits == 4 && SectionPos.SectionSize == 16, "SectionPos sections must span sixteen blocks per axis.");
        Require(SectionPos.SectionBlockCount == 4096, "SectionPos sections must contain 4096 block positions.");

        long known = SectionPos.AsLong(1, 2, 3);
        Require(known == 4_398_049_656_834L, "SectionPos packing must use the 22/20/22 bit layout.");
        Require(SectionPos.GetX(known) == 1 && SectionPos.GetY(known) == 2 && SectionPos.GetZ(known) == 3, "SectionPos packed accessors must decode the known vector.");
        Require(SectionPos.Of(known).Equals(SectionPos.Of(1, 2, 3)), "SectionPos packed round-trip must preserve coordinates.");
        Require(SectionPos.AsLong(-1, -1, -1) == -1L, "SectionPos all-negative-one coordinates must fill the packed long.");
        Require(SectionPos.Of(-1L).Equals(SectionPos.Of(-1, -1, -1)), "SectionPos sign extension must decode all packed axes.");
        Require(
            SectionPos.Of(SectionPos.AsLong(-2_097_152, -524_288, 2_097_151))
                .Equals(SectionPos.Of(-2_097_152, -524_288, 2_097_151)),
            "SectionPos must sign-extend its 22/20/22-bit fields.");

        Require(SectionPos.BlockToSectionCoord(-1) == -1, "Block coordinate -1 must belong to section -1.");
        Require(SectionPos.BlockToSectionCoord(-16) == -1, "Block coordinate -16 must belong to section -1.");
        Require(SectionPos.BlockToSectionCoord(-17) == -2, "Block coordinate -17 must belong to section -2.");
        Require(SectionPos.BlockToSectionCoord(-16.0001) == -2, "Double block coordinates must floor before section conversion.");
        Require(SectionPos.PosToSectionCoord(-0.01) == -1, "Position-to-section conversion must floor negative fractions before shifting.");

        SectionPos fractionalPosition = SectionPos.Of(new TestPosition(-0.01, -16.0001, 31.9999));
        Require(
            fractionalPosition.Equals(SectionPos.Of(-1, -2, 1)),
            "SectionPos.Of(Position) must floor all real-valued coordinates before converting them to sections.");
        Require(SectionPos.SectionToBlockCoord(-1) == -16, "Section -1 must begin at block -16.");
        Require(SectionPos.SectionToBlockCoord(-2, 15) == -17, "Section -2's last block must be -17.");

        var position = new BlockPos(-1, -17, 32);
        SectionPos section = SectionPos.Of(position);
        Require(section.Equals(SectionPos.Of(-1, -2, 2)), "SectionPos of BlockPos must arithmetic-shift all three block coordinates.");
        Require(SectionPos.Of(new ChunkPos(-1, 2), -2).Equals(section), "SectionPos of ChunkPos and section Y must preserve horizontal section coordinates.");
        Require(section.MinBlockX() == -16 && section.MinBlockY() == -32 && section.MinBlockZ() == 32, "SectionPos minimum block coordinates must align to section origins.");
        Require(section.MaxBlockX() == -1 && section.MaxBlockY() == -17 && section.MaxBlockZ() == 47, "SectionPos maximum block coordinates must include the fifteenth offset.");
        Require(section.Origin().Equals(new BlockPos(-16, -32, 32)), "SectionPos origin must return its minimum block corner.");
        Require(section.Center().Equals(new BlockPos(-8, -24, 40)), "SectionPos center must be origin plus eight on every axis.");
        Require(section.Chunk() == new ChunkPos(-1, 2), "SectionPos chunk conversion must retain section X/Z.");

        long sectionNode = SectionPos.AsLong(-2, -3, 4);
        foreach (Direction direction in Direction.GetValues())
        {
            SectionPos expected = SectionPos.Of(
                -2 + direction.GetStepX(),
                -3 + direction.GetStepY(),
                4 + direction.GetStepZ());
            Require(
                SectionPos.Of(SectionPos.Offset(sectionNode, direction)).Equals(expected),
                $"SectionPos offset by {direction.GetName()} must advance exactly one neighboring section.");
        }
        Require(SectionPos.BlockToSection(position.AsLong()) == section.AsLong(), "Packed block-to-section conversion must equal object conversion.");
        Require(SectionPos.SectionToChunk(section.AsLong()) == ChunkPos.Pack(-1, 2), "Packed section-to-chunk conversion must preserve section X/Z.");
        Require(SectionPos.GetZeroNode(SectionPos.AsLong(-1, -20, 2)) == SectionPos.AsLong(-1, 0, 2), "SectionPos zero node must clear its packed Y component.");

        short relative = SectionPos.SectionRelativePos(new BlockPos(-1, -2, -3));
        Require(relative == 0x0FDE, "SectionPos relative short must pack X in bits 8-11, Z in 4-7, and Y in 0-3.");
        Require(
            SectionPos.SectionRelativeX(relative) == 15 &&
            SectionPos.SectionRelativeY(relative) == 14 &&
            SectionPos.SectionRelativeZ(relative) == 13,
            "SectionPos relative component decoders must preserve the reference nibble layout.");
        Require(
            SectionPos.Of(-1, -1, -1).RelativeToBlockPos(relative).Equals(new BlockPos(-1, -2, -3)),
            "SectionPos relative-to-block conversion must rebuild absolute block coordinates.");
        Require(
            SectionPos.AroundChunk(new ChunkPos(-1, 2), 1, -2, -1).Count() == 18,
            "SectionPos around-chunk iteration must cover its inclusive horizontal and vertical bounds.");

        SectionPos[] closed = SectionPos.BetweenClosedStream(-1, 0, 2, 0, 1, 2).ToArray();
        Require(
            closed.SequenceEqual(new[]
            {
                SectionPos.Of(-1, 0, 2), SectionPos.Of(0, 0, 2),
                SectionPos.Of(-1, 1, 2), SectionPos.Of(0, 1, 2)
            }),
            "SectionPos closed-box traversal must advance X first, then Y, then Z.");

        var blocks = SectionPos.Of(0, 0, 0).BlocksInside()
            .Select(position => position.Immutable())
            .ToArray();
        Require(blocks.Length == SectionPos.SectionBlockCount, "SectionPos blocksInside must enumerate all 4096 positions.");
        Require(blocks[0].Equals(new BlockPos(0, 0, 0)), "SectionPos blocksInside must start at the section origin.");
        Require(blocks[^1].Equals(new BlockPos(15, 15, 15)), "SectionPos blocksInside must finish at the maximum block corner.");
        using (IEnumerator<BlockPos> cursor = SectionPos.Of(0, 0, 0).BlocksInside().GetEnumerator())
        {
            Require(cursor.MoveNext(), "SectionPos blocksInside must start an iterator.");
            BlockPos reusedCursor = cursor.Current;
            Require(cursor.MoveNext() && ReferenceEquals(reusedCursor, cursor.Current),
                "SectionPos blocksInside must preserve the mutable cursor reuse contract.");
        }

        var surrounding = new List<long>();
        SectionPos.AroundAndAtBlockPos(new BlockPos(15, 15, -1), surrounding.Add);
        Require(surrounding.Count == 8, "AroundAndAtBlockPos must emit eight sections when all three adjacent axes cross boundaries.");
        Require(
            surrounding.SequenceEqual(new[]
            {
                SectionPos.AsLong(0, 0, -1), SectionPos.AsLong(0, 0, 0),
                SectionPos.AsLong(0, 1, -1), SectionPos.AsLong(0, 1, 0),
                SectionPos.AsLong(1, 0, -1), SectionPos.AsLong(1, 0, 0),
                SectionPos.AsLong(1, 1, -1), SectionPos.AsLong(1, 1, 0)
            }),
            "AroundAndAtBlockPos must preserve X/Y/Z nested-loop order.");
        var single = new List<long>();
        SectionPos.AroundAndAtBlockPos(new BlockPos(5, 5, 5), single.Add);
        Require(single.SequenceEqual(new[] { SectionPos.AsLong(0, 0, 0) }), "AroundAndAtBlockPos must use the single-section fast path when possible.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
