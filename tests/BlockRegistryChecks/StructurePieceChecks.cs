using Asteria.World;
using Asteria.World.LevelGen.Structure;

internal static class StructurePieceChecks
{
    public static void Run()
    {
        CheckChunkProximity();
        CheckMetadata();
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

    private static TestPiece CreatePiece(BoundingBox bounds, int genDepth = 0) =>
        new(genDepth, bounds);

    private sealed class TestPiece(int genDepth, BoundingBox bounds)
        : StructurePiece(genDepth, bounds);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
