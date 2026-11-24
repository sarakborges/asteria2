using Asteria.Content.Blocks;
using Asteria.Core;
using Asteria.World;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Structure;

internal static class StructurePieceChecks
{
    public static void Run()
    {
        CheckChunkProximity();
        CheckMetadata();
        CheckOrientedCoordinates();
        CheckWorldPosition();
        CheckRandomHorizontalDirection();
    }

    private static void CheckChunkProximity()
    {
        var chunk = new ChunkPos(0, 0);

        Require(CreatePiece(new BoundingBox(-12, 1000, 27, -12, 1000, 27))
                    .IsCloseToChunk(chunk, 12),
            "StructurePiece proximity must include both inclusive horizontal distance boundaries.");
        Require(!CreatePiece(new BoundingBox(-13, -1000, 8, -13, -1000, 8))
                    .IsCloseToChunk(chunk, 12),
            "StructurePiece proximity must reject a piece one block beyond the negative X bound.");
        Require(!CreatePiece(new BoundingBox(28, 0, 8, 28, 0, 8))
                    .IsCloseToChunk(chunk, 12),
            "StructurePiece proximity must reject a piece one block beyond the positive X bound.");
        Require(!CreatePiece(new BoundingBox(8, 0, -13, 8, 0, -13))
                    .IsCloseToChunk(chunk, 12),
            "StructurePiece proximity must reject a piece one block beyond the negative Z bound.");
        Require(!CreatePiece(new BoundingBox(8, 0, 28, 8, 0, 28))
                    .IsCloseToChunk(chunk, 12),
            "StructurePiece proximity must reject a piece one block beyond the positive Z bound.");

        var negativeChunk = new ChunkPos(-2, 1);
        Require(CreatePiece(new BoundingBox(-44, int.MinValue, 43, -44, int.MinValue, 43))
                    .IsCloseToChunk(negativeChunk, 12),
            "StructurePiece proximity must derive bounds from negative chunk coordinates.");
        Require(!CreatePiece(new BoundingBox(-45, 0, 20, -45, 0, 20))
                    .IsCloseToChunk(negativeChunk, 12),
            "StructurePiece proximity must preserve the expanded bounds for negative chunks.");

        Require(!CreatePiece(new BoundingBox(0, 0, 0, 0, 0, 0))
                    .IsCloseToChunk(chunk, -1),
            "StructurePiece proximity must use the supplied distance without normalizing it.");
    }

    private static void CheckMetadata()
    {
        var bounds = new BoundingBox(-5, 10, -7, 4, 20, 8);
        TestPiece piece = CreatePiece(bounds, 3);

        Require(ReferenceEquals(piece.GetBoundingBox(), bounds),
            "StructurePiece.getBoundingBox must return its backing bounds.");
        Require(piece.GetGenDepth() == 3,
            "StructurePiece must preserve its initial generation depth.");

        piece.SetGenDepth(9);
        Require(piece.GetGenDepth() == 9,
            "StructurePiece.setGenDepth must update generation depth.");

        Require(piece.GetLocatorPosition().Equals(bounds.GetCenter()),
            "StructurePiece locator position must be the bounding-box center.");
    }

