using Asteria.Content.Chunks.Status;
using Asteria.World;

namespace Asteria.World.Rendering;

/// <summary>
/// Produces the valid chunk positions covered by a square render region.
/// Positions are ordered from the center outward so a streaming consumer can
/// prioritize the chunk containing the player.
/// </summary>
public static class ChunkRenderRegion
{
    public static IReadOnlyList<ChunkPos> GetChunkPositions(ChunkPos center, int radius)
    {
        ArgumentNullException.ThrowIfNull(center);
        if (!center.IsValid())
            throw new ArgumentOutOfRangeException(nameof(center), center, "The region center must be a valid chunk position.");
        if (radius < 0)
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Render radius cannot be negative.");

        ChunkPos[] positions = ChunkPos.RangeClosed(center, radius)
            .Where(static position => position.IsValid())
            .OrderBy(position => position.DistanceSquared(center))
            .ThenBy(static position => position.X)
            .ThenBy(static position => position.Z)
            .ToArray();

        return Array.AsReadOnly(positions);
    }
}
