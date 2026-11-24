using Asteria.Content.Chunks;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Level;
using Asteria.World.Lighting;

internal static class LayerLightSectionStorageChecks
{
    private sealed class TestMap : DataLayerStorageMap<TestMap>
    {
        public TestMap()
            : this(new Dictionary<long, DataLayer>())
        {
        }

        private TestMap(Dictionary<long, DataLayer> map)
            : base(map)
        {
        }

        public int Count => Map.Count;

        public void Add(long key, DataLayer layer) =>
            SetLayer(key, layer);

        public override TestMap Copy() =>
            new(new Dictionary<long, DataLayer>(Map));
    }

    private sealed class TestGetter : LightChunkGetter
    {
        public List<(LightLayer Layer, long Section)> Updates { get; } = new();

        public LightChunk? GetChunkForLighting(int chunkX, int chunkZ) => null;

        public BlockGetter GetLevel() =>
            throw new InvalidOperationException("The storage tests do not require a loaded level.");

        public void OnLightUpdate(LightLayer layer, SectionPos position) =>
            Updates.Add((layer, position.AsLong()));
    }

    private sealed class TestStorage : LayerLightSectionStorage<TestMap>
    {
        public List<long> AddedNodes { get; } = new();
        public List<long> RemovedNodes { get; } = new();

        public TestStorage(TestGetter getter, TestMap? initialMap = null)
            : base(LightLayer.BLOCK, getter, initialMap ?? new TestMap())
        {
        }

        protected override int GetLightValue(long blockNode) => 0;

        public DataLayer? ReadLayer(long sectionNode, bool updating) =>
            GetDataLayer(sectionNode, updating);

        public DataLayer? ReadLayerToWrite(long sectionNode) =>
            GetDataLayerToWrite(sectionNode);

        public int ReadStoredLevel(long blockNode) =>
            GetStoredLevel(blockNode);

        public void WriteStoredLevel(long blockNode, int level) =>
            SetStoredLevel(blockNode, level);

        public void MarkAffected(long sectionNode) =>
            MarkSectionAndNeighborsAsAffected(sectionNode);

        public void SetState(long sectionNode, byte state) =>
            PutSectionState(sectionNode, state);

        public byte ReadState(long sectionNode) =>
            GetSectionState(sectionNode);

        public void UpdateStatus(long sectionNode, bool empty) =>
            UpdateSectionStatus(sectionNode, empty);

        public void ProcessInconsistencies() =>
            MarkNewInconsistencies();

        public void Swap() =>
            SwapSectionMap();

        public void EnableColumn(long zeroNode, bool enabled) =>
            SetLightEnabled(zeroNode, enabled);

        public bool IsLightOnInSection(long sectionNode) =>
            LightOnInSection(sectionNode);

        public bool IsLightOnInColumn(long zeroNode) =>
            LightOnInColumn(zeroNode);

        public void Queue(long sectionNode, DataLayer? layer) =>
            QueueSectionData(sectionNode, layer);

        public int StoredCount => UpdatingSectionData.Count;

        public int QueuedCount => GetQueuedSectionCount();

        public bool PendingInconsistencies => HasInconsistencies();

        public int ChangedCount => ChangedSections.Count;

        public int AffectedCount => SectionsAffectedByLightUpdates.Count;

        protected override void OnNodeAdded(long sectionNode) =>
            AddedNodes.Add(sectionNode);

        protected override void OnNodeRemoved(long sectionNode) =>
            RemovedNodes.Add(sectionNode);

        public static byte StateHasData(byte state, bool hasData) =>
            SectionState.HasData(state, hasData);

        public static byte StateNeighborCount(byte state, int count) =>
            SectionState.NeighborCount(state, count);

        public static bool StateContainsData(byte state) =>
            SectionState.ContainsData(state);

        public static int StateGetNeighborCount(byte state) =>
            SectionState.GetNeighborCount(state);

        public static LightSectionType StateType(byte state) =>
            SectionState.GetType(state);
    }

    public static void Run()
    {
        CheckSectionStateEncoding();
        CheckCopyOnWriteAndVisibleSnapshot();
        CheckStoredLevelsAndAffectedSections();
        CheckSectionNeighborhoodLifecycle();
        CheckQueuedDataAndRetention();
        CheckLightColumnFlagsAndSwapNotifications();
    }

