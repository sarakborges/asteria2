using Asteria.Content.Chunks;
using Asteria.World;
using Asteria.Content.Chunks.Status;
using Asteria.Core;
using Asteria.World.Level;

internal static class OverworldWorldChecks
{
    public static void Run()
    {
        CheckGetChunkRunsTheGenerationPyramid();
        CheckGenerationCachePruning();
    }

    private static void CheckGetChunkRunsTheGenerationPyramid()
    {
        const long seed = 0x4F5652574F524C44L;
        LevelHeightAccessor heightAccessor = LevelHeightAccessor.Create(-16, 48);
        var world = new OverworldWorld(seed, heightAccessor);
        ChunkPos position = new(-4, 7);

        ProtoChunk generated = world.GetChunk(position);
        Require(
            ReferenceEquals(generated.GetPersistedStatus(), ChunkStatus.LIGHT) &&
            generated.IsLightCorrect(),
            "OverworldWorld.GetChunk must execute INITIALIZE_LIGHT and LIGHT before publishing a light-correct chunk.");

        int biomeCells = 0;
        foreach (ChunkSection section in generated.GetSections())
            section.GetBiomes().GetAll(_ => biomeCells++);
        Require(
            biomeCells > 0,
            "The runtime-generated center chunk must contain its populated BIOMES palette.");

        Require(
            world.LoadedChunkCount == 1 &&
            world.GetLoadedChunks().Count == 1 &&
            world.TryGetChunk(position, out ProtoChunk? cached) &&
            ReferenceEquals(cached, generated),
            "Only the light-ready center chunk may be published through the world's loaded-chunk cache.");

        ChunkPos initializedNeighborPosition = new(position.X + 1, position.Z);
        ProtoChunk? initializedNeighbor =
            world.GetChunkForLighting(initializedNeighborPosition.X, initializedNeighborPosition.Z) as ProtoChunk;
        Require(
            !world.TryGetChunk(initializedNeighborPosition, out _) &&
            initializedNeighbor is not null &&
            ReferenceEquals(initializedNeighbor.GetPersistedStatus(), ChunkStatus.INITIALIZE_LIGHT),
            "The light engine must see already generated staging neighbors after FEATURES/INITIALIZE_LIGHT without publishing them to world reads.");

        ChunkPos dependencyPosition = new(position.X + 2, position.Z);
        Require(
            !world.TryGetChunk(dependencyPosition, out _) &&
            world.GetChunkForLighting(dependencyPosition.X, dependencyPosition.Z) is null,
            "The light engine must not see terrain-only staging chunks or trigger their generation.");

        Require(
            ReferenceEquals(world.GetChunk(position), generated) &&
            world.LoadedChunkCount == 1,
            "A repeated light-ready chunk request must return the cached instance without republishing dependencies.");

        Require(
            world.UnloadChunk(position) &&
            !world.TryGetChunk(position, out _),
            "Unloading a feature-ready chunk must remove it from both public and generation caches.");

        ProtoChunk regenerated = world.GetChunk(position);
        Require(
            !ReferenceEquals(regenerated, generated) &&
            ReferenceEquals(regenerated.GetPersistedStatus(), ChunkStatus.LIGHT) &&
            regenerated.IsLightCorrect() &&
            world.LoadedChunkCount == 1,
            "An unloaded center chunk must be regenerated through LIGHT before it becomes visible again.");
    }

    private static void CheckGenerationCachePruning()
    {
        const long seed = 0x4341434845505255L;
        LevelHeightAccessor heightAccessor = LevelHeightAccessor.Create(-16, 48);
        var world = new OverworldWorld(seed, heightAccessor);
        ChunkPos firstPosition = new(-10, 4);
        ChunkPos adjacentPosition = new(firstPosition.X + 1, firstPosition.Z);

        ProtoChunk first = world.GetChunk(firstPosition);
        Require(
            world.CachedGenerationChunkCount == 23 * 23,
            "One loaded light-ready chunk must retain exactly its 23-by-23 generation and lighting dependency window.");

        ProtoChunk adjacent = world.GetChunk(adjacentPosition);
        Require(
            world.LoadedChunkCount == 2 &&
            world.CachedGenerationChunkCount == 24 * 23,
            "Adjacent loaded chunks must share their overlapping 23-by-23 generation and lighting dependency windows.");

        Require(
            world.UnloadChunk(firstPosition) &&
            world.LoadedChunkCount == 1 &&
            world.CachedGenerationChunkCount == 23 * 23 &&
            world.TryGetChunk(adjacentPosition, out ProtoChunk? cachedAdjacent) &&
            ReferenceEquals(cachedAdjacent, adjacent),
            "Unloading one chunk must retain exactly the 21-by-21 dependency window still required by its loaded neighbor.");

        Require(
            ReferenceEquals(world.GetChunk(firstPosition), first) &&
            world.LoadedChunkCount == 2,
            "A feature-ready chunk retained as a loaded neighbor's dependency must be safely reusable.");

        Require(
            world.UnloadChunk(firstPosition) &&
            world.UnloadChunk(adjacentPosition) &&
            world.LoadedChunkCount == 0 &&
            world.CachedGenerationChunkCount == 0,
            "Unloading the final public chunk must release every intermediate generation chunk.");

        ProtoChunk regenerated = world.GetChunk(firstPosition);
        Require(
            !ReferenceEquals(regenerated, first) &&
            ReferenceEquals(regenerated.GetPersistedStatus(), ChunkStatus.LIGHT) &&
            regenerated.IsLightCorrect() &&
            world.CachedGenerationChunkCount == 23 * 23,
            "A chunk requested after its entire dependency window was released must be regenerated.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
