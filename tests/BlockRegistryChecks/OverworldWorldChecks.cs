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
            ReferenceEquals(generated.GetPersistedStatus(), ChunkStatus.TERRAIN),
            "OverworldWorld.GetChunk must promote its center chunk through the TERRAIN status task.");

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
            "Only the terrain-ready center chunk may be published through the world's loaded-chunk cache.");

        ChunkPos dependencyPosition = new(position.X + 2, position.Z);
        Require(
            !world.TryGetChunk(dependencyPosition, out _) &&
            world.GetChunkForLighting(dependencyPosition.X, dependencyPosition.Z) is null,
            "Intermediate dependency chunks must remain hidden from world reads and lighting until TERRAIN is generated.");

        Require(
            ReferenceEquals(world.GetChunk(position), generated) &&
            world.LoadedChunkCount == 1,
            "A repeated terrain-ready chunk request must return the cached instance without republishing dependencies.");

        Require(
            world.UnloadChunk(position) &&
            !world.TryGetChunk(position, out _),
            "Unloading a terrain-ready chunk must remove it from both public and generation caches.");

        ProtoChunk regenerated = world.GetChunk(position);
        Require(
            !ReferenceEquals(regenerated, generated) &&
            ReferenceEquals(regenerated.GetPersistedStatus(), ChunkStatus.TERRAIN) &&
            world.LoadedChunkCount == 1,
            "An unloaded center chunk must be regenerated through TERRAIN before it becomes visible again.");
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
            world.CachedGenerationChunkCount == 19 * 19,
            "One loaded terrain chunk must retain exactly its 19-by-19 generation dependency window.");

        ProtoChunk adjacent = world.GetChunk(adjacentPosition);
        Require(
            world.LoadedChunkCount == 2 &&
            world.CachedGenerationChunkCount == 20 * 19,
            "Adjacent loaded chunks must share their overlapping generation dependency window.");

        Require(
            world.UnloadChunk(firstPosition) &&
            world.LoadedChunkCount == 1 &&
            world.CachedGenerationChunkCount == 19 * 19 &&
            world.TryGetChunk(adjacentPosition, out ProtoChunk? cachedAdjacent) &&
            ReferenceEquals(cachedAdjacent, adjacent),
            "Unloading one chunk must retain exactly the dependency window still required by its loaded neighbor.");

        Require(
            ReferenceEquals(world.GetChunk(firstPosition), first) &&
            world.LoadedChunkCount == 2,
            "A terrain-ready chunk retained as a loaded neighbor's dependency must be safely reusable.");

        Require(
            world.UnloadChunk(firstPosition) &&
            world.UnloadChunk(adjacentPosition) &&
            world.LoadedChunkCount == 0 &&
            world.CachedGenerationChunkCount == 0,
            "Unloading the final public chunk must release every intermediate generation chunk.");

        ProtoChunk regenerated = world.GetChunk(firstPosition);
        Require(
            !ReferenceEquals(regenerated, first) &&
            world.CachedGenerationChunkCount == 19 * 19,
            "A chunk requested after its entire dependency window was released must be regenerated.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
