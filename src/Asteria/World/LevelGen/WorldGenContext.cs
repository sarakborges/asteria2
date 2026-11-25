using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Util;
using Asteria.World.Level;
using Asteria.World.Lighting;
using Asteria.World.LevelGen.Structure;

namespace Asteria.World.LevelGen;

/// <summary>
/// The currently available world-generation services for Asteria's synchronous local runtime.
/// Structure-template management, a main-thread executor and a LevelChunk save listener are
/// intentionally absent until their corresponding systems are ported.
/// </summary>
public sealed class WorldGenContext
{
    public OverworldWorld Level { get; }
    public NoiseBasedChunkGenerator Generator { get; }
    public LevelLightEngine LightEngine { get; }
    public ChunkGeneratorStructureState StructureState => Level.StructureState;

    public WorldGenContext(
        OverworldWorld level,
        NoiseBasedChunkGenerator generator,
        LevelLightEngine lightEngine)
    {
        Level = level ?? throw new ArgumentNullException(nameof(level));
        Generator = generator ?? throw new ArgumentNullException(nameof(generator));
        LightEngine = lightEngine ?? throw new ArgumentNullException(nameof(lightEngine));
    }

    /// <summary>
    /// Creates the stage-scoped region from an already-resolved rectangular chunk snapshot.
    /// The snapshot must contain the center chunk; WorldGenRegion applies the step's
    /// dependency statuses and write radius to the supplied entries.
    /// </summary>
    public WorldGenRegion CreateWorldGenRegion(
        ChunkStep step,
        ChunkAccess center,
        StaticCache2D<ChunkAccess?> chunks)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(center);
        ArgumentNullException.ThrowIfNull(chunks);

        ChunkPos position = center.GetPos();
        if (!chunks.Contains(position.X, position.Z) ||
            !ReferenceEquals(chunks.Get(position.X, position.Z), center))
        {
            throw new InvalidOperationException(
                $"The chunk snapshot does not contain the requested center chunk {position}.");
        }

        var availableChunks = new List<ChunkAccess>();
        chunks.ForEach(chunk =>
        {
            if (chunk is not null)
                availableChunks.Add(chunk);
        });

        return Level.CreateWorldGenRegion(step, center, availableChunks);
    }
}
