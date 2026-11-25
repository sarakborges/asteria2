using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Util;
using Asteria.World;
using Asteria.World.Level;
using Asteria.World.LevelGen;

internal static class ChunkPyramidChecks
{
    public static void Run()
    {
        CheckGenerationPyramid();
        CheckLoadingPyramid();
        CheckBuilderValidation();
        CheckChunkStepExecution();
        CheckMaximumChunkCoordinate();
    }

    private static void CheckGenerationPyramid()
    {
        ChunkPyramid pyramid = ChunkPyramid.GenerationPyramid;
        IReadOnlyList<ChunkStatus> statuses = ChunkStatus.GetStatusList();

        Require(
            pyramid.Steps.Count == statuses.Count,
            "The generation pyramid must contain one step for each registered chunk status.");

        for (int index = 0; index < statuses.Count; index++)
        {
            Require(
                ReferenceEquals(pyramid.GetStepTo(statuses[index]).TargetStatus, statuses[index]),
                "Generation-pyramid steps must be indexed in the exact registered status order.");
        }

        ChunkStep references = pyramid.GetStepTo(ChunkStatus.STRUCTURE_REFERENCES);
        Require(
            references.DirectDependencies.Size() == 9 &&
            references.DirectDependencies.AsList().All(status =>
                ReferenceEquals(status, ChunkStatus.STRUCTURE_STARTS)),
            "Structure references must require structure starts at distance zero through eight.");

        ChunkStep terrain = pyramid.GetStepTo(ChunkStatus.TERRAIN);
        Require(
            terrain.BlockStateWriteRadius == 0 &&
            terrain.DirectDependencies.Size() == 9 &&
            ReferenceEquals(terrain.DirectDependencies.Get(0), ChunkStatus.BIOMES) &&
            ReferenceEquals(terrain.DirectDependencies.Get(1), ChunkStatus.BIOMES) &&
            terrain.DirectDependencies.AsList().Skip(2).All(status =>
                ReferenceEquals(status, ChunkStatus.STRUCTURE_STARTS)),
            "Terrain must require biomes within radius one, structure starts within radius eight, and write only to its center chunk.");

        ChunkStep features = pyramid.GetStepTo(ChunkStatus.FEATURES);
        Require(
            features.BlockStateWriteRadius == 1 &&
            ReferenceEquals(features.DirectDependencies.Get(0), ChunkStatus.TERRAIN) &&
            ReferenceEquals(features.DirectDependencies.Get(1), ChunkStatus.TERRAIN) &&
            features.DirectDependencies.AsList().Skip(2).All(status =>
                ReferenceEquals(status, ChunkStatus.STRUCTURE_STARTS)),
            "Features must require terrain within radius one, structure starts within radius eight, and may write within radius one.");

        ChunkStep full = pyramid.GetStepTo(ChunkStatus.FULL);
        ChunkStatus[] expectedFullDependencies =
        {
            ChunkStatus.LIGHT,
            ChunkStatus.INITIALIZE_LIGHT,
            ChunkStatus.TERRAIN,
            ChunkStatus.BIOMES,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS
        };

        Require(
            full.AccumulatedDependencies.AsList().SequenceEqual(expectedFullDependencies),
            "The generation FULL step must reproduce the exact 12-ring Minecraft 26.3 accumulated dependency window.");

        Require(
            full.GetAccumulatedRadiusOf(ChunkStatus.EMPTY) == 11 &&
            full.GetAccumulatedRadiusOf(ChunkStatus.STRUCTURE_STARTS) == 11 &&
            full.GetAccumulatedRadiusOf(ChunkStatus.STRUCTURE_REFERENCES) == 3 &&
            full.GetAccumulatedRadiusOf(ChunkStatus.BIOMES) == 3 &&
            full.GetAccumulatedRadiusOf(ChunkStatus.TERRAIN) == 2 &&
            full.GetAccumulatedRadiusOf(ChunkStatus.FEATURES) == 1 &&
            full.GetAccumulatedRadiusOf(ChunkStatus.INITIALIZE_LIGHT) == 1 &&
            full.GetAccumulatedRadiusOf(ChunkStatus.LIGHT) == 0 &&
            full.GetAccumulatedRadiusOf(ChunkStatus.FULL) == 0,
            "Generation FULL accumulated radii must match the vanilla 26.3 dependency pyramid, including the center-status shortcut.");

        Require(
            full.BlockStateWriteRadius == -1 &&
            pyramid.GetStepTo(ChunkStatus.EMPTY).GetAccumulatedRadiusOf(ChunkStatus.EMPTY) == 0,
            "Unconfigured write radius must remain -1, while a step always reports its own status at radius zero.");
    }

