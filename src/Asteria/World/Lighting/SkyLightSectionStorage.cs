using Asteria.Content.Chunks;
using Asteria.Core;

namespace Asteria.World.Lighting;

/// <summary>
/// Stores skylight section data and the highest stored section in each chunk column.
/// Port of Minecraft Java 26.3 SkyLightSectionStorage.
/// </summary>
public class SkyLightSectionStorage : LayerLightSectionStorage<SkyLightSectionStorage.SkyDataLayerStorageMap>
{
    public SkyLightSectionStorage(LightChunkGetter chunkSource)
        : base(
            LightLayer.SKY,
            chunkSource,
            new SkyDataLayerStorageMap(
                new Dictionary<long, DataLayer>(),
                new Dictionary<long, int>(),
                int.MaxValue))
    {
    }

    protected override int GetLightValue(long blockNode) =>
        GetLightValue(blockNode, updating: false);

    protected int GetLightValue(long blockNode, bool updating)
    {
        long sectionNode = SectionPos.BlockToSection(blockNode);
        int sectionY = SectionPos.GetY(sectionNode);
        SkyDataLayerStorageMap sections = updating
            ? UpdatingSectionData
            : VisibleSectionData;

        int topSection = sections.GetTopSectionY(SectionPos.GetZeroNode(sectionNode));
        if (topSection != sections.CurrentLowestY && sectionY < topSection)
        {
            DataLayer? layer = GetDataLayer(sections, sectionNode);
            if (layer is null)
            {
                blockNode = BlockPos.GetFlatIndex(blockNode);

                while (layer is null)
                {
                    if (++sectionY >= topSection)
                        return 15;

                    sectionNode = SectionPos.Offset(sectionNode, Direction.UP);
                    layer = GetDataLayer(sections, sectionNode);
                }
            }

            return layer.Get(
                SectionPos.SectionRelative(BlockPos.GetX(blockNode)),
                SectionPos.SectionRelative(BlockPos.GetY(blockNode)),
                SectionPos.SectionRelative(BlockPos.GetZ(blockNode)));
        }

        return updating && !LightOnInSection(sectionNode) ? 0 : 15;
    }

    protected override void OnNodeAdded(long sectionNode)
    {
        int y = SectionPos.GetY(sectionNode);
        if (UpdatingSectionData.CurrentLowestY > y)
            UpdatingSectionData.CurrentLowestY = y;

        long zeroNode = SectionPos.GetZeroNode(sectionNode);
        int oldTop = UpdatingSectionData.GetTopSectionY(zeroNode);
        if (oldTop < y + 1)
            UpdatingSectionData.SetTopSectionY(zeroNode, y + 1);
    }

    protected override void OnNodeRemoved(long sectionNode)
    {
        long zeroNode = SectionPos.GetZeroNode(sectionNode);
        int y = SectionPos.GetY(sectionNode);
        if (UpdatingSectionData.GetTopSectionY(zeroNode) == y + 1)
        {
            long newTopSection = sectionNode;
            while (!StoringLightForSection(newTopSection) && HasLightDataAtOrBelow(y))
            {
                newTopSection = SectionPos.Offset(newTopSection, Direction.DOWN);
                y--;
            }

            if (StoringLightForSection(newTopSection))
                UpdatingSectionData.SetTopSectionY(zeroNode, y + 1);
            else
                UpdatingSectionData.RemoveTopSection(zeroNode);
        }
    }

    protected override DataLayer CreateDataLayer(long sectionNode)
    {
        DataLayer? queuedLayer = GetQueuedSectionData(sectionNode);
        if (queuedLayer is not null)
            return queuedLayer;

        int topSection = UpdatingSectionData.GetTopSectionY(SectionPos.GetZeroNode(sectionNode));
        if (topSection != UpdatingSectionData.CurrentLowestY &&
            SectionPos.GetY(sectionNode) < topSection)
        {
            long aboveSection = SectionPos.Offset(sectionNode, Direction.UP);
            DataLayer? aboveData;
            while ((aboveData = GetDataLayer(aboveSection, updating: true)) is null)
                aboveSection = SectionPos.Offset(aboveSection, Direction.UP);

            return RepeatFirstLayer(aboveData);
        }

        return LightOnInSection(sectionNode)
            ? new DataLayer(15)
            : new DataLayer();
    }

    private static DataLayer RepeatFirstLayer(DataLayer data)
    {
        if (data.IsDefinitelyHomogenous())
            return data.Copy();

        byte[] input = data.GetData();
        byte[] output = new byte[DataLayer.Size];
        for (int layerIndex = 0; layerIndex < DataLayer.LayerCount; layerIndex++)
            Array.Copy(input, 0, output, layerIndex * DataLayer.LayerSize, DataLayer.LayerSize);

        return new DataLayer(output);
    }

    protected bool HasLightDataAtOrBelow(int sectionY) =>
        sectionY >= UpdatingSectionData.CurrentLowestY;

    protected bool IsAboveData(long sectionNode)
    {
        long zeroNode = SectionPos.GetZeroNode(sectionNode);
        int topSection = UpdatingSectionData.GetTopSectionY(zeroNode);
        return topSection == UpdatingSectionData.CurrentLowestY ||
            SectionPos.GetY(sectionNode) >= topSection;
    }

    protected int GetTopSectionY(long zeroNode) =>
        UpdatingSectionData.GetTopSectionY(zeroNode);

    protected int GetBottomSectionY() =>
        UpdatingSectionData.CurrentLowestY;

    public sealed class SkyDataLayerStorageMap
        : DataLayerStorageMap<SkyDataLayerStorageMap>
    {
        private readonly Dictionary<long, int> _topSections;

        public int CurrentLowestY { get; set; }

        public SkyDataLayerStorageMap(
            Dictionary<long, DataLayer> map,
            Dictionary<long, int> topSections,
            int currentLowestY)
            : base(map)
        {
            ArgumentNullException.ThrowIfNull(topSections);
            _topSections = topSections;
            CurrentLowestY = currentLowestY;
        }

        public int GetTopSectionY(long zeroNode) =>
            _topSections.TryGetValue(zeroNode, out int topSection)
                ? topSection
                : CurrentLowestY;

        public void SetTopSectionY(long zeroNode, int topSection) =>
            _topSections[zeroNode] = topSection;

        public void RemoveTopSection(long zeroNode) =>
            _topSections.Remove(zeroNode);

        public override SkyDataLayerStorageMap Copy() =>
            new(
                new Dictionary<long, DataLayer>(Map),
                new Dictionary<long, int>(_topSections),
                CurrentLowestY);
    }
}
