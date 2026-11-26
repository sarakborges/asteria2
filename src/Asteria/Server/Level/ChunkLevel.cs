using Asteria.Content.Chunks.Status;

namespace Asteria.Server.Level;

/// <summary>
/// Maps chunk-generation and full-chunk statuses to the distance-manager levels
/// used by the Minecraft Java 26.3 chunk lifecycle.
/// </summary>
public static class ChunkLevel
{
    public const int FULL_CHUNK_LEVEL = 33;
    public const int BLOCK_TICKING_LEVEL = 32;
    public const int ENTITY_TICKING_LEVEL = 31;

    private static readonly ChunkStep FULL_CHUNK_STEP =
        ChunkPyramid.GenerationPyramid.GetStepTo(ChunkStatus.FULL);

    public static readonly int RADIUS_AROUND_FULL_CHUNK =
        FULL_CHUNK_STEP.AccumulatedDependencies.GetRadius();

    public static readonly int MAX_LEVEL =
        FULL_CHUNK_LEVEL + RADIUS_AROUND_FULL_CHUNK;

    public static ChunkStatus? GenerationStatus(int level) =>
        GetStatusAroundFullChunk(level - FULL_CHUNK_LEVEL, null);

    public static ChunkStatus? GetStatusAroundFullChunk(
        int distanceToFullChunk,
        ChunkStatus? defaultValue)
    {
        if (distanceToFullChunk > RADIUS_AROUND_FULL_CHUNK)
            return defaultValue;

        return distanceToFullChunk <= 0
            ? ChunkStatus.FULL
            : FULL_CHUNK_STEP.AccumulatedDependencies.Get(distanceToFullChunk);
    }

    public static ChunkStatus GetStatusAroundFullChunk(int distanceToFullChunk) =>
        GetStatusAroundFullChunk(distanceToFullChunk, ChunkStatus.EMPTY)!;

    public static int ByStatus(ChunkStatus status) =>
        FULL_CHUNK_LEVEL + FULL_CHUNK_STEP.GetAccumulatedRadiusOf(status);

    public static FullChunkStatus FullStatus(int level)
    {
        if (level <= ENTITY_TICKING_LEVEL)
            return FullChunkStatus.ENTITY_TICKING;
        if (level <= BLOCK_TICKING_LEVEL)
            return FullChunkStatus.BLOCK_TICKING;

        return level <= FULL_CHUNK_LEVEL
            ? FullChunkStatus.FULL
            : FullChunkStatus.INACCESSIBLE;
    }

    public static int ByStatus(FullChunkStatus status) =>
        status switch
        {
            FullChunkStatus.INACCESSIBLE => MAX_LEVEL,
            FullChunkStatus.FULL => FULL_CHUNK_LEVEL,
            FullChunkStatus.BLOCK_TICKING => BLOCK_TICKING_LEVEL,
            FullChunkStatus.ENTITY_TICKING => ENTITY_TICKING_LEVEL,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

    public static bool IsEntityTicking(int level) =>
        level <= ENTITY_TICKING_LEVEL;

    public static bool IsBlockTicking(int level) =>
        level <= BLOCK_TICKING_LEVEL;

    public static bool IsLoaded(int level) =>
        level <= MAX_LEVEL;
}