    private static void CheckLoadingPyramid()
    {
        ChunkPyramid pyramid = ChunkPyramid.LoadingPyramid;
        ChunkStatus[] expectedFullDependencies =
        {
            ChunkStatus.LIGHT,
            ChunkStatus.INITIALIZE_LIGHT
        };

        Require(
            pyramid.Steps.Count == ChunkStatus.GetStatusList().Count,
            "The mob-free loading pyramid must contain all nine chunk statuses.");

        Require(
            pyramid.GetStepTo(ChunkStatus.FULL).AccumulatedDependencies.AsList()
                .SequenceEqual(expectedFullDependencies),
            "The loading FULL step must require LIGHT at the center and INITIALIZE_LIGHT at distance one after omitting mob spawning.");

        Require(
            ReferenceEquals(
                pyramid.GetStepTo(ChunkStatus.LIGHT).DirectDependencies.Get(0),
                ChunkStatus.INITIALIZE_LIGHT) &&
            ReferenceEquals(
                pyramid.GetStepTo(ChunkStatus.LIGHT).DirectDependencies.Get(1),
                ChunkStatus.INITIALIZE_LIGHT),
            "Loading LIGHT must require INITIALIZE_LIGHT at distances zero and one.");
    }

    private static void CheckBuilderValidation()
    {
        RequireThrows<ArgumentException>(
            () => _ = new ChunkPyramid.Builder().Step(
                ChunkStatus.STRUCTURE_STARTS,
                static builder => builder),
            "A chunk pyramid must begin at the root chunk status.");

        RequireThrows<ArgumentException>(
            () => _ = new ChunkStep.Builder(
                ChunkStatus.BIOMES,
                ChunkPyramid.GenerationPyramid.GetStepTo(ChunkStatus.EMPTY)),
            "A child ChunkStep builder must receive the immediately preceding status.");

        var terrainBuilder = new ChunkStep.Builder(
            ChunkStatus.TERRAIN,
            ChunkPyramid.GenerationPyramid.GetStepTo(ChunkStatus.BIOMES));
        RequireThrows<ArgumentException>(
            () => terrainBuilder.AddRequirement(ChunkStatus.FULL, 0),
            "A chunk step cannot require a status at or after its own target status.");

        RequireThrows<ArgumentNullException>(
            () => ChunkPyramid.GenerationPyramid.GetStepTo(null!),
            "A null status cannot select a chunk pyramid step.");
    }

