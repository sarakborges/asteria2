using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Content.Fluids;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;
using Asteria.World.LevelGen;

namespace Asteria.World.Level;

/// <summary>
/// Owns one in-memory, single-player Overworld generator and the proto chunks
/// generated from it. Chunk generation is synchronous and serialized so the
/// shared RandomState is never evaluated concurrently.
/// </summary>
public sealed class OverworldWorld : BlockGetter
{
    private readonly object _sync = new();
    private readonly NoiseBasedChunkGenerator _generator;
    private readonly LevelHeightAccessor _heightAccessor;
    private readonly PalettedContainerFactory _containerFactory;
    private readonly Dictionary<ChunkPos, ProtoChunk> _chunks = new();

    public long Seed => _generator.Seed;

    public int MinY => _heightAccessor.GetMinY();

    public int Height => _heightAccessor.GetHeight();

    public int GetMinY() => _heightAccessor.GetMinY();

    public int GetHeight() => _heightAccessor.GetHeight();

    public BlockState GetBlockState(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (IsOutsideBuildHeight(position.GetY()))
            return Blocks.VOID_AIR.DefaultState;

        return GetChunk(ChunkPos.Containing(position)).GetBlockState(position);
    }

    public FluidState GetFluidState(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (IsOutsideBuildHeight(position.GetY()))
            return Fluids.EMPTY.DefaultFluidState;

        return GetChunk(ChunkPos.Containing(position)).GetFluidState(position);
    }

    public int LoadedChunkCount
    {
        get
        {
            lock (_sync)
                return _chunks.Count;
        }
    }

    public OverworldWorld(long seed, LevelHeightAccessor? heightAccessor = null)
    {
        RegistryBootstrap.Initialize();

        _generator = NoiseBasedChunkGenerator.CreateOverworld(seed);
        _heightAccessor = heightAccessor ??
            LevelHeightAccessor.Create(_generator.GetMinY(), _generator.GetGenDepth());

        if (_heightAccessor.GetHeight() <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(heightAccessor),
                "World height must be positive.");

        _containerFactory = PalettedContainerFactory.CreateBuiltIn();
    }

    /// <summary>
    /// Gets a cached chunk or generates its BIOMES and TERRAIN stages on first access.
    /// A failed generation is not inserted into the world's chunk cache.
    /// </summary>
    public ProtoChunk GetChunk(ChunkPos position)
    {
        EnsureValidPosition(position);

        lock (_sync)
        {
            if (_chunks.TryGetValue(position, out ProtoChunk? existing))
                return existing;

            var chunk = new ProtoChunk(
                position,
                _heightAccessor,
                _containerFactory);
            _generator.BuildTerrain(chunk);

            if (!chunk.GetPersistedStatus().IsOrAfter(ChunkStatus.TERRAIN))
                throw new InvalidOperationException(
                    $"Chunk {position} did not reach TERRAIN during Overworld generation.");

            _chunks.Add(position, chunk);
            return chunk;
        }
    }

    public bool TryGetChunk(ChunkPos position, out ProtoChunk? chunk)
    {
        lock (_sync)
            return _chunks.TryGetValue(position, out chunk);
    }

    /// <summary>
    /// Removes a chunk from the in-memory cache. This does not persist or mutate
    /// the returned ProtoChunk; callers must retain/save it separately when needed.
    /// </summary>
    public bool UnloadChunk(ChunkPos position)
    {
        lock (_sync)
            return _chunks.Remove(position);
    }

    private static void EnsureValidPosition(ChunkPos position)
    {
        if (!position.IsValid())
            throw new ArgumentOutOfRangeException(
                nameof(position),
                position,
                "Chunk coordinates exceed the supported world-generation bounds.");
    }
}
