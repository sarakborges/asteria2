using Asteria.Content.Chunks;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Lighting;

internal static class BlockLightSectionStorageChecks
{
    private sealed class TestGetter : LightChunkGetter
    {
        public LightChunk? GetChunkForLighting(int chunkX, int chunkZ) => null;

        public BlockGetter GetLevel() =>
            throw new InvalidOperationException("Block light storage reads do not require a loaded level.");
    }

    private sealed class TestStorage : BlockLightSectionStorage
    {
        public TestStorage()
            : base(new TestGetter())
        {
        }

        public int ReadLightValue(long blockNode) =>
            GetLightValue(blockNode);

        public void SetUpdatingLayer(long sectionNode, DataLayer layer)
        {
            UpdatingSectionData.SetLayer(sectionNode, layer);
            ChangedSections.Add(sectionNode);
            UpdatingSectionData.ClearCache();
        }

        public void Publish() => SwapSectionMap();
    }

    public static void Run()
    {
        CheckMissingLayerReturnsZero();
        CheckReadsPublishedDataOnly();
        CheckPackedBlockCoordinates();
    }

    private static void CheckMissingLayerReturnsZero()
    {
        var storage = new TestStorage();
        long blockNode = new BlockPos(4, 5, 6).AsLong();
        Require(storage.ReadLightValue(blockNode) == 0,
            "Block light must be zero when the visible section has no data layer.");
    }

    private static void CheckReadsPublishedDataOnly()
    {
        var storage = new TestStorage();
        long sectionNode = SectionPos.AsLong(2, -1, 3);
        var layer = new DataLayer();
        layer.Set(7, 9, 11, 13);

        storage.SetUpdatingLayer(sectionNode, layer);
        long blockNode = new BlockPos(2 * 16 + 7, -16 + 9, 3 * 16 + 11).AsLong();

        Require(storage.ReadLightValue(blockNode) == 0,
            "Block light reads must use the visible map, not in-progress section data.");

        storage.Publish();
        Require(storage.ReadLightValue(blockNode) == 13,
            "Publishing the map must expose the section's packed light nibble.");
    }

    private static void CheckPackedBlockCoordinates()
    {
        var storage = new TestStorage();
        long sectionNode = SectionPos.AsLong(-3, 4, -2);
        var layer = new DataLayer(5);
        layer.Set(0, 0, 0, 1);
        layer.Set(15, 15, 15, 14);
        storage.SetUpdatingLayer(sectionNode, layer);
        storage.Publish();

        long minimum = new BlockPos(-48, 64, -32).AsLong();
        long maximum = new BlockPos(-33, 79, -17).AsLong();
        Require(storage.ReadLightValue(minimum) == 1,
            "Negative section origins must use section-relative block coordinates.");
        Require(storage.ReadLightValue(maximum) == 14,
            "The maximum local block coordinates must address cell 15,15,15.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
