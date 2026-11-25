using Asteria.Content.Biomes;
using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Util;
using Asteria.World;
using Asteria.World.Level;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Structure;
using Asteria.World.LevelGen.Structure.Pieces;
using Asteria.World.LevelGen.Structure.Placement;

internal static class ChunkFeatureGenerationChecks
{
    public static void Run()
    {
        CheckFeaturesTaskPlacesReferencedStructurePieces();
        CheckUnportedStructurePiecesPreventPromotion();
        CheckFeaturesTaskRejectsInsufficientTerrain();
    }

    private static void CheckFeaturesTaskPlacesReferencedStructurePieces()
    {
        const long seed = 0x4645415455524553L;
        var fixture = CreateFixture(seed, new ChunkPos(-7, 5));
        var piece = new PlacementProbePiece(new BoundingBox(
            fixture.CenterPosition.GetMinBlockX() + 7,
            0,
            fixture.CenterPosition.GetMinBlockZ() + 7,
            fixture.CenterPosition.GetMinBlockX() + 9,
            2,
            fixture.CenterPosition.GetMinBlockZ() + 9));
        ChunkPos sourcePosition = new(
            fixture.CenterPosition.X + 2,
            fixture.CenterPosition.Z);
        var start = new StructureStart(
            fixture.Structure,
            sourcePosition,
            references: 0,
            new PiecesContainer(new StructurePiece[] { piece }));
        fixture.Chunks[sourcePosition].SetStartForStructure(fixture.Structure, start);
        fixture.Center.AddReferenceForStructure(fixture.Structure, sourcePosition.Pack());

        ChunkAccess result = ChunkPyramid.GenerationPyramid
            .GetStepTo(ChunkStatus.FEATURES)
            .Apply(fixture.Context, fixture.Cache, fixture.Center)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        Require(
            ReferenceEquals(result, fixture.Center) &&
            ReferenceEquals(fixture.Center.GetPersistedStatus(), ChunkStatus.FEATURES),
            "The FEATURES task must promote its center only after decoration completes.");
        Require(
            piece.Calls == 1 &&
            piece.ReceivedLevel is WorldGenRegion &&
            ReferenceEquals(piece.ReceivedGenerator, fixture.World.Generator) &&
            piece.ReceivedChunkPosition.HasValue &&
            piece.ReceivedChunkPosition.Value.Equals(fixture.CenterPosition) &&
            piece.ReceivedReferencePosition is not null &&
            piece.ReceivedReferencePosition.Equals(fixture.CenterPosition.GetMiddleBlockPosition(0)) &&
            piece.ReceivedChunkBox is not null &&
            piece.ReceivedRandom is not null,
            "FEATURES must resolve the center chunk's structure reference and dispatch the intersecting piece with the stage-scoped region.");
        Require(
            ReferenceEquals(
                fixture.Center.GetBlockState(fixture.CenterPosition.GetMiddleBlockPosition(0)),
                Blocks.STONE.DefaultState),
            "A structure piece must be able to write inside the FEATURES chunk's permitted write zone.");
    }

    private static void CheckUnportedStructurePiecesPreventPromotion()
    {
        const long seed = 0x554E504F52544544L;
        var fixture = CreateFixture(seed, new ChunkPos(42, -19));
        ChunkPos sourcePosition = new(
            fixture.CenterPosition.X - 2,
            fixture.CenterPosition.Z);
        var piece = new UnportedProbePiece(new BoundingBox(
            fixture.CenterPosition.GetMinBlockX() + 1,
            0,
            fixture.CenterPosition.GetMinBlockZ() + 1,
            fixture.CenterPosition.GetMinBlockX() + 4,
            3,
            fixture.CenterPosition.GetMinBlockZ() + 4));
        fixture.Chunks[sourcePosition].SetStartForStructure(
            fixture.Structure,
            new StructureStart(
                fixture.Structure,
                sourcePosition,
                references: 0,
                new PiecesContainer(new StructurePiece[] { piece })));
        fixture.Center.AddReferenceForStructure(fixture.Structure, sourcePosition.Pack());

        InvalidOperationException failure = RequireThrows<InvalidOperationException>(
            () => ChunkPyramid.GenerationPyramid
                .GetStepTo(ChunkStatus.FEATURES)
                .Apply(fixture.Context, fixture.Cache, fixture.Center)
                .AsTask()
                .GetAwaiter()
                .GetResult(),
            "FEATURES must fail instead of silently skipping a referenced structure whose piece type has no placement implementation.");

        Require(
            failure.ToString().Contains(nameof(UnportedProbePiece), StringComparison.Ordinal) &&
            ReferenceEquals(fixture.Center.GetPersistedStatus(), ChunkStatus.TERRAIN),
            "A structure placement failure must preserve the TERRAIN status and expose the unported piece type.");
    }

