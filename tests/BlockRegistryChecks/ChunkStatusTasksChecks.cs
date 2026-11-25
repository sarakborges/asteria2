using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Content.Registries;
using Asteria.Util;
using Asteria.World;
using Asteria.World.Level;
using Asteria.World.LevelGen;

internal static class ChunkStatusTasksChecks
{
    public static void Run()
    {
        CheckBiomeTaskIsOnlyWiredForGeneration();
        CheckBiomeTaskPopulatesTheCenterChunk();
        CheckBiomeTaskRejectsAnUnrelatedCache();
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
            ChunkPyramid.GenerationPyramid.Steps
                .Where(step => step.TargetStatus is not null)
                .Where(step => step.TargetStatus != ChunkStatus.BIOMES)
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
                world.Generator.GetMinY(),
                world.Generator.GetGenDepth()),
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
        Require(
            chunk.GetSections().All(section => section.GetBiomes()
                .GetAll(_ => { })),
            "Biome palette inspection must complete for every chunk section.");
        Require(
            chunk.GetSections().Any(section => section.GetBiomes()
                .GetAll(_ => { }) is not null),
            "The generated chunk must retain its biome containers.");
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

    private static ProtoChunk CreateChunk(OverworldWorld world, ChunkPos position) =>
        new(
            position,
            LevelHeightAccessor.Create(
                world.Generator.GetMinY(),
                world.Generator.GetGenDepth()),
            PalettedContainerFactory.CreateBuiltIn());

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
