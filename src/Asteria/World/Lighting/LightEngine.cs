using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Core;
using Asteria.Core.Shapes;

namespace Asteria.World.Lighting;

/// <summary>
/// Common contract and shape rules shared by the block and sky light engines.
/// </summary>
public abstract class LightEngine : LayerLightEventListener
{
    public abstract void CheckBlock(BlockPos position);

    public abstract bool HasLightWork();

    public abstract int RunLightUpdates();

    public abstract void UpdateSectionStatus(SectionPos sectionPosition, bool sectionEmpty);

    public abstract void SetLightEnabled(ChunkPos position, bool enabled);

    public abstract void PropagateLightSources(ChunkPos position);

    public abstract DataLayer? GetDataLayerData(SectionPos position);

    public abstract int GetLightValue(BlockPos position);

    public abstract void QueueSectionData(long sectionNode, DataLayer? data);

    public abstract void RetainData(ChunkPos position, bool retain);

    public abstract string GetDebugData(long sectionNode);


    public const int MAX_LEVEL = 15;
    protected const int MIN_OPACITY = 1;

    public static bool HasDifferentLightProperties(BlockState oldState, BlockState newState)
    {
        ArgumentNullException.ThrowIfNull(oldState);
        ArgumentNullException.ThrowIfNull(newState);

        if (ReferenceEquals(newState, oldState))
            return false;

        return newState.GetLightDampening() != oldState.GetLightDampening() ||
            newState.GetLightEmission() != oldState.GetLightEmission() ||
            newState.UseShapeForLightOcclusion ||
            oldState.UseShapeForLightOcclusion;
    }

    public static int GetLightDampeningInto(
        BlockState fromState,
        BlockState toState,
        Direction direction,
        int simpleOpacity)
    {
        ArgumentNullException.ThrowIfNull(fromState);
        ArgumentNullException.ThrowIfNull(toState);
        ArgumentNullException.ThrowIfNull(direction);

        bool fromEmpty = IsEmptyShape(fromState);
        bool toEmpty = IsEmptyShape(toState);
        if (fromEmpty && toEmpty)
            return simpleOpacity;

        VoxelShape fromShape = fromEmpty ? Shapes.Empty() : fromState.OcclusionShape;
        VoxelShape toShape = toEmpty ? Shapes.Empty() : toState.OcclusionShape;
        return Shapes.MergedFaceOccludes(fromShape, toShape, direction)
            ? 16
            : simpleOpacity;
    }

    public static VoxelShape GetOcclusionShape(BlockState state, Direction direction)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(direction);

        return IsEmptyShape(state)
            ? Shapes.Empty()
            : state.GetFaceOcclusionShape(direction);
    }

    protected static bool IsEmptyShape(BlockState state) =>
        !state.CanOcclude || !state.UseShapeForLightOcclusion;

    protected static bool ShapeOccludes(
        BlockState fromState,
        BlockState toState,
        Direction direction)
    {
        VoxelShape fromShape = GetOcclusionShape(fromState, direction);
        VoxelShape toShape = GetOcclusionShape(toState, direction.GetOpposite());
        return Shapes.FaceShapeOccludes(fromShape, toShape);
    }
}

