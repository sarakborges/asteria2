using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Core;
using Asteria.World;

namespace Asteria.World.Lighting;

using SkyQueueEntry = LightEngine<SkyLightSectionStorage.SkyDataLayerStorageMap, SkyLightSectionStorage>.QueueEntry;

/// <summary>
/// Propagates full-sky sources and their attenuated light through chunk sections.
/// Port of Minecraft Java 26.3 SkyLightEngine.
/// </summary>
public sealed class SkyLightEngine
    : LightEngine<SkyLightSectionStorage.SkyDataLayerStorageMap, SkyLightSectionStorage>
{
    private static readonly long REMOVE_TOP_SKY_SOURCE_ENTRY = SkyQueueEntry.DecreaseAllDirections(15);
    private static readonly long REMOVE_SKY_SOURCE_ENTRY =
        SkyQueueEntry.DecreaseSkipOneDirection(15, Direction.UP);
    private static readonly long ADD_SKY_SOURCE_ENTRY =
        SkyQueueEntry.IncreaseSkipOneDirection(15, fromEmptyShape: false, Direction.UP);

    private readonly BlockPos.MutableBlockPos _mutablePosition = new();
    private readonly ChunkSkyLightSources _emptyChunkSources;

    public SkyLightEngine(LightChunkGetter chunkSource)
        : this(chunkSource, new SkyLightSectionStorage(chunkSource))
    {
    }

    public SkyLightEngine(
        LightChunkGetter chunkSource,
        SkyLightSectionStorage storage)
        : base(chunkSource, storage)
    {
        _emptyChunkSources = new ChunkSkyLightSources(chunkSource.GetLevel());
    }

    private static bool IsSourceLevel(int value) =>
        value == MAX_LEVEL;

    private int GetLowestSourceY(int x, int z, int defaultValue)
    {
        ChunkSkyLightSources? sources = GetChunkSources(
            SectionPos.BlockToSectionCoord(x),
            SectionPos.BlockToSectionCoord(z));
        return sources is null
            ? defaultValue
            : sources.GetLowestSourceY(
                SectionPos.SectionRelative(x),
                SectionPos.SectionRelative(z));
    }

    private ChunkSkyLightSources? GetChunkSources(int chunkX, int chunkZ) =>
        ChunkSource.GetChunkForLighting(chunkX, chunkZ)?.GetSkyLightSources();

    protected override void CheckNode(long blockNode)
    {
        int x = BlockPos.GetX(blockNode);
        int y = BlockPos.GetY(blockNode);
        int z = BlockPos.GetZ(blockNode);
        long sectionNode = SectionPos.BlockToSection(blockNode);

        int lowestSourceY = Storage.LightOnInSection(sectionNode)
            ? GetLowestSourceY(x, z, int.MaxValue)
            : int.MaxValue;

        if (lowestSourceY != int.MaxValue)
            UpdateSourcesInColumn(x, z, lowestSourceY);

        if (!Storage.StoringLightForSection(sectionNode))
            return;

        if (y >= lowestSourceY)
        {
            EnqueueDecrease(blockNode, REMOVE_SKY_SOURCE_ENTRY);
            EnqueueIncrease(blockNode, ADD_SKY_SOURCE_ENTRY);
        }
        else
        {
            int oldLevel = Storage.GetStoredLevel(blockNode);
            if (oldLevel > 0)
            {
                Storage.SetStoredLevel(blockNode, 0);
                EnqueueDecrease(blockNode, SkyQueueEntry.DecreaseAllDirections(oldLevel));
            }
            else
            {
                EnqueueDecrease(blockNode, PULL_LIGHT_IN_ENTRY);
            }
        }
    }

    private void UpdateSourcesInColumn(int x, int z, int lowestSourceY)
    {
        int worldBottomY = SectionPos.SectionToBlockCoord(Storage.GetBottomSectionY());
        RemoveSourcesBelow(x, z, lowestSourceY, worldBottomY);
        AddSourcesAbove(x, z, lowestSourceY, worldBottomY);
    }

    private void RemoveSourcesBelow(int x, int z, int lowestSourceY, int worldBottomY)
    {
        if (lowestSourceY <= worldBottomY)
            return;

        int sectionX = SectionPos.BlockToSectionCoord(x);
        int sectionZ = SectionPos.BlockToSectionCoord(z);
        int startY = lowestSourceY - 1;
        int sectionY = SectionPos.BlockToSectionCoord(startY);

        while (Storage.HasLightDataAtOrBelow(sectionY))
        {
            if (Storage.StoringLightForSection(SectionPos.AsLong(sectionX, sectionY, sectionZ)))
            {
                int sectionBottomY = SectionPos.SectionToBlockCoord(sectionY);
                int sectionTopY = sectionBottomY + 15;

                for (int y = Math.Min(sectionTopY, startY); y >= sectionBottomY; y--)
                {
                    long blockNode = BlockPos.AsLong(x, y, z);
                    if (!IsSourceLevel(Storage.GetStoredLevel(blockNode)))
                        return;

                    Storage.SetStoredLevel(blockNode, 0);
                    EnqueueDecrease(
                        blockNode,
                        y == lowestSourceY - 1
                            ? REMOVE_TOP_SKY_SOURCE_ENTRY
                            : REMOVE_SKY_SOURCE_ENTRY);
                }
            }

            sectionY--;
        }
    }

    private void AddSourcesAbove(int x, int z, int lowestSourceY, int worldBottomY)
    {
        int sectionX = SectionPos.BlockToSectionCoord(x);
        int sectionZ = SectionPos.BlockToSectionCoord(z);
        int neighborLowestSourceY = Math.Max(
            Math.Max(
                GetLowestSourceY(x - 1, z, int.MinValue),
                GetLowestSourceY(x + 1, z, int.MinValue)),
            Math.Max(
                GetLowestSourceY(x, z - 1, int.MinValue),
                GetLowestSourceY(x, z + 1, int.MinValue)));

        int startY = Math.Max(lowestSourceY, worldBottomY);
        long sectionNode = SectionPos.AsLong(
            sectionX,
            SectionPos.BlockToSectionCoord(startY),
            sectionZ);

        while (!Storage.IsAboveData(sectionNode))
        {
            if (Storage.StoringLightForSection(sectionNode))
            {
                int sectionBottomY = SectionPos.SectionToBlockCoord(SectionPos.GetY(sectionNode));
                int sectionTopY = sectionBottomY + 15;

                for (int y = Math.Max(sectionBottomY, startY); y <= sectionTopY; y++)
                {
                    long blockNode = BlockPos.AsLong(x, y, z);
                    if (IsSourceLevel(Storage.GetStoredLevel(blockNode)))
                        return;

                    Storage.SetStoredLevel(blockNode, MAX_LEVEL);
                    if (y >= neighborLowestSourceY && y != lowestSourceY)
                        continue;

                    EnqueueIncrease(
                        blockNode,
                        SkyQueueEntry.IncreaseSkySourceInDirections(
                            down: y == lowestSourceY,
                            north: y < GetLowestSourceY(x, z - 1, int.MinValue),
                            south: y < GetLowestSourceY(x, z + 1, int.MinValue),
                            west: y < GetLowestSourceY(x - 1, z, int.MinValue),
                            east: y < GetLowestSourceY(x + 1, z, int.MinValue)));
                }
            }

            sectionNode = SectionPos.Offset(sectionNode, Direction.UP);
        }
    }

    protected override void PropagateIncrease(long fromNode, long increaseData, int fromLevel)
    {
        BlockState? fromState = null;
        int emptySectionsBelow = CountEmptySectionsBelowIfAtBorder(fromNode);

        foreach (Direction direction in PROPAGATION_DIRECTIONS)
        {
            if (!SkyQueueEntry.ShouldPropagateInDirection(increaseData, direction))
                continue;

            long toNode = BlockPos.Offset(
                fromNode,
                direction.GetStepX(),
                direction.GetStepY(),
                direction.GetStepZ());
            long toSectionNode = SectionPos.BlockToSection(toNode);
            if (!Storage.StoringLightForSection(toSectionNode))
                continue;

            int maximumPossibleLevel = fromLevel - 1;
            int toLevel = Storage.GetStoredLevel(toNode);
            if (maximumPossibleLevel <= toLevel)
                continue;

            BlockState toState = GetState(_mutablePosition.Set(toNode));
            int newToLevel = fromLevel - GetOpacity(toState);
            if (newToLevel <= toLevel)
                continue;

            if (fromState is null)
            {
                fromState = SkyQueueEntry.IsFromEmptyShape(increaseData)
                    ? Blocks.AIR.DefaultState
                    : GetState(_mutablePosition.Set(fromNode));
            }

            if (ShapeOccludes(fromState, toState, direction))
                continue;

            Storage.SetStoredLevel(toNode, newToLevel);
            if (newToLevel > 1)
            {
                EnqueueIncrease(
                    toNode,
                    SkyQueueEntry.IncreaseSkipOneDirection(
                        newToLevel,
                        IsEmptyShape(toState),
                        direction.GetOpposite()));
            }

            PropagateFromEmptySections(
                toNode,
                direction,
                newToLevel,
                increase: true,
                emptySectionsBelow);
        }
    }

    protected override void PropagateDecrease(long fromNode, long decreaseData)
    {
        int emptySectionsBelow = CountEmptySectionsBelowIfAtBorder(fromNode);
        int oldFromLevel = SkyQueueEntry.GetFromLevel(decreaseData);

        foreach (Direction direction in PROPAGATION_DIRECTIONS)
        {
            if (!SkyQueueEntry.ShouldPropagateInDirection(decreaseData, direction))
                continue;

            long toNode = BlockPos.Offset(
                fromNode,
                direction.GetStepX(),
                direction.GetStepY(),
                direction.GetStepZ());
            long toSectionNode = SectionPos.BlockToSection(toNode);
            if (!Storage.StoringLightForSection(toSectionNode))
                continue;

            int toLevel = Storage.GetStoredLevel(toNode);
            if (toLevel == 0)
                continue;

            if (toLevel <= oldFromLevel - 1)
            {
                Storage.SetStoredLevel(toNode, 0);
                EnqueueDecrease(
                    toNode,
                    SkyQueueEntry.DecreaseSkipOneDirection(
                        toLevel,
                        direction.GetOpposite()));
                PropagateFromEmptySections(
                    toNode,
                    direction,
                    toLevel,
                    increase: false,
                    emptySectionsBelow);
            }
            else
            {
                EnqueueIncrease(
                    toNode,
                    SkyQueueEntry.IncreaseOnlyOneDirection(
                        toLevel,
                        fromEmptyShape: false,
                        direction.GetOpposite()));
            }
        }
    }

    private int CountEmptySectionsBelowIfAtBorder(long blockNode)
    {
        int y = BlockPos.GetY(blockNode);
        if (SectionPos.SectionRelative(y) != 0)
            return 0;

        int x = BlockPos.GetX(blockNode);
        int z = BlockPos.GetZ(blockNode);
        int localX = SectionPos.SectionRelative(x);
        int localZ = SectionPos.SectionRelative(z);
        if (localX != 0 && localX != 15 && localZ != 0 && localZ != 15)
            return 0;

        int sectionX = SectionPos.BlockToSectionCoord(x);
        int sectionY = SectionPos.BlockToSectionCoord(y);
        int sectionZ = SectionPos.BlockToSectionCoord(z);
        int emptySectionsBelow = 0;

        while (!Storage.StoringLightForSection(
                   SectionPos.AsLong(sectionX, sectionY - emptySectionsBelow - 1, sectionZ)) &&
               Storage.HasLightDataAtOrBelow(sectionY - emptySectionsBelow - 1))
        {
            emptySectionsBelow++;
        }

        return emptySectionsBelow;
    }

    private void PropagateFromEmptySections(
        long toNode,
        Direction direction,
        int toLevel,
        bool increase,
        int emptySectionsBelow)
    {
        if (emptySectionsBelow == 0)
            return;

        int x = BlockPos.GetX(toNode);
        int z = BlockPos.GetZ(toNode);
        if (!CrossedSectionEdge(direction, SectionPos.SectionRelative(x), SectionPos.SectionRelative(z)))
            return;

        int y = BlockPos.GetY(toNode);
        int sectionX = SectionPos.BlockToSectionCoord(x);
        int sectionZ = SectionPos.BlockToSectionCoord(z);
        int sectionY = SectionPos.BlockToSectionCoord(y) - 1;
        int bottomSectionY = sectionY - emptySectionsBelow + 1;

        while (sectionY >= bottomSectionY)
        {
            if (!Storage.StoringLightForSection(SectionPos.AsLong(sectionX, sectionY, sectionZ)))
            {
                sectionY--;
                continue;
            }

            int sectionMinY = SectionPos.SectionToBlockCoord(sectionY);
            for (int localY = 15; localY >= 0; localY--)
            {
                long blockNode = BlockPos.AsLong(x, sectionMinY + localY, z);
                if (increase)
                {
                    Storage.SetStoredLevel(blockNode, toLevel);
                    if (toLevel > 1)
                    {
                        EnqueueIncrease(
                            blockNode,
                            SkyQueueEntry.IncreaseSkipOneDirection(
                                toLevel,
                                fromEmptyShape: true,
                                direction.GetOpposite()));
                    }
                }
                else
                {
                    Storage.SetStoredLevel(blockNode, 0);
                    EnqueueDecrease(
                        blockNode,
                        SkyQueueEntry.DecreaseSkipOneDirection(
                            toLevel,
                            direction.GetOpposite()));
                }
            }

            sectionY--;
        }
    }

    private static bool CrossedSectionEdge(Direction direction, int x, int z)
    {
        if (ReferenceEquals(direction, Direction.NORTH))
            return z == 15;
        if (ReferenceEquals(direction, Direction.SOUTH))
            return z == 0;
        if (ReferenceEquals(direction, Direction.WEST))
            return x == 15;
        if (ReferenceEquals(direction, Direction.EAST))
            return x == 0;
        return false;
    }

    public override void SetLightEnabled(ChunkPos position, bool enabled)
    {
        base.SetLightEnabled(position, enabled);
        if (!enabled)
            return;

        ChunkSkyLightSources sources =
            GetChunkSources(position.X, position.Z) ?? _emptyChunkSources;
        int highestNonSourceY = unchecked(sources.GetHighestLowestSourceY() - 1);
        int lowestFullySourceSectionY =
            SectionPos.BlockToSectionCoord(highestNonSourceY) + 1;
        long zeroNode = SectionPos.GetZeroNode(position.X, position.Z);
        int topSectionY = Storage.GetTopSectionY(zeroNode);
        int bottomSectionY = Math.Max(
            Storage.GetBottomSectionY(),
            lowestFullySourceSectionY);

        for (int sectionY = topSectionY - 1; sectionY >= bottomSectionY; sectionY--)
        {
            DataLayer? dataLayer = Storage.GetDataLayerToWrite(
                SectionPos.AsLong(position.X, sectionY, position.Z));
            if (dataLayer is not null && dataLayer.IsEmpty())
                dataLayer.Fill(MAX_LEVEL);
        }
    }

    public override void PropagateLightSources(ChunkPos position)
    {
        long zeroNode = SectionPos.GetZeroNode(position.X, position.Z);
        Storage.SetLightEnabled(zeroNode, enable: true);

        ChunkSkyLightSources sources =
            GetChunkSources(position.X, position.Z) ?? _emptyChunkSources;
        ChunkSkyLightSources northSources =
            GetChunkSources(position.X, position.Z - 1) ?? _emptyChunkSources;
        ChunkSkyLightSources southSources =
            GetChunkSources(position.X, position.Z + 1) ?? _emptyChunkSources;
        ChunkSkyLightSources westSources =
            GetChunkSources(position.X - 1, position.Z) ?? _emptyChunkSources;
        ChunkSkyLightSources eastSources =
            GetChunkSources(position.X + 1, position.Z) ?? _emptyChunkSources;

        int topSectionY = Storage.GetTopSectionY(zeroNode);
        int bottomSectionY = Storage.GetBottomSectionY();
        int sectionMinX = SectionPos.SectionToBlockCoord(position.X);
        int sectionMinZ = SectionPos.SectionToBlockCoord(position.Z);

        for (int sectionY = topSectionY - 1; sectionY >= bottomSectionY; sectionY--)
        {
            long sectionNode = SectionPos.AsLong(position.X, sectionY, position.Z);
            DataLayer? dataLayer = Storage.GetDataLayerToWrite(sectionNode);
            if (dataLayer is null)
                continue;

            int sectionMinY = SectionPos.SectionToBlockCoord(sectionY);
            int sectionMaxY = sectionMinY + 15;
            bool sourcesBelow = false;

            for (int z = 0; z < 16; z++)
            {
                for (int x = 0; x < 16; x++)
                {
                    int lowestSourceY = sources.GetLowestSourceY(x, z);
                    if (lowestSourceY > sectionMaxY)
                        continue;

                    int northLowestSourceY = z == 0
                        ? northSources.GetLowestSourceY(x, 15)
                        : sources.GetLowestSourceY(x, z - 1);
                    int southLowestSourceY = z == 15
                        ? southSources.GetLowestSourceY(x, 0)
                        : sources.GetLowestSourceY(x, z + 1);
                    int westLowestSourceY = x == 0
                        ? westSources.GetLowestSourceY(15, z)
                        : sources.GetLowestSourceY(x - 1, z);
                    int eastLowestSourceY = x == 15
                        ? eastSources.GetLowestSourceY(0, z)
                        : sources.GetLowestSourceY(x + 1, z);
                    int neighborLowestSourceY = Math.Max(
                        Math.Max(northLowestSourceY, southLowestSourceY),
                        Math.Max(westLowestSourceY, eastLowestSourceY));

                    for (int y = sectionMaxY; y >= Math.Max(sectionMinY, lowestSourceY); y--)
                    {
                        dataLayer.Set(x, SectionPos.SectionRelative(y), z, MAX_LEVEL);
                        if (y != lowestSourceY && y >= neighborLowestSourceY)
                            continue;

                        long blockNode = BlockPos.AsLong(
                            sectionMinX + x,
                            y,
                            sectionMinZ + z);
                        EnqueueIncrease(
                            blockNode,
                            SkyQueueEntry.IncreaseSkySourceInDirections(
                                down: y == lowestSourceY,
                                north: y < northLowestSourceY,
                                south: y < southLowestSourceY,
                                west: y < westLowestSourceY,
                                east: y < eastLowestSourceY));
                    }

                    if (lowestSourceY >= sectionMinY)
                        continue;

                    sourcesBelow = true;
                }
            }

            if (!sourcesBelow)
                break;
        }
    }
}
