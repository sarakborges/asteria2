using Asteria.World.LevelGen.Structure;
using Asteria.World.LevelGen.Structure.Pieces;

internal static class StructureStartChecks
{
    public static void Run()
    {
        CheckInvalidStart();
        CheckStartIdentityAndReferences();
        CheckTerrainAdaptationBounds();
    }

    private static void CheckInvalidStart()
    {
        StructureStart invalid = StructureStart.INVALID_START;

        Require(
            !invalid.IsValid() &&
            invalid.GetPieces().Count == 0 &&
            invalid.GetReferences() == 0 &&
            invalid.GetStructure() is null &&
            invalid.GetChunkPos() == ChunkPos.Zero,
            "The invalid start must be an empty, structure-less sentinel at chunk origin.");

        RequireThrows<InvalidOperationException>(
            invalid.GetBoundingBox,
            "The invalid start must not produce a bounding box from an empty piece container.");
    }

    private static void CheckStartIdentityAndReferences()
    {
        var structure = new TestStructure(StructureSettings.DEFAULT);
        var position = new ChunkPos(-12, 37);
        var piece = new TestPiece(new BoundingBox(-180, 22, 580, -171, 31, 591));
        var pieces = new PiecesContainer(new[] { piece });
        var start = new StructureStart(structure, position, 0, pieces);

        Require(
            start.IsValid() &&
            start.GetChunkPos() == position &&
            ReferenceEquals(start.GetStructure(), structure) &&
            start.GetPieces().Count == 1 &&
            ReferenceEquals(start.GetPieces()[0], piece),
            "A valid structure start must retain its structure, origin chunk and ordered pieces.");

        Require(
            start.GetReferences() == 0 && start.CanBeReferenced(),
            "A structure start with zero references must still accept its one allowed reference.");

        start.AddReference();
        Require(
            start.GetReferences() == 1 && !start.CanBeReferenced(),
            "Adding one reference must reach the vanilla maximum and disallow another reference.");

        start.AddReference();
        Require(
            start.GetReferences() == 2 && !start.CanBeReferenced(),
            "AddReference increments unconditionally; CanBeReferenced is the caller's guard.");
    }

    private static void CheckTerrainAdaptationBounds()
    {
        BoundingBox original = new(100, 40, -80, 115, 55, -65);

        var noAdaptation = new TestStructure(new StructureSettings(
            StructureSettings.DEFAULT.Biomes,
            StructureSettings.DEFAULT.Step,
            TerrainAdjustment.NONE));
        Require(
            ReferenceEquals(noAdaptation.AdjustBoundingBox(original), original),
            "NONE terrain adaptation must preserve the original bounding-box instance.");

        foreach (TerrainAdjustment adjustment in TerrainAdjustment.Values
                     .Where(value => !ReferenceEquals(value, TerrainAdjustment.NONE)))
        {
            var structure = new TestStructure(new StructureSettings(
                StructureSettings.DEFAULT.Biomes,
                StructureSettings.DEFAULT.Step,
                adjustment));

            BoundingBox adjusted = structure.AdjustBoundingBox(original);
            Require(
                adjusted.MinX() == 88 &&
                adjusted.MinY() == 28 &&
                adjusted.MinZ() == -92 &&
                adjusted.MaxX() == 127 &&
                adjusted.MaxY() == 67 &&
                adjusted.MaxZ() == -53,
                $"{adjustment} terrain adaptation must inflate all six bounds by twelve blocks.");
        }

        var structureForCaching = new TestStructure(new StructureSettings(
            StructureSettings.DEFAULT.Biomes,
            StructureSettings.DEFAULT.Step,
            TerrainAdjustment.BURY));
        var start = new StructureStart(
            structureForCaching,
            ChunkPos.Zero,
            0,
            new PiecesContainer(new[]
            {
                new TestPiece(original)
            }));

        BoundingBox first = start.GetBoundingBox();
        BoundingBox second = start.GetBoundingBox();
        Require(
            ReferenceEquals(first, second),
            "StructureStart must cache and return its adjusted bounding box.");
    }

    private sealed class TestStructure : Structure
    {
        public TestStructure(StructureSettings settings) : base(settings)
        {
        }
    }

    private sealed class TestPiece : StructurePiece
    {
        public TestPiece(BoundingBox boundingBox) : base(0, boundingBox)
        {
        }
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
