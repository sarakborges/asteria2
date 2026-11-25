using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Util;
using Asteria.World.Level;

namespace Asteria.World.LevelGen;

/// <summary>
/// Status-task implementations whose required world-generation dependencies are
/// currently available in Asteria. Unported tasks remain absent from ChunkPyramid
/// and therefore fail closed when requested.
/// </summary>
public static class ChunkStatusTasks
{
    /// <summary>
    /// Port of Minecraft Java 26.3 generateBiomes: populate the target proto chunk's
    /// quart biome palette through the configured chunk generator.
    /// </summary>
    public static ValueTask<ChunkAccess> GenerateBiomes(
        WorldGenContext context,
        ChunkStep step,
        StaticCache2D<ChunkAccess?> chunks,
        ChunkAccess chunk)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(chunk);

        if (!ReferenceEquals(step.TargetStatus, ChunkStatus.BIOMES))
        {
            throw new ArgumentException(
                $"The biome task cannot execute status {step.TargetStatus}.",
                nameof(step));
        }

        ChunkPos position = chunk.GetPos();
        if (!chunks.Contains(position.X, position.Z) ||
            !ReferenceEquals(chunks.Get(position.X, position.Z), chunk))
        {
            throw new InvalidOperationException(
                $"The BIOMES task cache does not contain its center chunk {position}.");
        }

        if (chunk is not ProtoChunk protoChunk)
        {
            throw new InvalidOperationException(
                $"BIOMES generation requires a ProtoChunk, received {chunk.GetType().Name}.");
        }

        context.Generator.CreateBiomes(protoChunk);
        return ValueTask.FromResult<ChunkAccess>(protoChunk);
    }
}
