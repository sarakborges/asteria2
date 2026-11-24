using Asteria.Content.Chunks;
using Asteria.Core;

namespace Asteria.World.Lighting;

/// <summary>
/// Receives block and chunk lifecycle events that require light propagation.
/// </summary>
public interface LightEventListener
{
    void CheckBlock(BlockPos position);

    bool HasLightWork();

    int RunLightUpdates();

    void UpdateSectionStatus(SectionPos sectionPosition, bool sectionEmpty);

    void UpdateSectionStatus(BlockPos position, bool sectionEmpty) =>
        UpdateSectionStatus(SectionPos.Of(position), sectionEmpty);

    void SetLightEnabled(ChunkPos position, bool enabled);

    void PropagateLightSources(ChunkPos position);
}