    private static void CheckSectionStateEncoding()
    {
        byte state = TestStorage.StateHasData(0, true);
        Require(TestStorage.StateContainsData(state), "The section data bit must be independently set.");
        Require(TestStorage.StateGetNeighborCount(state) == 0, "The initial neighbor count must be zero.");

        state = TestStorage.StateNeighborCount(state, 26);
        Require(TestStorage.StateContainsData(state), "Neighbor updates must preserve the data bit.");
        Require(TestStorage.StateGetNeighborCount(state) == 26, "The neighbor counter must support all 26 adjacent sections.");
        Require(TestStorage.StateType(state) == LightSectionType.LightAndData,
            "A nonempty section with light storage must report LIGHT_AND_DATA.");

        byte neighborsOnly = TestStorage.StateNeighborCount(0, 1);
        Require(TestStorage.StateType(neighborsOnly) == LightSectionType.LightOnly,
            "A section with neighbors but no blocks must report LIGHT_ONLY.");
        Require(TestStorage.StateHasData(neighborsOnly, false) == neighborsOnly,
            "Clearing the data bit must preserve the neighbor count.");
        Require(TestStorage.StateType(0) == LightSectionType.Empty,
            "A zero state must report EMPTY.");

        foreach (int invalid in new[] { -1, 27 })
        {
            try
            {
                _ = TestStorage.StateNeighborCount(0, invalid);
                throw new InvalidOperationException("Neighbor counts outside [0, 26] must fail.");
            }
            catch (ArgumentException exception) when (exception.ParamName == "neighborCount")
            {
            }
        }
    }

    private static void CheckCopyOnWriteAndVisibleSnapshot()
    {
        long sectionNode = SectionPos.AsLong(0, 0, 0);
        var originalLayer = new DataLayer(4);
        var initialMap = new TestMap();
        initialMap.Add(sectionNode, originalLayer);
        var storage = new TestStorage(new TestGetter(), initialMap);

        Require(ReferenceEquals(storage.ReadLayer(sectionNode, false), originalLayer),
            "The published map initially shares the immutable data-layer snapshot.");
        Require(ReferenceEquals(storage.ReadLayer(sectionNode, true), originalLayer),
            "The updating map initially retains the original data layer.");

        DataLayer writable = storage.ReadLayerToWrite(sectionNode)!;
        Require(!ReferenceEquals(writable, originalLayer),
            "The first write to a changed section must use a copied data layer.");
        Require(ReferenceEquals(storage.ReadLayerToWrite(sectionNode), writable),
            "Repeated writes within one section update must reuse the same copied layer.");

        writable.Set(2, 3, 4, 9);
        Require(storage.ReadStoredLevel(new BlockPos(2, 3, 4).AsLong()) == 9,
            "Stored light reads must use the updating data.");
        Require(originalLayer.Get(2, 3, 4) == 4,
            "Copy-on-write must preserve the published layer.");
        Require(storage.ReadLayer(sectionNode, false)!.Get(2, 3, 4) == 4,
            "Readers of the published map must not observe in-progress writes.");

        storage.Swap();
        Require(storage.ReadLayer(sectionNode, false)!.Get(2, 3, 4) == 9,
            "Swapping section maps must publish the newly written level.");
        Require(storage.ChangedCount == 0,
            "Publishing a new section map must clear the changed-section set.");
    }

    private static void CheckStoredLevelsAndAffectedSections()
    {
        long sectionNode = SectionPos.AsLong(0, 0, 0);
        var initialMap = new TestMap();
        initialMap.Add(sectionNode, new DataLayer());
        var storage = new TestStorage(new TestGetter(), initialMap);

        long blockNode = new BlockPos(15, 15, 15).AsLong();
        storage.WriteStoredLevel(blockNode, 13);
        Require(storage.ReadStoredLevel(blockNode) == 13,
            "Packed block coordinates must resolve the matching DataLayer cell.");
        Require(storage.AffectedCount == 8,
            "Updating a block at a section corner must affect each section touched by its one-block neighborhood.");

        storage.MarkAffected(sectionNode);
        Require(storage.AffectedCount == 27,
            "Marking a section and its neighbors must add the complete 3x3x3 section cube.");
    }

