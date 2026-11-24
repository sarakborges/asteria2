using Asteria.Content.Blocks;
using Asteria.World;

namespace Asteria.Content.Chunks;

/// <summary>
/// Chunk data required by the light propagation engine.
/// </summary>
public interface LightChunk : BlockGetter
{
    void FindBlockLightSources(Action<BlockPos, BlockState> consumer);

    ChunkSkyLightSources GetSkyLightSources();
}
