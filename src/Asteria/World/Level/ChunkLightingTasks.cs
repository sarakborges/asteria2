using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Core;
using Asteria.World.Lighting;

namespace Asteria.World.Level;

/// <summary>
/// Synchronous single-player equivalent of Minecraft 26.3's chunk lighting tasks.
/// The caller remains responsible for advancing chunk statuses and satisfying the
/// LIGHT step's one-chunk initialization dependency radius.
/// </summary>
public static class ChunkLightingTasks
{
    /// <summary>
    /// Builds sky-source metadata and registers every non-empty section with the
    /// level light engine. The engine's update batch is completed before the column
    /// enabled flag and queued-data retention are changed, matching PRE/POST ordering.
    /// </summary>
    public static void InitializeLight(
        ProtoChunk chunk,
        LevelLightEngine lightEngine,
        bool lighted)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(lightEngine);

        if (!chunk.GetPersistedStatus().IsOrAfter(ChunkStatus.FEATURES))
        {
            throw new InvalidOperationException(
                $"Chunk {chunk.GetPos()} must reach FEATURES before lighting initialization.");
        }

        chunk.InitializeLightSources();
        chunk.SetLightEngine(lightEngine);

        ChunkPos chunkPosition = chunk.GetPos();
        ChunkSection[] sections = chunk.GetSections();
        for (int sectionIndex = 0; sectionIndex < chunk.GetSectionsCount(); sectionIndex++)
        {
            ChunkSection section = sections[sectionIndex];
            if (!section.HasOnlyAir)
            {
                int sectionY = chunk.GetSectionYFromSectionIndex(sectionIndex);
                lightEngine.UpdateSectionStatus(
                    SectionPos.Of(chunkPosition, sectionY),
                    sectionEmpty: false);
            }
        }

        // ThreadedLevelLightEngine.runUpdate executes PRE tasks, one light update
        // batch, and only then POST tasks. This local world has no light executor,
        // so preserve the same order directly.
        lightEngine.RunLightUpdates();
        lightEngine.SetLightEnabled(chunkPosition, lighted);
        lightEngine.RetainData(chunkPosition, retain: false);
    }

    /// <summary>
    /// Removes one or more no-longer-retained chunk columns from the lighting engine.
    /// This mirrors ThreadedLevelLightEngine.updateChunkStatus: stop retaining data,
    /// disable the column, clear queued section layers including vertical padding, and
    /// remove the world's section states before publishing one final update batch.
    /// </summary>
    public static void UnloadLight(
        IEnumerable<ProtoChunk> chunks,
        LevelLightEngine lightEngine)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(lightEngine);

        ProtoChunk[] snapshot = chunks.ToArray();
        if (Array.Exists(snapshot, static chunk => chunk is null))
            throw new ArgumentException("Chunk collections cannot contain null entries.", nameof(chunks));

        bool queuedAny = false;
        foreach (ProtoChunk chunk in snapshot)
        {
            if (!chunk.GetPersistedStatus().IsOrAfter(ChunkStatus.INITIALIZE_LIGHT))
                continue;

            ChunkPos position = chunk.GetPos();
            lightEngine.RetainData(position, retain: false);
            lightEngine.SetLightEnabled(position, enabled: false);

            for (int sectionY = lightEngine.GetMinLightSection();
                 sectionY < lightEngine.GetMaxLightSection();
                 sectionY++)
            {
                SectionPos sectionPosition = SectionPos.Of(position, sectionY);
                lightEngine.QueueSectionData(LightLayer.BLOCK, sectionPosition, data: null);
                lightEngine.QueueSectionData(LightLayer.SKY, sectionPosition, data: null);
            }

            for (int sectionY = chunk.GetMinSectionY();
                 sectionY <= chunk.GetMaxSectionY();
                 sectionY++)
            {
                lightEngine.UpdateSectionStatus(
                    SectionPos.Of(position, sectionY),
                    sectionEmpty: true);
            }

            queuedAny = true;
        }

        if (queuedAny)
            lightEngine.RunLightUpdates();
    }

    /// <summary>
    /// Seeds and computes both available light channels for a chunk. The chunk's
    /// one-chunk neighbour ring must already have reached INITIALIZE_LIGHT, as
    /// required by ChunkPyramid's LIGHT step. Status promotion remains the scheduler's job.
    /// </summary>
    public static void LightChunk(
        ProtoChunk centerChunk,
        LevelLightEngine lightEngine,
        bool lighted)
    {
        ArgumentNullException.ThrowIfNull(centerChunk);
        ArgumentNullException.ThrowIfNull(lightEngine);

        if (!centerChunk.GetPersistedStatus().IsOrAfter(ChunkStatus.INITIALIZE_LIGHT))
        {
            throw new InvalidOperationException(
                $"Chunk {centerChunk.GetPos()} must reach INITIALIZE_LIGHT before lighting.");
        }

        centerChunk.SetLightCorrect(false);

        if (!lighted)
            lightEngine.PropagateLightSources(centerChunk.GetPos());

        // The vanilla light task publishes correctness only after the PRE batch
        // has propagated and the engine's update map has been swapped.
        lightEngine.RunLightUpdates();
        centerChunk.SetLightCorrect(true);
    }
}
