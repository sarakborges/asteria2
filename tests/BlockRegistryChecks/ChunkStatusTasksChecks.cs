using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Content.Biomes;
using Asteria.Core;
using Asteria.Content.Registries;
using Asteria.Util;
using Asteria.World;
using Asteria.World.Level;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Structure;
using Asteria.World.LevelGen.Structure.Pieces;
using Asteria.World.LevelGen.Structure.Placement;

internal static class ChunkStatusTasksChecks
{
    public static void Run()
    {
        CheckBiomeTaskIsOnlyWiredForGeneration();
        CheckBiomeTaskPopulatesTheCenterChunk();
        CheckBiomeTaskRejectsAnUnrelatedCache();
        CheckStructureStartsTaskGeneratesAndStoresStart();
        CheckStructureReferencesTaskCollectsIntersectingStarts();
    }

    private static void CheckBiomeTaskIsOnlyWiredForGeneration()
    {
        ChunkStep generationStep =
            ChunkPyramid.GenerationPyramid.GetStepTo(ChunkStatus.BIOMES);
        ChunkStep loadingStep =
            ChunkPyramid.LoadingPyramid.GetStepTo(ChunkStatus.BIOMES);

        Require(
            generationStep.Task == ChunkStatusTasks.GenerateBiomes,
            "The generation BIOMES step must execute the ported biome-generation task.");
        Require(
            loadingStep.Task is null,
            "The loading BIOMES step must remain unassigned until its persisted-chunk loading task is ported.");

        Require(
            ChunkPyramid.GenerationPyramid.GetStepTo(ChunkStatus.STRUCTURE_REFERENCES).Task ==
                ChunkStatusTasks.GenerateStructureReferences,
            "The generation STRUCTURE_REFERENCES step must execute the ported reference-generation task.");
        Require(
            ChunkPyramid.LoadingPyramid.GetStepTo(ChunkStatus.STRUCTURE_REFERENCES).Task is null,
            "The loading STRUCTURE_REFERENCES step must remain unassigned until persisted references can be loaded.");
        Require(
            ChunkPyramid.GenerationPyramid.GetStepTo(ChunkStatus.STRUCTURE_STARTS).Task ==
                ChunkStatusTasks.GenerateStructureStarts,
            "The generation STRUCTURE_STARTS step must execute the ported structure-start task.");
        Require(
            ChunkPyramid.LoadingPyramid.GetStepTo(ChunkStatus.STRUCTURE_STARTS).Task is null,
            "The loading STRUCTURE_STARTS step must remain unassigned until persisted structure starts can be loaded.");
        Require(
            ChunkPyramid.GenerationPyramid.Steps
                .Where(step =>
                    step.TargetStatus != ChunkStatus.BIOMES &&
                    step.TargetStatus != ChunkStatus.STRUCTURE_STARTS &&
                    step.TargetStatus != ChunkStatus.STRUCTURE_REFERENCES)
                .All(step => step.Task is null),
            "No other generation stage may be marked executable before its task is ported.");
    }

    private static void CheckBiomeTaskPopulatesTheCenterChunk()
    {
        var world = new OverworldWorld(0x42494F4D45530001L);
        var context = new WorldGenContext(world, world.Generator, world.LightEngine);
        ChunkPos position = new(3, -5);
        var chunk = new ProtoChunk(
            position,
            LevelHeightAccessor.Create(
                world.GetMinY(),
                world.GetHeight()),
            PalettedContainerFactory.CreateBuiltIn());
        StaticCache2D<ChunkAccess?> cache =
            StaticCache2D<ChunkAccess?>.Create(
                position.X,
                position.Z,
                0,
                (_, _) => chunk);

        ChunkAccess result = ChunkPyramid.GenerationPyramid
            .GetStepTo(ChunkStatus.BIOMES)
            .Apply(context, cache, chunk)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        Require(
            ReferenceEquals(result, chunk) &&
            ReferenceEquals(chunk.GetPersistedStatus(), ChunkStatus.BIOMES),
            "The BIOMES task must return the center proto chunk after populating its BIOMES stage.");
        int biomeCells = 0;
        foreach (ChunkSection section in chunk.GetSections())
            section.GetBiomes().GetAll(_ => biomeCells++);

        Require(
            biomeCells > 0,
            "The generated chunk must contain populated quart biome palette entries.");
    }