    private static void CheckSectionNeighborhoodLifecycle()
    {
        var getter = new TestGetter();
        var storage = new TestStorage(getter);
        long sectionNode = SectionPos.AsLong(-2, 4, 7);

        storage.UpdateStatus(sectionNode, empty: false);
        Require(storage.ReadState(sectionNode) == 32,
            "A nonempty section must set the has-data bit.");
        Require(storage.StoredCount == 27 && storage.AddedNodes.Count == 27,
            "The first nonempty section must initialize storage for itself and its 26 neighbors.");
        Require(storage.PendingInconsistencies,
            "Initializing light sections must schedule inconsistency processing.");

        storage.ProcessInconsistencies();
        Require(!storage.PendingInconsistencies,
            "Processing pending section changes must clear the inconsistency flag.");

        int expectedNotifications = storage.AffectedCount;
        storage.Swap();
        Require(expectedNotifications > 0 && getter.Updates.Count == expectedNotifications,
            "Publishing section state must notify the chunk source about every affected section.");

        storage.UpdateStatus(sectionNode, empty: true);
        Require(storage.StoredCount == 27,
            "Section removal is deferred until inconsistency processing.");
        storage.ProcessInconsistencies();
        Require(storage.StoredCount == 0 && storage.RemovedNodes.Count == 27,
            "Making the center section empty must remove storage after its 26 neighbors lose their final reference.");
        Require(storage.GetDebugSectionType(sectionNode) == LightSectionType.Empty,
            "A fully removed section must report EMPTY.");
    }

    private static void CheckQueuedDataAndRetention()
    {
        long sectionNode = SectionPos.AsLong(2, 3, -4);
        long zeroNode = SectionPos.GetZeroNode(sectionNode);
        var getter = new TestGetter();
        var storage = new TestStorage(getter);
        var queued = new DataLayer(12);

        storage.Queue(sectionNode, queued);
        Require(ReferenceEquals(storage.GetDataLayerData(sectionNode), queued),
            "Queued data must be visible before section initialization.");

        storage.SetState(sectionNode, TestStorage.StateHasData(0, true));
        storage.ProcessInconsistencies();
        Require(ReferenceEquals(storage.ReadLayer(sectionNode, true), queued),
            "Queued data must be installed into the updating map when storage is initialized.");
        Require(storage.QueuedCount == 0,
            "Installed queued data must be removed from the queue.");

        storage.RetainData(zeroNode, retain: true);
        storage.SetState(sectionNode, 0);
        storage.ProcessInconsistencies();
        Require(storage.ReadLayer(sectionNode, true) is null,
            "Removing section state must discard its active data layer.");
        Require(ReferenceEquals(storage.GetDataLayerData(sectionNode), queued),
            "Retaining a chunk column must preserve section light data for a later reload.");
        Require(storage.QueuedCount == 1,
            "Retained light data must be queued after section removal.");

        storage.RetainData(zeroNode, retain: false);
        storage.Queue(sectionNode, null);
        Require(storage.GetDataLayerData(sectionNode) is null,
            "Removing queued section data must reveal the absent published layer.");
    }

    private static void CheckLightColumnFlagsAndSwapNotifications()
    {
        long sectionNode = SectionPos.AsLong(-1, 2, 6);
        long zeroNode = SectionPos.GetZeroNode(sectionNode);
        var getter = new TestGetter();
        var storage = new TestStorage(getter);

        storage.EnableColumn(zeroNode, true);
        Require(storage.IsLightOnInColumn(zeroNode) && storage.IsLightOnInSection(sectionNode),
            "Enabling a light column must apply to every vertical section in that column.");
        storage.EnableColumn(zeroNode, false);
        Require(!storage.IsLightOnInColumn(zeroNode) && !storage.IsLightOnInSection(sectionNode),
            "Disabling a light column must clear the shared column source flag.");

        storage.MarkAffected(sectionNode);
        storage.Swap();
        Require(getter.Updates.Count == 27,
            "Section-map publication must notify all 27 affected neighboring sections.");
        Require(getter.Updates.All(update => update.Layer == LightLayer.BLOCK),
            "Notifications must carry the storage's selected light layer.");
        Require(storage.AffectedCount == 0,
            "Published light-update notifications must be cleared after dispatch.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
