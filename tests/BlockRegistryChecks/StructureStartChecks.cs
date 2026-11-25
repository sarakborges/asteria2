using Asteria.World.LevelGen.Structure;
using Asteria.World.LevelGen.Structure.Pieces;
using Asteria.Core;
using Asteria.Util.Random;
using Asteria.World.Level;
using Asteria.World.LevelGen;
using Asteria.World;

internal static class StructureStartChecks
{
    public static void Run()
    {
        CheckInvalidStart();
        CheckStartIdentityAndReferences();
        CheckTerrainAdaptationBounds();
        CheckPlaceInChunkDispatchesOnlyIntersectingPieces();
        CheckPlaceInChunkFailsClosedForUnportedPieces();
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
            () => _ = invalid.GetBoundingBox(),
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

    private static void CheckPlaceInChunkDispatchesOnlyIntersectingPieces()
    {
        const long seed = 0x535452504C414345L;
        var world = new OverworldWorld(seed, LevelHeightAccessor.Create(-16, 64));
        ChunkPos origin = ChunkPos.Zero;
        var westPiece = new PlacementProbePiece(new BoundingBox(12, 0, 2, 15, 4, 6));
        var eastPiece = new PlacementProbePiece(new BoundingBox(16, 0, 2, 20, 4, 6));
        var outsidePiece = new PlacementProbePiece(new BoundingBox(32, 0, 2, 35, 4, 6));
        var structure = new TestStructure(StructureSettings.DEFAULT);
        var start = new StructureStart(
            structure, origin, 0,
            new PiecesContainer(new StructurePiece[] { westPiece, eastPiece, outsidePiece }));
        var random = new LegacyRandomSource(seed);
        var westBox = new BoundingBox(0, -16, 0, 15, 47, 15);
        var eastBox = new BoundingBox(16, -16, 0, 31, 47, 15);
        ChunkPos eastChunk = new(1, 0);

        start.PlaceInChunk(world, world.Generator, random, westBox, origin);
        Require(
            westPiece.Calls == 1 &&
            eastPiece.Calls == 0 &&
            outsidePiece.Calls == 0,
            "StructureStart.PlaceInChunk must dispatch only pieces whose inclusive bounds intersect the requested chunk box.");
        Require(
            ReferenceEquals(westPiece.Level, world) &&
            ReferenceEquals(westPiece.Generator, world.Generator) &&
            ReferenceEquals(westPiece.Random, random) &&
            ReferenceEquals(westPiece.ChunkBox, westBox) &&
            ReferenceEquals(westPiece.ChunkPosition, origin) &&
            westPiece.ReferencePosition!.Equals(origin.GetMiddleBlockPosition(0)),
            "Piece post-processing must receive the exact world services, random source, chunk bounds and vanilla chunk-center reference position.");

        start.PlaceInChunk(world, world.Generator, random, eastBox, eastChunk);
        Require(
            westPiece.Calls == 1 &&
            eastPiece.Calls == 1 &&
            outsidePiece.Calls == 0 &&
            eastPiece.ReferencePosition!.Equals(eastChunk.GetMiddleBlockPosition(0)),
            "A multi-chunk structure start must dispatch each piece only in the chunk box it intersects and derive that chunk's own reference position.");

        StructureStart.INVALID_START.PlaceInChunk(world, world.Generator, random, westBox, origin);
        Require(
            westPiece.Calls == 1 && eastPiece.Calls == 1 && outsidePiece.Calls == 0,
            "Invalid structure starts must not dispatch piece placement.");
    }

    private static void CheckPlaceInChunkFailsClosedForUnportedPieces()
    {
        const long seed = 0x554E504F52544544L;
        var world = new OverworldWorld(seed, LevelHeightAccessor.Create(-16, 64));
        var piece = new TestPiece(new BoundingBox(0, 0, 0, 3, 3, 3));
        var start = new StructureStart(
            new TestStructure(StructureSettings.DEFAULT),
            ChunkPos.Zero,
            0,
            new PiecesContainer(new[] { piece }));

        RequireThrows<NotSupportedException>(
            () => start.PlaceInChunk(
                world, world.Generator, new LegacyRandomSource(seed),
                new BoundingBox(0, -16, 0, 15, 47, 15), ChunkPos.Zero),
            "A valid start containing a piece without PostProcess must fail explicitly instead of silently omitting structure blocks.");
    }
    private sealed class TestStructure : Structure
    {
        public TestStructure(StructureSettings settings) : base(settings)
        {
        } 
        protected override Structure.GenerationStub? FindGenerationPoint(
            Structure.GenerationContext context) => null;
    }

    private sealed class PlacementProbePiece(BoundingBox bounds) : StructurePiece(0, bounds)
    {
        public int Calls { get; private set; }
        public WorldGenLevel? Level { get; private set; }
        public NoiseBasedChunkGenerator? Generator { get; private set; }
        public IRandomSource? Random { get; private set; }
        public BoundingBox? ChunkBox { get; private set; }
        public ChunkPos? ChunkPosition { get; private set; }
        public BlockPos? ReferencePosition { get; private set; }

        public override void PostProcess(
            WorldGenLevel level,
            NoiseBasedChunkGenerator generator,
            IRandomSource random,
            BoundingBox chunkBoundingBox,
            ChunkPos chunkPos,
            BlockPos referencePosition)
        {
            Calls++;
            Level = level;
            Generator = generator;
            Random = random;
            ChunkBox = chunkBoundingBox;
            ChunkPosition = chunkPos;
            ReferencePosition = referencePosition;
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
