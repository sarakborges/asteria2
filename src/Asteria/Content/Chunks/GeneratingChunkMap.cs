using Asteria.Content.Chunks.Status;
using Asteria.Util;
using Asteria.World;
using Asteria.World.Level;

namespace Asteria.Content.Chunks;

/// <summary>
/// Minimal generation-map contract used by ChunkGenerationTask and
/// GenerationChunkHolder. Mirrors Minecraft Java 26.3 GeneratingChunkMap.
/// </summary>
public interface GeneratingChunkMap
{
    GenerationChunkHolder AcquireGeneration(long chunkNode);

    Task<ChunkAccess> ApplyStep(
        GenerationChunkHolder chunkHolder,
        ChunkStep step,
        StaticCache2D<GenerationChunkHolder> cache);

    void ReleaseGeneration(GenerationChunkHolder chunkHolder);

    ChunkGenerationTask ScheduleGenerationTask(
        ChunkStatus targetStatus,
        ChunkPos position);

    void RunGenerationTasks();
}