    private static void CheckFeaturesTaskRejectsInsufficientTerrain()
    {
        var fixture = CreateFixture(0x4645415453544154L, new ChunkPos(13, 22));
        fixture.Center.SetPersistedStatus(ChunkStatus.BIOMES);

        RequireThrows<InvalidOperationException>(
            () => ChunkStatusTasks.GenerateFeatures(
                fixture.Context,
                ChunkPyramid.GenerationPyramid.GetStepTo(ChunkStatus.FEATURES),
                fixture.Cache,
                fixture.Center),
            "FEATURES must reject a center chunk that has not reached TERRAIN.");
    }

    private static FeatureFixture CreateFixture(long seed, ChunkPos centerPosition)
    {
        LevelHeightAccessor heightAccessor = LevelHeightAccessor.Create(-16, 48);
        var world = new OverworldWorld(seed, heightAccessor);
        HolderSet<Biome> possibleBiomes = HolderSet<Biome>.Direct(
            BuiltInRegistries.Biome.ListElements().Cast<Holder<Biome>>());
        var structure = new PlacementProbeStructure(possibleBiomes);
        var structureSet = new StructureSet(
            Holder<Structure>.Direct(structure),
            new AlwaysStructurePlacement());
        ChunkGeneratorStructureState structureState = world.Generator.CreateStructureState(
            ChunkPos.Zero,
            new[] { Holder<StructureSet>.Direct(structureSet) });
        var context = new WorldGenContext(
            world,
            world.Generator,
            world.LightEngine,
            structureState);
        var chunks = new Dictionary<ChunkPos, ProtoChunk>();
        foreach (ChunkPos position in ChunkPos.RangeClosed(centerPosition, 8))
        {
            var created = new ProtoChunk(
                position,
                heightAccessor,
                PalettedContainerFactory.CreateBuiltIn());
            if (centerPosition.GetChessboardDistance(position) <= 1)
            {
                world.Generator.CreateBiomes(created);
                created.SetPersistedStatus(ChunkStatus.TERRAIN);
            }
            else
            {
                created.SetPersistedStatus(ChunkStatus.STRUCTURE_STARTS);
            }

            chunks.Add(position, created);
        }

        ProtoChunk center = chunks[centerPosition];
        StaticCache2D<ChunkAccess?> cache = StaticCache2D<ChunkAccess?>.Create(
            centerPosition.X,
            centerPosition.Z,
            8,
            (x, z) => chunks[new ChunkPos(x, z)]);

        return new FeatureFixture(
            world,
            context,
            structure,
            centerPosition,
            center,
            chunks,
            cache);
    }

    private sealed record FeatureFixture(
        OverworldWorld World,
        WorldGenContext Context,
        Structure Structure,
        ChunkPos CenterPosition,
        ProtoChunk Center,
        IReadOnlyDictionary<ChunkPos, ProtoChunk> Chunks,
        StaticCache2D<ChunkAccess?> Cache);

    private sealed class PlacementProbeStructure(HolderSet<Biome> biomes)
        : Structure(new StructureSettings(
            biomes,
            GenerationStep.Decoration.SURFACE_STRUCTURES,
            TerrainAdjustment.NONE))
    {
        protected override Structure.GenerationStub? FindGenerationPoint(
            Structure.GenerationContext context) => null;
    }

    private sealed class AlwaysStructurePlacement : StructurePlacement
    {
        public bool IsStructureChunk(
            IStructurePlacementState state,
            int sourceX,
            int sourceZ) => true;
    }

    private sealed class PlacementProbePiece(BoundingBox bounds)
        : StructurePiece(0, bounds)
    {
        public int Calls { get; private set; }
        public WorldGenLevel? ReceivedLevel { get; private set; }
        public NoiseBasedChunkGenerator? ReceivedGenerator { get; private set; }
        public IRandomSource? ReceivedRandom { get; private set; }
        public BoundingBox? ReceivedChunkBox { get; private set; }
        public ChunkPos? ReceivedChunkPosition { get; private set; }
        public BlockPos? ReceivedReferencePosition { get; private set; }

        public override void PostProcess(
            WorldGenLevel level,
            NoiseBasedChunkGenerator generator,
            IRandomSource random,
            BoundingBox chunkBoundingBox,
            ChunkPos chunkPos,
            BlockPos referencePosition)
        {
            Calls++;
            ReceivedLevel = level;
            ReceivedGenerator = generator;
            ReceivedRandom = random;
            ReceivedChunkBox = chunkBoundingBox;
            ReceivedChunkPosition = chunkPos;
            ReceivedReferencePosition = referencePosition;
            _ = level.SetBlock(
                chunkPos.GetMiddleBlockPosition(0),
                Blocks.STONE.DefaultState);
        }
    }

    private sealed class UnportedProbePiece(BoundingBox bounds)
        : StructurePiece(0, bounds);

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
