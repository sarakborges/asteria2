using Asteria.Content.Chunks;
using Asteria.Core;

namespace Asteria.World.Lighting;

/// <summary>
/// Public per-channel light engine surface used by LevelLightEngine.
/// </summary>
public interface LayerLightEngine : LayerLightEventListener
{
    void QueueSectionData(long sectionNode, DataLayer? data);

    void RetainData(ChunkPos position, bool retain);

    string GetDebugData(long sectionNode);

    LightSectionType GetDebugSectionType(long sectionNode);

    bool LightOnInColumn(long sectionZeroNode);
}