    private static void CheckChunkStepExecution()
    {
        const long seed = 0x4348554E4B535445L;
        var world = new OverworldWorld(
            seed,
            LevelHeightAccessor.Create(-16, 48));
        var context = new WorldGenContext(
            world,
            world.Generator,
            world.LightEngine);
        var chunk = new ProtoChunk(
            ChunkPos.Zero,
            LevelHeightAccessor.Create(-16, 48),
            PalettedContainerFactory.CreateBuiltIn());
        StaticCache2D<ChunkAccess?> cache = StaticCache2D<ChunkAccess?>.Create(
            centerX: 0,
            centerZ: 0,
            range: 0,
            (_, _) => chunk);

        int invocationCount = 0;
        var builder = new ChunkStep.Builder(
            ChunkStatus.STRUCTURE_STARTS,
            ChunkPyramid.GenerationPyramid.GetStepTo(ChunkStatus.EMPTY));
        builder.SetTask((taskContext, step, availableChunks, centerChunk) =>
        {
            invocationCount++;
            Require(
                ReferenceEquals(taskContext, context) &&
                ReferenceEquals(availableChunks, cache) &&
                ReferenceEquals(centerChunk, chunk),
                "ChunkStep.Apply must pass the exact world-generation context and resolved dependency snapshot to its task.");

            WorldGenRegion region = taskContext.CreateWorldGenRegion(
                step,
                centerChunk,
                availableChunks);
            Require(
                ReferenceEquals(region.GetChunk(0, 0), centerChunk),
                "WorldGenContext must build the task region from the supplied center chunk.");

            return ValueTask.FromResult(centerChunk);
        });

        ChunkStep step = builder.Build();
        ChunkAccess generated = step.Apply(context, cache, chunk)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        Require(
            invocationCount == 1 &&
            ReferenceEquals(generated, chunk) &&
            ReferenceEquals(chunk.GetPersistedStatus(), ChunkStatus.STRUCTURE_STARTS),
            "ChunkStep.Apply must promote a returned ProtoChunk to the target status after its task succeeds.");

        _ = step.Apply(context, cache, chunk)
            .AsTask()
            .GetAwaiter()
            .GetResult();
        Require(
            invocationCount == 2 &&
            ReferenceEquals(chunk.GetPersistedStatus(), ChunkStatus.STRUCTURE_STARTS),
            "A step task must still execute for a chunk already at its target status without changing that status.");

        var failedChunk = new ProtoChunk(
            ChunkPos.Zero,
            LevelHeightAccessor.Create(-16, 48),
            PalettedContainerFactory.CreateBuiltIn());
        failedChunk.SetPersistedStatus(ChunkStatus.STRUCTURE_STARTS);
        StaticCache2D<ChunkAccess?> failedCache = StaticCache2D<ChunkAccess?>.Create(
            centerX: 0,
            centerZ: 0,
            range: 0,
            (_, _) => failedChunk);
        var failingBuilder = new ChunkStep.Builder(
            ChunkStatus.STRUCTURE_REFERENCES,
            ChunkPyramid.GenerationPyramid.GetStepTo(ChunkStatus.STRUCTURE_STARTS));
        failingBuilder.SetTask(static (_, _, _, _) =>
            ValueTask.FromException<ChunkAccess>(
                new InvalidOperationException("chunk-step-fixture-failure")));
        ChunkStep failingStep = failingBuilder.Build();

        bool taskFailureObserved = false;
        try
        {
            _ = failingStep.Apply(context, failedCache, failedChunk)
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }
        catch (InvalidOperationException exception)
        {
            taskFailureObserved = exception.Message == "chunk-step-fixture-failure";
        }

        Require(
            taskFailureObserved &&
            ReferenceEquals(failedChunk.GetPersistedStatus(), ChunkStatus.STRUCTURE_STARTS),
            "A failed ChunkStep task must preserve the original persisted status and propagate the task failure.");

        var unassignedChunk = new ProtoChunk(
            ChunkPos.Zero,
            LevelHeightAccessor.Create(-16, 48),
            PalettedContainerFactory.CreateBuiltIn());
        StaticCache2D<ChunkAccess?> unassignedCache = StaticCache2D<ChunkAccess?>.Create(
            centerX: 0,
            centerZ: 0,
            range: 0,
            (_, _) => unassignedChunk);

        bool unassignedTaskRejected = false;
        try
        {
            _ = ChunkPyramid.GenerationPyramid
                .GetStepTo(ChunkStatus.STRUCTURE_STARTS)
                .Apply(context, unassignedCache, unassignedChunk)
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }
        catch (InvalidOperationException exception)
        {
            unassignedTaskRejected = exception.Message.Contains(
                "No chunk-generation task is registered",
                StringComparison.Ordinal);
        }

        Require(
            unassignedTaskRejected &&
            ReferenceEquals(unassignedChunk.GetPersistedStatus(), ChunkStatus.EMPTY),
            "A pyramid step without a ported task must fail closed instead of falsely promoting a chunk.");
    }

    private static void CheckMaximumChunkCoordinate()
    {
        int safetyMargin = (32 + ChunkPyramid.GenerationPyramid
            .GetStepTo(ChunkStatus.FULL).AccumulatedDependencies.Size() + 1) * 2;
        int expected = SectionPos.BlockToSectionCoord(BlockPos.MaxHorizontalCoordinate) - safetyMargin;

        Require(
            ChunkPyramid.MaxChunkCoordinateValue == expected &&
            expected == 2_097_061,
            "The maximum chunk-coordinate bound must preserve the generation pyramid's vanilla safety margin.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}
