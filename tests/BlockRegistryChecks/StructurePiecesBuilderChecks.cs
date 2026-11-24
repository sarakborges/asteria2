using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Structure;
using Asteria.World.LevelGen.Structure.Pieces;

internal static class StructurePiecesBuilderChecks
{
    public static void Run()
    {
        CheckCollectionAndCollisions();
        CheckVerticalOffset();
        CheckSeaLevelPlacement();
        CheckHeightRangePlacement();
    }

    private static void CheckCollectionAndCollisions()
    {
        var builder = new StructurePiecesBuilder();
        Require(builder.IsEmpty(), "A new StructurePiecesBuilder must be empty.");

        TestPiece first = CreatePiece(new BoundingBox(-10, 20, -10, -1, 30, -1));
        TestPiece second = CreatePiece(new BoundingBox(20, 40, 20, 29, 50, 29));
        builder.AddPiece(first);
        builder.AddPiece(second);

        Require(!builder.IsEmpty(), "Adding pieces must clear the builder's empty state.");
        Require(builder.GetBoundingBox().Equals(new BoundingBox(-10, 20, -10, 29, 50, 29)),
            "StructurePiecesBuilder.getBoundingBox must encompass every added piece.");

        Require(ReferenceEquals(builder.FindCollisionPiece(new BoundingBox(-2, 25, -2, 0, 26, 0)), first),
            "Collision search must return the first intersecting piece.");
        Require(ReferenceEquals(builder.FindCollisionPiece(new BoundingBox(25, 45, 25, 25, 45, 25)), second),
            "Collision search must continue until it finds an intersecting piece.");
        Require(builder.FindCollisionPiece(new BoundingBox(100, 100, 100, 101, 101, 101)) is null,
            "Collision search must return null when no piece intersects.");

        PiecesContainer built = builder.Build();
        builder.Clear();
        Require(builder.IsEmpty(), "clear must remove every piece from the builder.");
        Require(built.Pieces.Count == 2 &&
                ReferenceEquals(built.Pieces[0], first) &&
                ReferenceEquals(built.Pieces[1], second),
            "build must copy the current piece collection into an independent PiecesContainer.");

        RequireThrows(
            () => builder.GetBoundingBox(),
            "Unable to calculate boundingbox without pieces");
    }

    private static void CheckVerticalOffset()
    {
        var builder = new StructurePiecesBuilder();
        var firstBounds = new BoundingBox(0, 10, 0, 2, 12, 2);
        var secondBounds = new BoundingBox(10, 20, 10, 12, 22, 12);
        builder.AddPiece(CreatePiece(firstBounds));
        builder.AddPiece(CreatePiece(secondBounds));

#pragma warning disable CS0618
        builder.OffsetPiecesVertically(-7);
#pragma warning restore CS0618

        Require(firstBounds.Equals(new BoundingBox(0, 3, 0, 2, 5, 2)) &&
                secondBounds.Equals(new BoundingBox(10, 13, 10, 12, 15, 12)),
            "offsetPiecesVertically must move every piece in place by the supplied Y delta.");
    }

    private static void CheckSeaLevelPlacement()
    {
        var builder = new StructurePiecesBuilder();
        var bounds = new BoundingBox(0, 20, 0, 9, 30, 9);
        builder.AddPiece(CreatePiece(bounds));

        var expectedRandom = new LegacyRandomSource(123456L);
        int expectedTop = 12 + expectedRandom.NextInt(42);
#pragma warning disable CS0618
        int dy = builder.MoveBelowSeaLevel(64, 0, new LegacyRandomSource(123456L), 10);
#pragma warning restore CS0618

        Require(dy == expectedTop - 30 &&
                bounds.MinY() == 20 + dy &&
                bounds.MaxY() == expectedTop &&
                bounds.MaxY() < 54,
            "moveBelowSeaLevel must preserve the source height-span, upper bound and RNG draw.");
    }

    private static void CheckHeightRangePlacement()
    {
        var builder = new StructurePiecesBuilder();
        var bounds = new BoundingBox(0, 20, 0, 9, 30, 9);
        builder.AddPiece(CreatePiece(bounds));

        var expectedRandom = new LegacyRandomSource(98765L);
        int expectedBottom = expectedRandom.NextInt(20);
        builder.MoveInsideHeights(new LegacyRandomSource(98765L), 0, 30);

        Require(bounds.MinY() == expectedBottom &&
                bounds.MaxY() == expectedBottom + 10,
            "moveInsideHeights must draw from the available bottom-offset range and preserve height.");

        var narrowBuilder = new StructurePiecesBuilder();
        var narrowBounds = new BoundingBox(0, 20, 0, 9, 30, 9);
        narrowBuilder.AddPiece(CreatePiece(narrowBounds));
        narrowBuilder.MoveInsideHeights(new LegacyRandomSource(1L), 7, 17);
        Require(narrowBounds.MinY() == 7 && narrowBounds.MaxY() == 17,
            "moveInsideHeights must use lowestAllowed directly when heightSpan is one.");
    }

    private static TestPiece CreatePiece(BoundingBox bounds) => new(bounds);

    private sealed class TestPiece(BoundingBox bounds) : StructurePiece(0, bounds);

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            Require(exception.Message == message,
                "Empty builder bounding-box calculation must preserve the source exception.");
            return;
        }

        throw new InvalidOperationException("Expected an InvalidOperationException.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
