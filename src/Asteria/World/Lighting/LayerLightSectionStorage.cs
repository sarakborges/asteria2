using System.Collections.Concurrent;
using Asteria.Content.Chunks;
using Asteria.Core;
using Asteria.World;

namespace Asteria.World.Lighting;

/// <summary>
/// Maintains mutable and published light data for sections, including section
/// neighborhood state and queued light data received before a section is ready.
/// Port of the Minecraft Java 26.3 layer light storage lifecycle.
/// </summary>
public abstract class LayerLightSectionStorage<TMap>
    where TMap : DataLayerStorageMap<TMap>
{
    private readonly LightLayer _layer;
    private readonly HashSet<long> _columnsWithSources = new();
    private readonly HashSet<long> _columnsToRetainQueuedDataFor = new();
    private readonly HashSet<long> _toRemove = new();
    private readonly ConcurrentDictionary<long, DataLayer> _queuedSections = new();

    protected readonly LightChunkGetter ChunkSource;
    protected readonly Dictionary<long, byte> SectionStates = new();
    protected volatile TMap VisibleSectionData;
    protected readonly TMap UpdatingSectionData;
    protected readonly HashSet<long> ChangedSections = new();
    protected readonly HashSet<long> SectionsAffectedByLightUpdates = new();
    protected volatile bool HasPendingInconsistencies;

    protected LayerLightSectionStorage(
        LightLayer layer,
        LightChunkGetter chunkSource,
        TMap initialMap)
    {
        ArgumentNullException.ThrowIfNull(chunkSource);
        ArgumentNullException.ThrowIfNull(initialMap);

        _layer = layer;
        ChunkSource = chunkSource;
        UpdatingSectionData = initialMap;
        VisibleSectionData = initialMap.Copy();
        VisibleSectionData.DisableCache();
    }

    protected bool StoringLightForSection(long sectionNode) =>
        GetDataLayer(sectionNode, updating: true) is not null;

    protected DataLayer? GetDataLayer(long sectionNode, bool updating) =>
        GetDataLayer(updating ? UpdatingSectionData : VisibleSectionData, sectionNode);

    protected DataLayer? GetDataLayer(TMap sections, long sectionNode) =>
        sections.GetLayer(sectionNode);

    protected DataLayer? GetDataLayerToWrite(long sectionNode)
    {
        DataLayer? dataLayer = UpdatingSectionData.GetLayer(sectionNode);
        if (dataLayer is null)
            return null;

        if (ChangedSections.Add(sectionNode))
        {
            dataLayer = dataLayer.Copy();
            UpdatingSectionData.SetLayer(sectionNode, dataLayer);
            UpdatingSectionData.ClearCache();
        }

        return dataLayer;
    }

    public DataLayer? GetDataLayerData(long sectionNode)
    {
        if (_queuedSections.TryGetValue(sectionNode, out DataLayer? queued))
            return queued;

        return GetDataLayer(sectionNode, updating: false);
    }

    protected abstract int GetLightValue(long blockNode);

    protected int GetStoredLevel(long blockNode)
    {
        long sectionNode = SectionPos.BlockToSection(blockNode);
        DataLayer dataLayer = GetDataLayer(sectionNode, updating: true)!;
        return dataLayer.Get(
            SectionPos.SectionRelative(BlockPos.GetX(blockNode)),
            SectionPos.SectionRelative(BlockPos.GetY(blockNode)),
            SectionPos.SectionRelative(BlockPos.GetZ(blockNode)));
    }

    protected void SetStoredLevel(long blockNode, int level)
    {
        long sectionNode = SectionPos.BlockToSection(blockNode);
        DataLayer dataLayer;
        if (ChangedSections.Add(sectionNode))
        {
            dataLayer = UpdatingSectionData.CopyDataLayer(sectionNode);
        }
        else
        {
            dataLayer = GetDataLayer(sectionNode, updating: true)!;
        }

        dataLayer.Set(
            SectionPos.SectionRelative(BlockPos.GetX(blockNode)),
            SectionPos.SectionRelative(BlockPos.GetY(blockNode)),
            SectionPos.SectionRelative(BlockPos.GetZ(blockNode)),
            level);
        SectionPos.AroundAndAtBlockPos(
            blockNode,
            sectionNode => { SectionsAffectedByLightUpdates.Add(sectionNode); });
    }

    protected void MarkSectionAndNeighborsAsAffected(long sectionNode)
    {
        int x = SectionPos.GetX(sectionNode);
        int y = SectionPos.GetY(sectionNode);
        int z = SectionPos.GetZ(sectionNode);

        for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
        {
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                    SectionsAffectedByLightUpdates.Add(
                        SectionPos.AsLong(x + offsetX, y + offsetY, z + offsetZ));
            }
        }
    }

    protected DataLayer CreateDataLayer(long sectionNode) =>
        _queuedSections.TryGetValue(sectionNode, out DataLayer? queued)
            ? queued
            : new DataLayer();

    protected bool HasInconsistencies() =>
        HasPendingInconsistencies;

    protected void MarkNewInconsistencies()
    {
        if (!HasPendingInconsistencies)
            return;

        HasPendingInconsistencies = false;

        foreach (long node in _toRemove)
        {
            _queuedSections.TryRemove(node, out DataLayer? queued);
            DataLayer? stored = UpdatingSectionData.RemoveLayer(node);
            if (_columnsToRetainQueuedDataFor.Contains(SectionPos.GetZeroNode(node)))
            {
                if (queued is not null)
                    _queuedSections[node] = queued;
                else if (stored is not null)
                    _queuedSections[node] = stored;
            }
        }

        UpdatingSectionData.ClearCache();

        foreach (long node in _toRemove)
        {
            OnNodeRemoved(node);
            ChangedSections.Add(node);
        }

        _toRemove.Clear();

        foreach (KeyValuePair<long, DataLayer> entry in _queuedSections.ToArray())
        {
            long sectionNode = entry.Key;
            if (!StoringLightForSection(sectionNode))
                continue;

            DataLayer data = entry.Value;
            if (!ReferenceEquals(UpdatingSectionData.GetLayer(sectionNode), data))
            {
                UpdatingSectionData.SetLayer(sectionNode, data);
                ChangedSections.Add(sectionNode);
            }

            if (_queuedSections.TryGetValue(sectionNode, out DataLayer? current) &&
                ReferenceEquals(current, data))
            {
                _queuedSections.TryRemove(sectionNode, out _);
            }
        }

        UpdatingSectionData.ClearCache();
    }

    protected virtual void OnNodeAdded(long sectionNode)
    {
    }

    protected virtual void OnNodeRemoved(long sectionNode)
    {
    }

    protected void SetLightEnabled(long zeroNode, bool enable)
    {
        if (enable)
            _columnsWithSources.Add(zeroNode);
        else
            _columnsWithSources.Remove(zeroNode);
    }

    protected bool LightOnInSection(long sectionNode) =>
        _columnsWithSources.Contains(SectionPos.GetZeroNode(sectionNode));

    protected bool LightOnInColumn(long sectionZeroNode) =>
        _columnsWithSources.Contains(sectionZeroNode);

    public void RetainData(long zeroNode, bool retain)
    {
        if (retain)
            _columnsToRetainQueuedDataFor.Add(zeroNode);
        else
            _columnsToRetainQueuedDataFor.Remove(zeroNode);
    }

    protected void QueueSectionData(long sectionNode, DataLayer? data)
    {
        if (data is not null)
        {
            _queuedSections[sectionNode] = data;
            HasPendingInconsistencies = true;
        }
        else
        {
            _queuedSections.TryRemove(sectionNode, out _);
        }
    }

    protected void UpdateSectionStatus(long sectionNode, bool sectionEmpty)
    {
        byte state = SectionStates.GetValueOrDefault(sectionNode);
        byte newState = SectionState.HasData(state, !sectionEmpty);
        if (state == newState)
            return;

        PutSectionState(sectionNode, newState);
        int neighborIncrement = sectionEmpty ? -1 : 1;

        for (int offsetX = -1; offsetX <= 1; offsetX++)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
                {
                    if (offsetX == 0 && offsetY == 0 && offsetZ == 0)
                        continue;

                    long neighborNode = SectionPos.Offset(sectionNode, offsetX, offsetY, offsetZ);
                    byte neighborState = SectionStates.GetValueOrDefault(neighborNode);
                    PutSectionState(
                        neighborNode,
                        SectionState.NeighborCount(
                            neighborState,
                            SectionState.GetNeighborCount(neighborState) + neighborIncrement));
                }
            }
        }
    }

    protected void PutSectionState(long sectionNode, byte state)
    {
        if (state != SectionState.Empty)
        {
            bool isNew = !SectionStates.ContainsKey(sectionNode);
            SectionStates[sectionNode] = state;
            if (isNew)
                InitializeSection(sectionNode);
        }
        else if (SectionStates.Remove(sectionNode))
        {
            RemoveSection(sectionNode);
        }
    }

    private void InitializeSection(long sectionNode)
    {
        if (_toRemove.Remove(sectionNode))
            return;

        UpdatingSectionData.SetLayer(sectionNode, CreateDataLayer(sectionNode));
        ChangedSections.Add(sectionNode);
        OnNodeAdded(sectionNode);
        MarkSectionAndNeighborsAsAffected(sectionNode);
        HasPendingInconsistencies = true;
    }

    private void RemoveSection(long sectionNode)
    {
        _toRemove.Add(sectionNode);
        HasPendingInconsistencies = true;
    }

    protected void SwapSectionMap()
    {
        if (ChangedSections.Count > 0)
        {
            TMap copy = UpdatingSectionData.Copy();
            copy.DisableCache();
            VisibleSectionData = copy;
            ChangedSections.Clear();
        }

        if (SectionsAffectedByLightUpdates.Count == 0)
            return;

        foreach (long sectionNode in SectionsAffectedByLightUpdates)
            ChunkSource.OnLightUpdate(_layer, SectionPos.Of(sectionNode));

        SectionsAffectedByLightUpdates.Clear();
    }

    public SectionType GetDebugSectionType(long sectionNode) =>
        SectionState.GetType(SectionStates.GetValueOrDefault(sectionNode));

    protected byte GetSectionState(long sectionNode) =>
        SectionStates.GetValueOrDefault(sectionNode);


    protected int GetQueuedSectionCount() =>
        _queuedSections.Count;

    protected static class SectionState
    {
        public const byte Empty = 0;
        private const byte HasDataBit = 32;
        private const byte NeighborCountBits = 31;

        public static byte HasData(byte state, bool hasData) =>
            unchecked((byte)(hasData ? state | HasDataBit : state & ~HasDataBit));

        public static byte NeighborCount(byte state, int neighborCount)
        {
            if (neighborCount is < 0 or > 26)
                throw new ArgumentException("Neighbor count was not within range [0; 26]", nameof(neighborCount));

            return unchecked((byte)(state & ~NeighborCountBits | neighborCount & NeighborCountBits));
        }

        public static bool ContainsData(byte state) =>
            (state & HasDataBit) != 0;

        public static int GetNeighborCount(byte state) =>
            state & NeighborCountBits;

        public static SectionType GetType(byte state)
        {
            if (state == Empty)
                return SectionType.Empty;

            return ContainsData(state)
                ? SectionType.LightAndData
                : SectionType.LightOnly;
        }
    }

    public enum SectionType
    {
        Empty,
        LightOnly,
        LightAndData
    }
}
