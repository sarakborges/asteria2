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
                    step.TargetStatus != ChunkStatus.STRUCTURE_STARTS)
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
    private static ProtoChunk CreateChunk(OverworldWorld world, ChunkPos position) =>
        new(
            position,
            LevelHeightAccessor.Create(
                world.Generator.GetMinY(),
                world.Generator.GetGenDepth()),
            PalettedContainerFactory.CreateBuiltIn());

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
