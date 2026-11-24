using Asteria.Content.Chunks;
using Asteria.Core;

namespace Asteria.World.Lighting;

/// <summary>
/// Stores block-emitted light values for sections.
/// Port of Minecraft Java 26.3 BlockLightSectionStorage.
/// </summary>
public class BlockLightSectionStorage : LayerLightSectionStorage<BlockLightSectionStorage.BlockDataLayerStorageMap>
{
    public BlockLightSectionStorage(LightChunkGetter chunkSource)
        : base(
            LightLayer.BLOCK,
            chunkSource,
            new BlockDataLayerStorageMap(new Dictionary<long, DataLayer>()))
    {
    }

    protected override int GetLightValue(long blockNode)
    {
        long sectionNode = SectionPos.BlockToSection(blockNode);
        DataLayer? layer = GetDataLayer(sectionNode, updating: false);
        return layer is null
            ? 0
            : layer.Get(
                SectionPos.SectionRelative(BlockPos.GetX(blockNode)),
                SectionPos.SectionRelative(BlockPos.GetY(blockNode)),
                SectionPos.SectionRelative(BlockPos.GetZ(blockNode)));
    }

    public sealed class BlockDataLayerStorageMap
        : DataLayerStorageMap<BlockDataLayerStorageMap>
    {
        public BlockDataLayerStorageMap(Dictionary<long, DataLayer> map)
            : base(map)
        {
        }

        public override BlockDataLayerStorageMap Copy() =>
            new(new Dictionary<long, DataLayer>(Map));
    }
}
