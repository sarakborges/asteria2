using System.Diagnostics;
using Asteria.Content.Biomes;
using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Content.Fluids;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Level;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.Ticks;

namespace Asteria.World.LevelGen;

/// <summary>
/// Stage-scoped world access used during chunk decoration. Neighbor chunks are exposed
/// only when they satisfy the direct dependency status for their Chebyshev-distance ring;
/// block writes are additionally restricted by the step's write radius.
/// </summary>
public sealed class WorldGenRegion : WorldGenLevel
{
    private readonly OverworldWorld _level;
    private readonly ChunkStep _generatingStep;
    private readonly ChunkAccess _center;
    private readonly Dictionary<ChunkPos, ChunkAccess> _chunks;
    private readonly BiomeManager _biomeManager;
    private string? _currentlyGenerating;
    private long _nextSubTickOrder;

    public WorldGenRegion(
        OverworldWorld level,
        ChunkStep generatingStep,
        ChunkAccess center,
        IEnumerable<ChunkAccess> availableChunks,
        BiomeResolver uncachedBiomeResolver)
    {
        _level = level ?? throw new ArgumentNullException(nameof(level));
        _generatingStep = generatingStep ?? throw new ArgumentNullException(nameof(generatingStep));
        _center = center ?? throw new ArgumentNullException(nameof(center));
        ArgumentNullException.ThrowIfNull(availableChunks);
        ArgumentNullException.ThrowIfNull(uncachedBiomeResolver);
        _biomeManager = new BiomeManager(
            uncachedBiomeResolver,
            BiomeManager.ObfuscateSeed(_level.Seed));

        if (generatingStep.DirectDependencies.Size() == 0)
            throw new ArgumentException(
                "A world-generation region requires at least one direct dependency ring.",
                nameof(generatingStep));

        ChunkPos centerPosition = center.GetPos();
        int centerMaxAllowedStatusIndex = generatingStep.DirectDependencies.Get(0).GetIndex();
        if (center.GetPersistedStatus().GetIndex() < centerMaxAllowedStatusIndex)
        {
            throw new ArgumentException(
                $"Center chunk {centerPosition} is at {center.GetPersistedStatus()}, " +
                $"but step {generatingStep.TargetStatus} requires " +
                $"{generatingStep.DirectDependencies.Get(0)} at radius zero.",
                nameof(center));
        }

        _chunks = new Dictionary<ChunkPos, ChunkAccess>();
        AddIfAllowed(center);

        foreach (ChunkAccess chunk in availableChunks)
        {
            ArgumentNullException.ThrowIfNull(chunk);
            AddIfAllowed(chunk);
        }

        if (!_chunks.TryGetValue(centerPosition, out ChunkAccess? cachedCenter) ||
            !ReferenceEquals(cachedCenter, center))
        {
            throw new InvalidOperationException(
                $"The center chunk {centerPosition} was not retained in the world-generation cache.");
        }
    }

    public long Seed => _level.Seed;

    public long GetGameTime() => _level.GetGameTime();