    private static void CheckBiomeTaskRejectsAnUnrelatedCache()
    {
        var world = new OverworldWorld(0x42494F4D45530002L);
        var context = new WorldGenContext(world, world.Generator, world.LightEngine);
        ChunkPos position = new(9, 11);
        var center = CreateChunk(world, position);
        var unrelated = CreateChunk(world, ChunkPos.Zero);
        StaticCache2D<ChunkAccess?> cache =
            StaticCache2D<ChunkAccess?>.Create(
                0,
                0,
                0,
                (_, _) => unrelated);

        RequireThrows<InvalidOperationException>(
            () => ChunkStatusTasks.GenerateBiomes(
                context,
                ChunkPyramid.GenerationPyramid.GetStepTo(ChunkStatus.BIOMES),
                cache,
                center),
            "The biome task must reject a cache that does not contain the exact center chunk.");
    }

    private static void CheckStructureStartsTaskGeneratesAndStoresStart()
    {
        const long seed = 0x535441525453544BL;
        var world = new OverworldWorld(seed, LevelHeightAccessor.Create(-16, 64));
        ChunkPos position = new(-6, 9);
        HolderSet<Biome> allBiomes = HolderSet<Biome>.Direct(
            BuiltInRegistries.Biome.ListElements().Cast<Holder<Biome>>());
        var structure = new StartProbeStructure(allBiomes);
        var placement = new AlwaysStructurePlacement();
        var structureSet = new StructureSet(
            Holder<Structure>.Direct(structure),
            placement);
        Holder<StructureSet> structureSetHolder = Holder<StructureSet>.Direct(structureSet);
        ChunkGeneratorStructureState structureState = world.Generator.CreateStructureState(
            ChunkPos.Zero,
            new[] { structureSetHolder });
        var context = new WorldGenContext(
            world,
            world.Generator,
            world.LightEngine,
            structureState);
        var chunk = CreateChunk(world, position);
        StaticCache2D<ChunkAccess?> cache = StaticCache2D<ChunkAccess?>.Create(
            position.X,
            position.Z,
            0,
            (_, _) => chunk);

        ChunkAccess result = ChunkPyramid.GenerationPyramid
            .GetStepTo(ChunkStatus.STRUCTURE_STARTS)
            .Apply(context, cache, chunk)
            .AsTask()
            .GetAwaiter()
            .GetResult();
        StructureStart? storedStart = chunk.GetStartForStructure(structure);

        Require(
            ReferenceEquals(result, chunk) &&
            ReferenceEquals(chunk.GetPersistedStatus(), ChunkStatus.STRUCTURE_STARTS),
            "The STRUCTURE_STARTS task must return and promote its center ProtoChunk.");
        Require(
            placement.QueryCount == 1 &&
            structure.GenerationCallCount == 1 &&
            storedStart is not null &&
            storedStart.IsValid() &&
            ReferenceEquals(storedStart.GetStructure(), structure) &&
            storedStart.GetChunkPos() == position &&
            storedStart.GetReferences() == 0 &&
            storedStart.GetPieces().Count == 1,
            "The task must evaluate the set placement, generate one valid structure start and store its pieces under the structure key.");

        var emptyWorld = new OverworldWorld(seed + 1, LevelHeightAccessor.Create(-16, 64));
        var emptyContext = new WorldGenContext(
            emptyWorld, emptyWorld.Generator, emptyWorld.LightEngine);
        var emptyChunk = CreateChunk(emptyWorld, position);
        StaticCache2D<ChunkAccess?> emptyCache = StaticCache2D<ChunkAccess?>.Create(
            position.X, position.Z, 0, (_, _) => emptyChunk);
        ChunkAccess emptyResult = ChunkPyramid.GenerationPyramid
            .GetStepTo(ChunkStatus.STRUCTURE_STARTS)
            .Apply(emptyContext, emptyCache, emptyChunk)
            .AsTask()
            .GetAwaiter()
            .GetResult();
        Require(
            ReferenceEquals(emptyResult, emptyChunk) &&
            ReferenceEquals(emptyChunk.GetPersistedStatus(), ChunkStatus.STRUCTURE_STARTS) &&
            emptyChunk.GetAllStarts().Count == 0,
            "An Overworld with no registered StructureSets must complete the stage without fabricating structure starts.");

        var unrelated = CreateChunk(world, ChunkPos.Zero);
        StaticCache2D<ChunkAccess?> unrelatedCache = StaticCache2D<ChunkAccess?>.Create(
            0, 0, 0, (_, _) => unrelated);
        RequireThrows<InvalidOperationException>(
            () => ChunkStatusTasks.GenerateStructureStarts(
                context,
                ChunkPyramid.GenerationPyramid.GetStepTo(ChunkStatus.STRUCTURE_STARTS),
                unrelatedCache,
                chunk),
            "The structure-start task must reject a cache that does not contain its exact center chunk.");
    }
    private static void CheckStructureReferencesTaskCollectsIntersectingStarts()
    {
        const long seed = 0x5354525543545246L;
        const int radius = ChunkStatus.MAX_STRUCTURE_DISTANCE;
        var world = new OverworldWorld(seed, LevelHeightAccessor.Create(-16, 64));
        var context = new WorldGenContext(world, world.Generator, world.LightEngine);
        LevelHeightAccessor heightAccessor = LevelHeightAccessor.Create(-16, 64);
        ProtoChunk CreateReferenceChunk(ChunkPos position) =>
            new(position, heightAccessor, PalettedContainerFactory.CreateBuiltIn());

        ChunkPos centerPosition = new(-2, 3);
        var chunks = new Dictionary<ChunkPos, ProtoChunk>();

        foreach (ChunkPos position in ChunkPos.RangeClosed(centerPosition, radius))
        {
            ProtoChunk created = CreateReferenceChunk(position);
            created.SetPersistedStatus(ChunkStatus.STRUCTURE_STARTS);
            chunks.Add(position, created);
        }

        ProtoChunk center = chunks[centerPosition];
        var firstStructure = new StartProbeStructure(
            HolderSet<Biome>.Direct(Biomes.PLAINS.BuiltInRegistryHolder));
        var secondStructure = new StartProbeStructure(
            HolderSet<Biome>.Direct(Biomes.DESERT.BuiltInRegistryHolder));
        var outsideStructure = new StartProbeStructure(
            HolderSet<Biome>.Direct(Biomes.PLAINS.BuiltInRegistryHolder));
        var invalidStructure = new StartProbeStructure(
            HolderSet<Biome>.Direct(Biomes.DESERT.BuiltInRegistryHolder));

        ChunkPos eastEdgeSource = new(centerPosition.X + radius, centerPosition.Z);
        chunks[eastEdgeSource].SetStartForStructure(
            firstStructure,
            CreateStart(firstStructure, eastEdgeSource,
                new BoundingBox(
                    centerPosition.GetMaxBlockX(),
                    200,
                    centerPosition.GetMinBlockZ() + 4,
                    150,
                    210,
                    centerPosition.GetMinBlockZ() + 9)));

        ChunkPos northEdgeSource = new(centerPosition.X - radius, centerPosition.Z + radius);
        chunks[northEdgeSource].SetStartForStructure(
            firstStructure,
            CreateStart(firstStructure, northEdgeSource,
                new BoundingBox(-140, 200, centerPosition.GetMaxBlockZ(),
                    centerPosition.GetMinBlockX(), 210, 140)));

        chunks[centerPosition].SetStartForStructure(
            secondStructure,
            CreateStart(secondStructure, centerPosition,
                new BoundingBox(centerPosition.GetMinBlockX() + 1, 200,
                    centerPosition.GetMinBlockZ() + 1, centerPosition.GetMinBlockX() + 4,
                    210, centerPosition.GetMinBlockZ() + 4)));

        ChunkPos outsideSource = new(centerPosition.X + 4, centerPosition.Z - 4);
        chunks[outsideSource].SetStartForStructure(
            outsideStructure,
            CreateStart(outsideStructure, outsideSource,
                new BoundingBox(centerPosition.GetMaxBlockX() + 1, -16, 4,
                    centerPosition.GetMaxBlockX() + 5, 16, 9)));
        chunks[new ChunkPos(centerPosition.X, centerPosition.Z + 1)]
            .SetStartForStructure(invalidStructure, StructureStart.INVALID_START);

        StaticCache2D<ChunkAccess?> cache = StaticCache2D<ChunkAccess?>.Create(
            centerPosition.X,
            centerPosition.Z,
            radius,
            (x, z) => chunks[new ChunkPos(x, z)]);

        ChunkAccess result = ChunkPyramid.GenerationPyramid
            .GetStepTo(ChunkStatus.STRUCTURE_REFERENCES)
            .Apply(context, cache, center)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        ISet<long> firstReferences = center.GetReferencesForStructure(firstStructure);
        ISet<long> secondReferences = center.GetReferencesForStructure(secondStructure);
        Require(
            ReferenceEquals(result, center) &&
            ReferenceEquals(center.GetPersistedStatus(), ChunkStatus.STRUCTURE_REFERENCES),
            "The STRUCTURE_REFERENCES task must complete and promote the center proto chunk.");
        Require(
            firstReferences.SetEquals(new[] { eastEdgeSource.Pack(), northEdgeSource.Pack() }) &&
            secondReferences.SetEquals(new[] { centerPosition.Pack() }) &&
            center.GetReferencesForStructure(outsideStructure).Count == 0 &&
            center.GetReferencesForStructure(invalidStructure).Count == 0,
            "Reference collection must include both radius-eight edge starts and the center start, while excluding non-intersecting and invalid starts.");
        Require(
            chunks[eastEdgeSource].GetStartForStructure(firstStructure)!.GetReferences() == 0 &&
            chunks[northEdgeSource].GetStartForStructure(firstStructure)!.GetReferences() == 0,
            "Creating chunk references must store source chunk positions without incrementing StructureStart's separate reference counter.");

        _ = ChunkStatusTasks.GenerateStructureReferences(
            context,
            ChunkPyramid.GenerationPyramid.GetStepTo(ChunkStatus.STRUCTURE_REFERENCES),
            cache,
            center);
        Require(
            firstReferences.Count == 2 && secondReferences.Count == 1,
            "Repeating reference generation must leave the center chunk's reference sets deduplicated.");

        var missingCenter = CreateReferenceChunk(centerPosition);
        missingCenter.SetPersistedStatus(ChunkStatus.STRUCTURE_STARTS);
        StaticCache2D<ChunkAccess?> undersizedCache = StaticCache2D<ChunkAccess?>.Create(
            centerPosition.X, centerPosition.Z, radius - 1,
            (x, z) => x == centerPosition.X && z == centerPosition.Z
                ? missingCenter
                : chunks[new ChunkPos(x, z)]);
        RequireThrows<InvalidOperationException>(
            () => ChunkStatusTasks.GenerateStructureReferences(
                context,
                ChunkPyramid.GenerationPyramid.GetStepTo(ChunkStatus.STRUCTURE_REFERENCES),
                undersizedCache,
                missingCenter),
            "Structure-reference generation must fail when its radius-eight dependency window is incomplete.");

    }
    private static ProtoChunk CreateChunk(OverworldWorld world, ChunkPos position) =>
        new(
            position,
            LevelHeightAccessor.Create(
                world.Generator.GetMinY(),
                world.Generator.GetGenDepth()),
            PalettedContainerFactory.CreateBuiltIn());

