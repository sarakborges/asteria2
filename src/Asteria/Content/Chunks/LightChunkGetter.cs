using Asteria.Core;
using Asteria.World;

namespace Asteria.Content.Chunks;

/// <summary>
/// Supplies already available chunks and update notifications to the light engine.
/// Lookup must not force chunk generation.
/// </summary>
public interface LightChunkGetter
{
    LightChunk? GetChunkForLighting(int chunkX, int chunkZ);

    BlockGetter GetLevel();

    void OnLightUpdate(LightLayer layer, SectionPos position)
    {
    }
}