    public void ScheduleBlockTick(BlockPos position, Block block, int delay)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(block);
        GetChunk(position).GetBlockTicks().Schedule(new ScheduledTick<Block>(
            block,
            position,
            unchecked(GetGameTime() + delay),
            TickPriority.NORMAL,
            Interlocked.Increment(ref _nextSubTickOrder) - 1));
    }

    public ChunkStep GeneratingStep => _generatingStep;

    public ChunkPos GetCenter() => _center.GetPos();

    public int GetMinY() => _center.GetMinY();

    public int GetHeight() => _center.GetHeight();

    public bool HasChunk(int chunkX, int chunkZ)
    {
        int distance = _center.GetPos().GetChessboardDistance(chunkX, chunkZ);
        return distance < _generatingStep.DirectDependencies.Size();
    }

    public ChunkAccess GetChunk(int chunkX, int chunkZ) =>
        GetChunk(chunkX, chunkZ, ChunkStatus.EMPTY);

    public ChunkAccess GetChunk(int chunkX, int chunkZ, ChunkStatus targetStatus)
    {
        ArgumentNullException.ThrowIfNull(targetStatus);

        ChunkPos centerPosition = _center.GetPos();
        int distance = centerPosition.GetChessboardDistance(chunkX, chunkZ);
        ChunkDependencies dependencies = _generatingStep.DirectDependencies;
        ChunkStatus? maxAllowedStatus = distance < dependencies.Size()
            ? dependencies.Get(distance)
            : null;

        if (maxAllowedStatus is not null &&
            targetStatus.IsOrBefore(maxAllowedStatus) &&
            _chunks.TryGetValue(new ChunkPos(chunkX, chunkZ), out ChunkAccess? chunk))
        {
            return chunk;
        }

        string actualStatus = _chunks.TryGetValue(new ChunkPos(chunkX, chunkZ), out ChunkAccess? cached)
            ? cached.GetPersistedStatus().ToString()
            : "[out of cache bounds or unavailable]";
        throw new InvalidOperationException(
            $"Chunk request is outside the world-generation dependencies: requested [{chunkX}, {chunkZ}], " +
            $"center {centerPosition}, generating { _generatingStep.TargetStatus }, " +
            $"requested status {targetStatus}, actual status {actualStatus}, " +
            $"maximum allowed status {maxAllowedStatus?.ToString() ?? "null"}, " +
            $"dependencies {_generatingStep.DirectDependencies}.");
    }

    public ChunkAccess GetChunk(ChunkPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return GetChunk(position.X, position.Z);
    }

    public ChunkAccess GetChunk(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        ChunkPos chunkPosition = ChunkPos.Containing(position);
        return GetChunk(chunkPosition);
    }

    public BlockState GetBlockState(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        ChunkPos chunkPosition = ChunkPos.Containing(position);
        WarnIfReadOutsideWriteZone(chunkPosition);
        return GetChunk(chunkPosition).GetBlockState(position);
    }

    public FluidState GetFluidState(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        ChunkPos chunkPosition = ChunkPos.Containing(position);
        WarnIfReadOutsideWriteZone(chunkPosition);
        return GetChunk(chunkPosition).GetFluidState(position);
    }

    public Holder<Biome> GetBiome(BlockPos position) =>
        _biomeManager.GetBiome(position);

    public int GetHeight(Heightmap.Types type, int blockX, int blockZ)
    {
        ArgumentNullException.ThrowIfNull(type);
        ChunkPos chunkPosition = new(
            SectionPos.BlockToSectionCoord(blockX),
            SectionPos.BlockToSectionCoord(blockZ));
        WarnIfReadOutsideWriteZone(chunkPosition);
        return GetChunk(chunkPosition).GetHeight(type, blockX, blockZ);
    }

    public bool IsWithinWriteZone(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return IsWithinWriteZone(ChunkPos.Containing(position));
    }

    public bool IsWithinWriteZone(ChunkPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        int radius = _generatingStep.BlockStateWriteRadius;
        ChunkPos center = _center.GetPos();
        return Math.Abs(center.X - position.X) <= radius &&
               Math.Abs(center.Z - position.Z) <= radius;
    }

    public bool EnsureCanWrite(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (IsWithinWriteZone(position))
            return true;

        ChunkPos target = ChunkPos.Containing(position);
        Trace.TraceWarning(
            "Detected setBlock outside the world-generation write radius: " +
            $"chunk [{target.X}, {target.Z}], position {position}, " +
            $"center {_center.GetPos()}, step {_generatingStep.TargetStatus}, " +
            $"write radius {_generatingStep.BlockStateWriteRadius}" +
            (_currentlyGenerating is null ? string.Empty : $", currently generating: {_currentlyGenerating}"));
        return false;
    }

    public bool SetBlock(BlockPos position, BlockState state, int updateFlags = 3)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(state);
        if (!EnsureCanWrite(position))
            return false;

        ChunkAccess chunk = GetChunk(position);
        BlockState previousState = chunk.SetBlockState(position, state, updateFlags);
        if (!ReferenceEquals(previousState, state))
            chunk.MarkUnsaved();

        if ((updateFlags & 16) == 0)
        {
            BlockPos? postProcessPosition = state.GetPostProcessPos(this, position);
            if (postProcessPosition is not null)
                GetChunk(postProcessPosition).MarkPosForPostProcessing(postProcessPosition);
        }

        return true;
    }

    public void SetCurrentlyGenerating(string? description) =>
        _currentlyGenerating = description;

    private void AddIfAllowed(ChunkAccess chunk)
    {
        ChunkPos center = _center.GetPos();
        ChunkPos position = chunk.GetPos();
        int distance = center.GetChessboardDistance(position.X, position.Z);
        ChunkDependencies dependencies = _generatingStep.DirectDependencies;
        if (distance >= dependencies.Size())
            return;

        ChunkStatus requiredStatus = dependencies.Get(distance);
        if (!chunk.GetPersistedStatus().IsOrAfter(requiredStatus))
            return;

        if (!_chunks.TryAdd(position, chunk) &&
            !ReferenceEquals(_chunks[position], chunk))
        {
            throw new ArgumentException(
                $"Multiple chunk instances were supplied for position {position}.",
                nameof(chunk));
        }
    }

    private void WarnIfReadOutsideWriteZone(ChunkPos position)
    {
        if (position == _center.GetPos() || IsWithinWriteZone(position))
            return;

        Trace.TraceWarning(
            "Detected world-generation block read outside the write radius: " +
            $"chunk [{position.X}, {position.Z}], center {_center.GetPos()}, " +
            $"step {_generatingStep.TargetStatus}, write radius {_generatingStep.BlockStateWriteRadius}" +
            (_currentlyGenerating is null ? string.Empty : $", currently generating: {_currentlyGenerating}"));
    }
}