    private static StructureStart CreateStart(
        Structure structure,
        ChunkPos sourcePosition,
        BoundingBox boundingBox) =>
        new(
            structure,
            sourcePosition,
            references: 0,
            new PiecesContainer(new[] { new TestPiece(boundingBox) }));
    private sealed class StartProbeStructure : Structure
    {
        public int GenerationCallCount { get; private set; }

        public StartProbeStructure(HolderSet<Biome> biomes)
            : base(new StructureSettings(biomes))
        {
        }

        protected override Structure.GenerationStub? FindGenerationPoint(
            Structure.GenerationContext context)
        {
            GenerationCallCount++;
            int x = context.ChunkPos.GetMiddleBlockX();
            int z = context.ChunkPos.GetMiddleBlockZ();
            return new Structure.GenerationStub(
                new BlockPos(x, 0, z),
                builder => builder.AddPiece(new TestPiece(
                    new BoundingBox(x - 2, -2, z - 2, x + 2, 2, z + 2))));
        }
    }

    private sealed class TestPiece(BoundingBox boundingBox)
        : StructurePiece(0, boundingBox);

    private sealed class AlwaysStructurePlacement : StructurePlacement
    {
        public int QueryCount { get; private set; }

        public bool IsStructureChunk(
            IStructurePlacementState state,
            int sourceX,
            int sourceZ)
        {
            QueryCount++;
            return true;
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