/// <summary>
/// Queue-based light propagation shared by the SKY and BLOCK specializations.
/// </summary>
public abstract class LightEngine<TMap, TStorage> : LightEngine
    where TMap : DataLayerStorageMap<TMap>
    where TStorage : LayerLightSectionStorage<TMap>
{
    private const int MIN_QUEUE_SIZE = 512;
    private const int CACHE_SIZE = 2;

    protected static readonly long PULL_LIGHT_IN_ENTRY = QueueEntry.DecreaseAllDirections(1);
    protected static readonly Direction[] PROPAGATION_DIRECTIONS = Direction.GetValues();

    protected readonly LightChunkGetter ChunkSource;
    protected readonly TStorage Storage;

    private readonly HashSet<long> _blockNodesToCheck = new(MIN_QUEUE_SIZE);
    private readonly Queue<long> _decreaseQueue = new();
    private readonly Queue<long> _increaseQueue = new();
    private readonly long[] _lastChunkPositions = new long[CACHE_SIZE];
    private readonly LightChunk?[] _lastChunks = new LightChunk?[CACHE_SIZE];

    protected LightEngine(LightChunkGetter chunkSource, TStorage storage)
    {
        ArgumentNullException.ThrowIfNull(chunkSource);
        ArgumentNullException.ThrowIfNull(storage);

        ChunkSource = chunkSource;
        Storage = storage;
        ClearChunkCache();
    }

    public override void CheckBlock(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        _blockNodesToCheck.Add(position.AsLong());
    }

    public override void QueueSectionData(long sectionNode, DataLayer? data) =>
        Storage.QueueSectionData(sectionNode, data);

    public override void RetainData(ChunkPos position, bool retain)
    {
        Storage.RetainData(SectionPos.GetZeroNode(position.X, position.Z), retain);
    }

    public override void UpdateSectionStatus(SectionPos sectionPosition, bool sectionEmpty) =>
        Storage.UpdateSectionStatus(sectionPosition.AsLong(), sectionEmpty);

    public override void SetLightEnabled(ChunkPos position, bool enabled) =>
        Storage.SetLightEnabled(SectionPos.GetZeroNode(position.X, position.Z), enabled);

    public override int RunLightUpdates()
    {
        foreach (long blockNode in _blockNodesToCheck)
            CheckNode(blockNode);

        _blockNodesToCheck.Clear();

        int count = PropagateDecreases();
        ClearChunkCache();
        Storage.MarkNewInconsistencies();
        Storage.SwapSectionMap();
        return count + PropagateIncreases();
    }

    private int PropagateIncreases()
    {
        int count = 0;
        while (_increaseQueue.Count > 0)
        {
            long fromNode = _increaseQueue.Dequeue();
            long increaseData = _increaseQueue.Dequeue();
            int fromLevel = Storage.GetStoredLevel(fromNode);
            int fromTargetLevel = QueueEntry.GetFromLevel(increaseData);

            if (QueueEntry.IsIncreaseFromEmission(increaseData) && fromLevel < fromTargetLevel)
            {
                Storage.SetStoredLevel(fromNode, fromTargetLevel);
                fromLevel = fromTargetLevel;
            }

            if (fromLevel == fromTargetLevel)
                PropagateIncrease(fromNode, increaseData, fromLevel);

            count++;
        }

        return count;
    }

    private int PropagateDecreases()
    {
        int count = 0;
        while (_decreaseQueue.Count > 0)
        {
            long fromNode = _decreaseQueue.Dequeue();
            long decreaseData = _decreaseQueue.Dequeue();
            PropagateDecrease(fromNode, decreaseData);
            count++;
        }

        return count;
    }

    protected void EnqueueDecrease(long fromNode, long decreaseData)
    {
        _decreaseQueue.Enqueue(fromNode);
        _decreaseQueue.Enqueue(decreaseData);
    }

    protected void EnqueueIncrease(long fromNode, long increaseData)
    {
        _increaseQueue.Enqueue(fromNode);
        _increaseQueue.Enqueue(increaseData);
    }

    public override bool HasLightWork() =>
        Storage.HasInconsistencies() ||
        _blockNodesToCheck.Count > 0 ||
        _decreaseQueue.Count > 0 ||
        _increaseQueue.Count > 0;

    public override DataLayer? GetDataLayerData(SectionPos position) =>
        Storage.GetDataLayerData(position.AsLong());

    public override int GetLightValue(BlockPos position) =>
        Storage.GetLightValueForEngine(position.AsLong());

    public override string GetDebugData(long sectionNode) =>
        Storage.GetDebugSectionType(sectionNode) switch
        {
            LayerLightSectionStorage<TMap>.SectionType.Empty => "2",
            LayerLightSectionStorage<TMap>.SectionType.LightOnly => "1",
            LayerLightSectionStorage<TMap>.SectionType.LightAndData => "0",
            _ => throw new ArgumentOutOfRangeException(nameof(sectionNode))
        };

    public LayerLightSectionStorage<TMap>.SectionType GetDebugSectionType(long sectionNode) =>
        Storage.GetDebugSectionType(sectionNode);

    protected BlockState GetState(BlockPos position)
    {
        int chunkX = SectionPos.BlockToSectionCoord(position.GetX());
        int chunkZ = SectionPos.BlockToSectionCoord(position.GetZ());
        LightChunk? chunk = GetChunk(chunkX, chunkZ);
        return chunk is null
            ? Blocks.BEDROCK.DefaultState
            : chunk.GetBlockState(position);
    }

    protected int GetOpacity(BlockState state) =>
        Math.Max(MIN_OPACITY, state.GetLightDampening());

    protected LightChunk? GetChunk(int chunkX, int chunkZ)
    {
        long packedPosition = ChunkPos.Pack(chunkX, chunkZ);
        for (int index = 0; index < CACHE_SIZE; index++)
        {
            if (packedPosition == _lastChunkPositions[index])
                return _lastChunks[index];
        }

        LightChunk? chunk = ChunkSource.GetChunkForLighting(chunkX, chunkZ);
        for (int index = CACHE_SIZE - 1; index > 0; index--)
        {
            _lastChunkPositions[index] = _lastChunkPositions[index - 1];
            _lastChunks[index] = _lastChunks[index - 1];
        }

        _lastChunkPositions[0] = packedPosition;
        _lastChunks[0] = chunk;
        return chunk;
    }

    private void ClearChunkCache()
    {
        Array.Fill(_lastChunkPositions, ChunkPos.InvalidChunkPos);
        Array.Clear(_lastChunks);
    }

    protected abstract void CheckNode(long blockNode);

    protected abstract void PropagateIncrease(long fromNode, long increaseData, int fromLevel);

    protected abstract void PropagateDecrease(long fromNode, long decreaseData);

    public static class QueueEntry
    {
        private const long FROM_LEVEL_BITS = 4L;
        private const long DIRECTION_BITS = 6L;
        private const long LEVEL_MASK = 15L;
        private const long DIRECTIONS_MASK = 1008L;
        private const long FLAG_FROM_EMPTY_SHAPE = 1024L;
        private const long FLAG_INCREASE_FROM_EMISSION = 2048L;

        public static long DecreaseSkipOneDirection(int oldFromLevel, Direction skipDirection)
        {
            long data = WithoutDirection(DIRECTIONS_MASK, skipDirection);
            return WithLevel(data, oldFromLevel);
        }

        public static long DecreaseAllDirections(int oldFromLevel) =>
            WithLevel(DIRECTIONS_MASK, oldFromLevel);

        public static long IncreaseLightFromEmission(int newFromLevel, bool fromEmptyShape)
        {
            long data = DIRECTIONS_MASK | FLAG_INCREASE_FROM_EMISSION;
            if (fromEmptyShape)
                data |= FLAG_FROM_EMPTY_SHAPE;
            return WithLevel(data, newFromLevel);
        }

        public static long IncreaseSkipOneDirection(
            int newFromLevel,
            bool fromEmptyShape,
            Direction skipDirection)
        {
            long data = WithoutDirection(DIRECTIONS_MASK, skipDirection);
            if (fromEmptyShape)
                data |= FLAG_FROM_EMPTY_SHAPE;
            return WithLevel(data, newFromLevel);
        }

        public static long IncreaseOnlyOneDirection(
            int newFromLevel,
            bool fromEmptyShape,
            Direction direction)
        {
            long data = 0L;
            if (fromEmptyShape)
                data |= FLAG_FROM_EMPTY_SHAPE;
            data = WithDirection(data, direction);
            return WithLevel(data, newFromLevel);
        }

        public static long IncreaseSkySourceInDirections(
            bool down,
            bool north,
            bool south,
            bool west,
            bool east)
        {
            long data = WithLevel(0L, 15);
            if (down)
                data = WithDirection(data, Direction.DOWN);
            if (north)
                data = WithDirection(data, Direction.NORTH);
            if (south)
                data = WithDirection(data, Direction.SOUTH);
            if (west)
                data = WithDirection(data, Direction.WEST);
            if (east)
                data = WithDirection(data, Direction.EAST);
            return data;
        }

        public static int GetFromLevel(long entry) =>
            (int)(entry & LEVEL_MASK);

        public static bool IsFromEmptyShape(long entry) =>
            (entry & FLAG_FROM_EMPTY_SHAPE) != 0L;

        public static bool IsIncreaseFromEmission(long entry) =>
            (entry & FLAG_INCREASE_FROM_EMISSION) != 0L;

        public static bool ShouldPropagateInDirection(long entry, Direction direction) =>
            (entry & (1L << (direction.Get3DDataValue() + 4))) != 0L;

        private static long WithLevel(long entry, int level) =>
            (entry & ~LEVEL_MASK) | ((long)level & LEVEL_MASK);

        private static long WithDirection(long entry, Direction direction) =>
            entry | (1L << (direction.Get3DDataValue() + 4));

        private static long WithoutDirection(long entry, Direction direction) =>
            entry & ~(1L << (direction.Get3DDataValue() + 4));
    }
}