    private static void CheckOrientedCoordinates()
    {
        var bounds = new BoundingBox(100, 20, 200, 111, 29, 211);
        TestPiece piece = CreatePiece(bounds);

        Require(piece.GetOrientation() is null &&
                piece.GetMirror() is null &&
                piece.GetRotation() is null,
            "StructurePiece constructor must leave orientation, mirror and rotation at their default null values.");

        CheckCoordinates(piece, 2, 4, 3, 2, 4, 3);

        piece.SetOrientation(Direction.NORTH);
        CheckCoordinates(piece, 2, 4, 3, 102, 24, 208);
        Require(ReferenceEquals(piece.GetMirror(), Mirror.NONE) &&
                ReferenceEquals(piece.GetRotation(), Rotation.NONE),
            "North orientation must select no mirror and no rotation.");

        piece.SetOrientation(Direction.SOUTH);
        CheckCoordinates(piece, 2, 4, 3, 102, 24, 203);
        Require(ReferenceEquals(piece.GetMirror(), Mirror.LEFT_RIGHT) &&
                ReferenceEquals(piece.GetRotation(), Rotation.NONE),
            "South orientation must select LEFT_RIGHT and no rotation.");

        piece.SetOrientation(Direction.WEST);
        CheckCoordinates(piece, 2, 4, 3, 108, 24, 202);
        Require(ReferenceEquals(piece.GetMirror(), Mirror.LEFT_RIGHT) &&
                ReferenceEquals(piece.GetRotation(), Rotation.CLOCKWISE_90),
            "West orientation must select LEFT_RIGHT and CLOCKWISE_90.");

        piece.SetOrientation(Direction.EAST);
        CheckCoordinates(piece, 2, 4, 3, 103, 24, 202);
        Require(ReferenceEquals(piece.GetMirror(), Mirror.NONE) &&
                ReferenceEquals(piece.GetRotation(), Rotation.CLOCKWISE_90),
            "East orientation must select no mirror and CLOCKWISE_90.");

        piece.SetOrientation(Direction.UP);
        CheckCoordinates(piece, 2, 4, 3, 2, 24, 3);
        Require(ReferenceEquals(piece.GetMirror(), Mirror.NONE) &&
                ReferenceEquals(piece.GetRotation(), Rotation.NONE),
            "Vertical orientations must preserve the default mirror and rotation.");

        piece.SetOrientation(null);
        CheckCoordinates(piece, 2, 4, 3, 2, 4, 3);
        Require(ReferenceEquals(piece.GetMirror(), Mirror.NONE) &&
                ReferenceEquals(piece.GetRotation(), Rotation.NONE),
            "Clearing orientation must reset mirror and rotation to NONE.");

        Require(TestPiece.CreateBox(10, 20, 30, Direction.NORTH, 4, 5, 6)
                    .Equals(new BoundingBox(10, 20, 30, 13, 24, 35)) &&
                TestPiece.CreateBox(10, 20, 30, Direction.WEST, 4, 5, 6)
                    .Equals(new BoundingBox(10, 20, 30, 15, 24, 33)),
            "StructurePiece.makeBoundingBox must swap width and depth when the direction axis is not Z.");

        BoundingBox moved = new BoundingBox(1, 2, 3, 4, 5, 6);
        TestPiece movingPiece = CreatePiece(moved);
        movingPiece.Move(-2, 3, 5);
        Require(moved.Equals(new BoundingBox(-1, 5, 8, 2, 8, 11)),
            "StructurePiece.move must translate the backing bounding box in place.");
    }

    private static void CheckRandomHorizontalDirection()
    {
        int[] seeds = { 0, 1, -1, 123456, int.MinValue };
        Direction[] expectedOrder =
        {
            Direction.NORTH,
            Direction.EAST,
            Direction.SOUTH,
            Direction.WEST
        };

        foreach (int seed in seeds)
        {
            var expectedRandom = new LegacyRandomSource(seed);
            var actualRandom = new LegacyRandomSource(seed);
            Direction expected = expectedOrder[expectedRandom.NextInt(expectedOrder.Length)];
            Direction actual = TestPiece.RandomHorizontalDirection(actualRandom);

            Require(ReferenceEquals(actual, expected),
                "StructurePiece.getRandomHorizontalDirection must select from the horizontal plane in source order.");
        }
    }

    private static void CheckWorldPosition()
    {
        TestPiece piece = CreatePiece(new BoundingBox(100, 20, 200, 111, 29, 211));
        piece.SetOrientation(Direction.WEST);

        BlockPos.MutableBlockPos position = piece.WorldPos(2, 4, 3);
        Require(position.Equals(new BlockPos(108, 24, 202)),
            "StructurePiece.getWorldPos must compose the exact local X/Y/Z transforms.");

        position.Set(1, 2, 3);
        Require(position.Equals(new BlockPos(1, 2, 3)),
            "StructurePiece.getWorldPos must return a mutable block-position instance.");

        piece.SetOrientation(null);
        Require(piece.WorldPos(-2, 5, 7).Equals(new BlockPos(-2, 5, 7)),
            "StructurePiece.getWorldPos must bypass transforms when orientation is null.");
    }

    private static void CheckCoordinates(
        TestPiece piece,
        int localX,
        int localY,
        int localZ,
        int expectedX,
        int expectedY,
        int expectedZ)
    {
        Require(piece.WorldX(localX, localZ) == expectedX &&
                piece.WorldY(localY) == expectedY &&
                piece.WorldZ(localX, localZ) == expectedZ,
            "StructurePiece local-to-world transforms must preserve the reference switch formulas.");
    }

    private static TestPiece CreatePiece(BoundingBox bounds, int genDepth = 0) =>
        new(genDepth, bounds);

    private sealed class TestPiece(int genDepth, BoundingBox bounds)
        : StructurePiece(genDepth, bounds)
    {
        public BlockPos.MutableBlockPos WorldPos(int x, int y, int z) => GetWorldPos(x, y, z);
        public int WorldX(int x, int z) => GetWorldX(x, z);
        public int WorldY(int y) => GetWorldY(y);
        public int WorldZ(int x, int z) => GetWorldZ(x, z);
        public static Direction RandomHorizontalDirection(IRandomSource random) =>
            GetRandomHorizontalDirection(random);

        public static BoundingBox CreateBox(
            int x, int y, int z, Direction direction, int width, int height, int depth) =>
            MakeBoundingBox(x, y, z, direction, width, height, depth);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
