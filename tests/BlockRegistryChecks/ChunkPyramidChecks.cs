using Asteria.Content.Chunks.Status;
using Asteria.Core;

internal static class ChunkPyramidChecks
{
    public static void Run()
    {
        CheckGenerationPyramid();
        CheckLoadingPyramid();
        CheckBuilderValidation();
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
            ChunkStatus.SPAWN,
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
            full.GetAccumulatedRadiusOf(ChunkStatus.SPAWN) == 0 &&
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
            ChunkStatus.SPAWN,
            ChunkStatus.INITIALIZE_LIGHT
        };

        Require(
            pyramid.Steps.Count == ChunkStatus.GetStatusList().Count,
            "The loading pyramid must contain all ten chunk statuses.");

        Require(
            pyramid.GetStepTo(ChunkStatus.FULL).AccumulatedDependencies.AsList()
                .SequenceEqual(expectedFullDependencies),
            "The loading FULL step must require SPAWN at the center and INITIALIZE_LIGHT at distance one.");

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
