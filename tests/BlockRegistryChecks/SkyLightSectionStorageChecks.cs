using Asteria.Content.Chunks;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Lighting;

internal static class SkyLightSectionStorageChecks
{
    private sealed class TestGetter : LightChunkGetter
    {
        public LightChunk? GetChunkForLighting(int chunkX, int chunkZ) => null;

        public BlockGetter GetLevel() =>
            throw new InvalidOperationException("Skylight section storage tests do not require a loaded level.");
    }

    private sealed class TestStorage : SkyLightSectionStorage
    {
        public TestStorage()
            : base(new TestGetter())
        {
        }

        public int ReadLightValue(long blockNode, bool updating = false) =>
            updating ? GetLightValue(blockNode, true) : GetLightValue(blockNode);

        public void AddNode(long sectionNode) =>
            OnNodeAdded(sectionNode);

        public void RemoveNode(long sectionNode)
        {
            UpdatingSectionData.RemoveLayer(sectionNode);
            UpdatingSectionData.ClearCache();
            OnNodeRemoved(sectionNode);
        }

        public void SetLayer(long sectionNode, DataLayer layer)
        {
            UpdatingSectionData.SetLayer(sectionNode, layer);
            ChangedSections.Add(sectionNode);
            UpdatingSectionData.ClearCache();
        }

        public DataLayer CreateLayer(long sectionNode) =>
            CreateDataLayer(sectionNode);

        public void Queue(long sectionNode, DataLayer layer) =>
            QueueSectionData(sectionNode, layer);

        public void EnableColumn(long zeroNode, bool enabled) =>
            SetLightEnabled(zeroNode, enabled);

        public int ReadTopSectionY(long zeroNode) =>
            GetTopSectionY(zeroNode);

        public int ReadBottomSectionY() =>
            GetBottomSectionY();

        public bool ReadHasLightDataAtOrBelow(int sectionY) =>
            HasLightDataAtOrBelow(sectionY);

        public bool ReadIsAboveData(long sectionNode) =>
            IsAboveData(sectionNode);

        public void Publish() =>
            SwapSectionMap();
    }

    public static void Run()
    {
        CheckDefaultLightOutsideStoredData();
        CheckTopSectionTrackingAndProjectedLight();
        CheckFirstLayerReplication();
        CheckQueuedLayerPrecedence();
        CheckTopSectionRemoval();
    }

    private static void CheckDefaultLightOutsideStoredData()
    {
        var storage = new TestStorage();
        long blockNode = new BlockPos(3, 20, -7).AsLong();
        long zeroNode = SectionPos.GetZeroNode(SectionPos.BlockToSection(blockNode));

        Require(storage.ReadLightValue(blockNode) == 15,
            "Published sky light above the highest stored section must default to full sky light.");
        Require(storage.ReadLightValue(blockNode, updating: true) == 0,
            "Updating sky light in a column without a light source must default to zero.");

        storage.EnableColumn(zeroNode, enabled: true);
        Require(storage.ReadLightValue(blockNode, updating: true) == 15,
            "Enabling sky light in a column must make its unstored upper sections full brightness.");
    }

    private static void CheckTopSectionTrackingAndProjectedLight()
    {
        var storage = new TestStorage();
        long topSection = SectionPos.AsLong(2, 3, -1);
        long zeroNode = SectionPos.GetZeroNode(topSection);
        storage.AddNode(topSection);

        Require(storage.ReadBottomSectionY() == 3,
            "The lowest stored section Y must track the first data section.");
        Require(storage.ReadTopSectionY(zeroNode) == 4,
            "A stored section at Y must set the column's top section to Y + 1.");
        Require(storage.ReadIsAboveData(SectionPos.AsLong(2, 4, -1)),
            "A section at or above the column top must be above stored light data.");
        Require(!storage.ReadIsAboveData(SectionPos.AsLong(2, 2, -1)),
            "A section below the column top must not be classified as above data.");

        var topLayer = new DataLayer();
        topLayer.Set(6, 0, 9, 12);
        topLayer.Set(6, 5, 9, 3);
        storage.SetLayer(topSection, topLayer);
        storage.Publish();

        long blockBelow = new BlockPos(2 * 16 + 6, 1 * 16 + 7, -1 * 16 + 9).AsLong();
        Require(storage.ReadLightValue(blockBelow) == 12,
            "A section without its own layer must project the first vertical layer from the next stored section.");
    }

    private static void CheckFirstLayerReplication()
    {
        var storage = new TestStorage();
        long upperSection = SectionPos.AsLong(-2, 2, 4);
        long lowerSection = SectionPos.AsLong(-2, 0, 4);
        storage.AddNode(upperSection);

        var upperLayer = new DataLayer();
        upperLayer.Set(1, 0, 2, 7);
        upperLayer.Set(1, 1, 2, 9);
        storage.SetLayer(upperSection, upperLayer);

        storage.AddNode(lowerSection);
        DataLayer repeated = storage.CreateLayer(lowerSection);
        Require(repeated.Get(1, 0, 2) == 7 &&
                repeated.Get(1, 1, 2) == 7 &&
                repeated.Get(1, 15, 2) == 7,
            "A new section below stored data must repeat the first 128-byte layer across all 16 Y slices.");

        var homogeneousStorage = new TestStorage();
        long homogeneousUpper = SectionPos.AsLong(0, 3, 0);
        long homogeneousLower = SectionPos.AsLong(0, 1, 0);
        homogeneousStorage.AddNode(homogeneousUpper);
        homogeneousStorage.SetLayer(homogeneousUpper, new DataLayer(8));
        homogeneousStorage.AddNode(homogeneousLower);
        DataLayer homogeneousCopy = homogeneousStorage.CreateLayer(homogeneousLower);
        Require(homogeneousCopy.IsDefinitelyFilledWith(8),
            "Repeating a homogeneous source layer must preserve the lazy homogeneous representation.");
    }

    private static void CheckQueuedLayerPrecedence()
    {
        var storage = new TestStorage();
        long sectionNode = SectionPos.AsLong(1, -2, 5);
        var queued = new DataLayer(11);
        storage.Queue(sectionNode, queued);

        Require(ReferenceEquals(storage.CreateLayer(sectionNode), queued),
            "Queued section light data must take precedence over generated defaults.");
    }

    private static void CheckTopSectionRemoval()
    {
        var storage = new TestStorage();
        long lowerSection = SectionPos.AsLong(4, 1, -3);
        long upperSection = SectionPos.AsLong(4, 3, -3);
        long zeroNode = SectionPos.GetZeroNode(upperSection);

        storage.AddNode(lowerSection);
        storage.SetLayer(lowerSection, new DataLayer(2));
        storage.AddNode(upperSection);
        storage.SetLayer(upperSection, new DataLayer(6));
        Require(storage.ReadTopSectionY(zeroNode) == 4,
            "The highest section in a column must be tracked independently of the global minimum.");

        storage.RemoveNode(upperSection);
        Require(storage.ReadTopSectionY(zeroNode) == 2,
            "Removing the top section must scan down to the next section with stored light data.");

        storage.RemoveNode(lowerSection);
        Require(storage.ReadTopSectionY(zeroNode) == storage.ReadBottomSectionY(),
            "Removing the final section in a column must restore the map's default top-section value.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
