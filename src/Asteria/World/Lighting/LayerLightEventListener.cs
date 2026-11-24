using Asteria.Content.Chunks;
using Asteria.Core;

namespace Asteria.World.Lighting;

/// <summary>
/// Light event listener that also exposes the data and sampled values of one light channel.
/// </summary>
public interface LayerLightEventListener : LightEventListener
{
    DataLayer? GetDataLayerData(SectionPos position);

    int GetLightValue(BlockPos position);

    public static class Dummy
    {
        public static LayerLightEventListener Instance { get; } = new DummyListener();

        private sealed class DummyListener : LayerLightEventListener
        {
            public DataLayer? GetDataLayerData(SectionPos position) => null;

            public int GetLightValue(BlockPos position) => 0;

            public void CheckBlock(BlockPos position) { }

            public bool HasLightWork() => false;

            public int RunLightUpdates() => 0;

            public void UpdateSectionStatus(SectionPos sectionPosition, bool sectionEmpty) { }

            public void SetLightEnabled(ChunkPos position, bool enabled) { }

            public void PropagateLightSources(ChunkPos position) { }
        }
    }
}
