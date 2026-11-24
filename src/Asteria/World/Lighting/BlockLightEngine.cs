using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Core;
using Asteria.World;

namespace Asteria.World.Lighting;

using BlockQueueEntry = LightEngine<BlockLightSectionStorage.BlockDataLayerStorageMap, BlockLightSectionStorage>.QueueEntry;

/// <summary>
/// Propagates light emitted by blocks using the shared vanilla light queues.
/// Port of Minecraft Java 26.3 BlockLightEngine.
/// </summary>
public sealed class BlockLightEngine
    : LightEngine<BlockLightSectionStorage.BlockDataLayerStorageMap, BlockLightSectionStorage>
{
    private readonly BlockPos.MutableBlockPos _mutablePosition = new();

    public BlockLightEngine(LightChunkGetter chunkSource)
        : this(chunkSource, new BlockLightSectionStorage(chunkSource))
    {
    }

    public BlockLightEngine(
        LightChunkGetter chunkSource,
        BlockLightSectionStorage storage)
        : base(chunkSource, storage)
    {
    }

    protected override void CheckNode(long blockNode)
    {
        long sectionNode = SectionPos.BlockToSection(blockNode);
        if (!Storage.StoringLightForSection(sectionNode))
            return;

        BlockState state = GetState(_mutablePosition.Set(blockNode));
        int emission = GetEmission(blockNode, state);
        int oldLevel = Storage.GetStoredLevel(blockNode);

        if (emission < oldLevel)
        {
            Storage.SetStoredLevel(blockNode, 0);
            EnqueueDecrease(blockNode, BlockQueueEntry.DecreaseAllDirections(oldLevel));
        }
        else
        {
            EnqueueDecrease(blockNode, PULL_LIGHT_IN_ENTRY);
        }

        if (emission > 0)
        {
            EnqueueIncrease(
                blockNode,
                BlockQueueEntry.IncreaseLightFromEmission(emission, IsEmptyShape(state)));
        }
    }

    protected override void PropagateIncrease(long fromNode, long increaseData, int fromLevel)
    {
        BlockState? fromState = null;

        foreach (Direction direction in PROPAGATION_DIRECTIONS)
        {
            if (!BlockQueueEntry.ShouldPropagateInDirection(increaseData, direction))
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
                fromState = BlockQueueEntry.IsFromEmptyShape(increaseData)
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
                    BlockQueueEntry.IncreaseSkipOneDirection(
                        newToLevel,
                        IsEmptyShape(toState),
                        direction.GetOpposite()));
            }
        }
    }

    protected override void PropagateDecrease(long fromNode, long decreaseData)
    {
        int oldFromLevel = BlockQueueEntry.GetFromLevel(decreaseData);

        foreach (Direction direction in PROPAGATION_DIRECTIONS)
        {
            if (!BlockQueueEntry.ShouldPropagateInDirection(decreaseData, direction))
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
                BlockState toState = GetState(_mutablePosition.Set(toNode));
                int toEmission = GetEmission(toNode, toState);
                Storage.SetStoredLevel(toNode, 0);

                if (toEmission < toLevel)
                {
                    EnqueueDecrease(
                        toNode,
                        BlockQueueEntry.DecreaseSkipOneDirection(
                            toLevel,
                            direction.GetOpposite()));
                }

                if (toEmission > 0)
                {
                    EnqueueIncrease(
                        toNode,
                        BlockQueueEntry.IncreaseLightFromEmission(
                            toEmission,
                            IsEmptyShape(toState)));
                }
            }
            else
            {
                EnqueueIncrease(
                    toNode,
                    BlockQueueEntry.IncreaseOnlyOneDirection(
                        toLevel,
                        fromEmptyShape: false,
                        direction.GetOpposite()));
            }
        }
    }

    private int GetEmission(long blockNode, BlockState state)
    {
        int emission = state.GetLightEmission();
        return emission > 0 && Storage.LightOnInSection(SectionPos.BlockToSection(blockNode))
            ? emission
            : 0;
    }

    public override void PropagateLightSources(ChunkPos position)
    {
        SetLightEnabled(position, enabled: true);
        LightChunk? chunk = ChunkSource.GetChunkForLighting(position.X, position.Z);
        if (chunk is null)
            return;

        chunk.FindBlockLightSources((lightPosition, state) =>
        {
            int emission = state.GetLightEmission();
            EnqueueIncrease(
                lightPosition.AsLong(),
                BlockQueueEntry.IncreaseLightFromEmission(emission, IsEmptyShape(state)));
        });
    }
}
