using Asteria.Content.Biomes;
using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Content.Fluids;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Structure;
using Asteria.World.Ticks;
using Asteria.World.Lighting;
using Asteria.Util;

namespace Asteria.World.Level;

/// <summary>
/// Owns one in-memory, single-player Overworld generator and the proto chunks
/// generated from it. Chunk generation is synchronous and serialized so the
/// shared RandomState is never evaluated concurrently.
/// </summary>
public sealed class OverworldWorld : BlockGetter, LightChunkGetter, WorldGenLevel
{
    private readonly object _sync = new();
    private readonly NoiseBasedChunkGenerator _generator;
    private readonly LevelHeightAccessor _heightAccessor;
    private readonly PalettedContainerFactory _containerFactory;
    private readonly Dictionary<ChunkPos, ProtoChunk> _chunks = new();
    private readonly Dictionary<ChunkPos, ProtoChunk> _generationChunks = new();
    private readonly BiomeManager _biomeManager;
    private readonly LevelLightEngine _lightEngine;
    private long _gameTime;
    private long _nextSubTickOrder;

    public long Seed => _generator.Seed;

    public NoiseBasedChunkGenerator Generator => _generator;

    public ChunkGeneratorStructureState StructureState { get; }

    public LevelLightEngine LightEngine => _lightEngine;

    public long GetGameTime() => Interlocked.Read(ref _gameTime);

    public void SetGameTime(long gameTime) =>
        Interlocked.Exchange(ref _gameTime, gameTime);

    public void ScheduleBlockTick(BlockPos position, Block block, int delay)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(block);
        ProtoChunk chunk = GetChunk(ChunkPos.Containing(position));
        chunk.GetBlockTicks().Schedule(new ScheduledTick<Block>(
            block,
            position,
            unchecked(GetGameTime() + delay),
            TickPriority.NORMAL,
            Interlocked.Increment(ref _nextSubTickOrder) - 1));
    }

    public int MinY => _heightAccessor.GetMinY();

    public int Height => _heightAccessor.GetHeight();

    public int GetMinY() => _heightAccessor.GetMinY();

    public int GetHeight() => _heightAccessor.GetHeight();

    public BlockState GetBlockState(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (_heightAccessor.IsOutsideBuildHeight(position.GetY()))
            return Blocks.VOID_AIR.DefaultState;

        return GetChunk(ChunkPos.Containing(position)).GetBlockState(position);
    }

    public bool EnsureCanWrite(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return !_heightAccessor.IsOutsideBuildHeight(position.GetY());
    }

    public bool SetBlock(BlockPos position, BlockState state, int updateFlags = 3)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!EnsureCanWrite(position))
            return false;

        _ = SetBlockState(position, state, updateFlags);
        if ((updateFlags & 16) == 0)
        {
            BlockPos? postProcessPosition = state.GetPostProcessPos(this, position);
            if (postProcessPosition is not null)
                GetChunk(ChunkPos.Containing(postProcessPosition)).MarkPosForPostProcessing(postProcessPosition);
        }

        return true;
    }

    public BlockState SetBlockState(
        BlockPos position,
        BlockState state,
        int flags = 3)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(state);
        if (_heightAccessor.IsOutsideBuildHeight(position.GetY()))
            return Blocks.VOID_AIR.DefaultState;

        ProtoChunk chunk = GetChunk(ChunkPos.Containing(position));
        BlockState previous = chunk.SetBlockState(position, state, flags);
        if (!ReferenceEquals(previous, state))
            chunk.MarkUnsaved();

        return previous;
    }

    public FluidState GetFluidState(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (_heightAccessor.IsOutsideBuildHeight(position.GetY()))
            return Fluids.EMPTY.DefaultFluidState;

        return GetChunk(ChunkPos.Containing(position)).GetFluidState(position);
    }

    public Holder<Biome> GetBiome(BlockPos position) =>
        _biomeManager.GetBiome(position);

    public int GetHeight(Heightmap.Types type, int blockX, int blockZ)
    {
        ArgumentNullException.ThrowIfNull(type);
        ChunkPos position = new(
            SectionPos.BlockToSectionCoord(blockX),
            SectionPos.BlockToSectionCoord(blockZ));
        return GetChunk(position).GetHeight(type, blockX, blockZ);
    }

    public int LoadedChunkCount
    {
        get
        {
            lock (_sync)
                return _chunks.Count;
        }
    }

    /// <summary>
    /// Number of proto chunks retained for the generation and lighting dependency window
    /// of every loaded, light-ready chunk.
    /// </summary>
    public int CachedGenerationChunkCount
    {
        get
        {
            lock (_sync)
                return _generationChunks.Count;
        }
    }

    /// <summary>
    /// Returns a stable, position-sorted snapshot. The returned collection does not
    /// hold the world's cache lock and is unaffected by subsequent loads or unloads.
    /// </summary>
    public IReadOnlyList<ProtoChunk> GetLoadedChunks()
    {
        lock (_sync)
        {
            ProtoChunk[] snapshot = _chunks.Values
                .OrderBy(chunk => chunk.GetPos().X)
                .ThenBy(chunk => chunk.GetPos().Z)
                .ToArray();
            return Array.AsReadOnly(snapshot);
        }
    }

    public OverworldWorld(long seed, LevelHeightAccessor? heightAccessor = null)
    {
        RegistryBootstrap.Initialize();

        _generator = NoiseBasedChunkGenerator.CreateOverworld(seed);
        StructureState = _generator.CreateStructureState(
            ChunkPos.Zero,
            BuiltInRegistries.StructureSet.ListElements());
        _biomeManager = new BiomeManager(
            _generator.CreateUncachedBiomeResolver(),
            BiomeManager.ObfuscateSeed(seed));
        _heightAccessor = heightAccessor ??
            LevelHeightAccessor.Create(_generator.GetMinY(), _generator.GetGenDepth());

        if (_heightAccessor.GetHeight() <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(heightAccessor),
                "World height must be positive.");

        _containerFactory = PalettedContainerFactory.CreateBuiltIn();
        _lightEngine = new LevelLightEngine(this, hasBlockLight: true, hasSkyLight: true);
    }

    /// <summary>
    /// Returns an already generated chunk to the light engine without forcing generation.
    /// Staging chunks become visible to lighting only after FEATURES has completed.
    /// </summary>
    public LightChunk? GetChunkForLighting(int chunkX, int chunkZ)
    {
        var position = new ChunkPos(chunkX, chunkZ);
        lock (_sync)
        {
            if (_chunks.TryGetValue(position, out ProtoChunk? loaded))
                return loaded;

            return _generationChunks.TryGetValue(position, out ProtoChunk? generated) &&
                   generated.GetPersistedStatus().IsOrAfter(ChunkStatus.FEATURES)
                ? generated
                : null;
        }
    }

    public BlockGetter GetLevel() => this;

    /// <summary>
    /// Creates the stage-scoped region for an already assembled dependency window.
    /// The scheduler owns chunk readiness; this level supplies the matching uncached
    /// biome resolver and world seed for the generation step.
    /// </summary>
    public WorldGenRegion CreateWorldGenRegion(
        ChunkStep generatingStep,
        ChunkAccess center,
        IEnumerable<ChunkAccess> availableChunks) =>
        new(
            this,
            generatingStep,
            center,
            availableChunks,
            _generator.CreateUncachedBiomeResolver());

    ChunkAccess WorldGenLevel.GetChunk(int chunkX, int chunkZ) =>
        GetChunk(new ChunkPos(chunkX, chunkZ));

    public ProtoChunk GetChunk(ChunkPos position)
    {
        EnsureValidPosition(position);

        lock (_sync)
        {
            if (_chunks.TryGetValue(position, out ProtoChunk? existing))
                return existing;

            ProtoChunk chunk = GetOrCreateGenerationChunk(position);
            if (!chunk.GetPersistedStatus().IsOrAfter(ChunkStatus.LIGHT))
            {
                try
                {
                    GenerateThroughLight(position);
                }
                catch
                {
                    PruneGenerationChunks();
                    throw;
                }
            }

            if (!chunk.GetPersistedStatus().IsOrAfter(ChunkStatus.LIGHT) ||
                !chunk.IsLightCorrect())
            {
                throw new InvalidOperationException(
                    $"Chunk {position} did not reach a light-correct LIGHT state through the generation pyramid.");
            }

            _chunks.Add(position, chunk);
            return chunk;
        }
    }

    private ProtoChunk GetOrCreateGenerationChunk(ChunkPos position)
    {
        if (_chunks.TryGetValue(position, out ProtoChunk? loaded))
        {
            _generationChunks[position] = loaded;
            return loaded;
        }

        if (_generationChunks.TryGetValue(position, out ProtoChunk? existing))
            return existing;

        var chunk = new ProtoChunk(position, _heightAccessor, _containerFactory);
        _generationChunks.Add(position, chunk);
        return chunk;
    }

    private void GenerateThroughLight(ChunkPos centerPosition)
    {
        ChunkPyramid pyramid = ChunkPyramid.GenerationPyramid;
        ChunkStep structureStartsStep = pyramid.GetStepTo(ChunkStatus.STRUCTURE_STARTS);
        ChunkStep structureReferencesStep = pyramid.GetStepTo(ChunkStatus.STRUCTURE_REFERENCES);
        ChunkStep biomesStep = pyramid.GetStepTo(ChunkStatus.BIOMES);
        ChunkStep terrainStep = pyramid.GetStepTo(ChunkStatus.TERRAIN);
        ChunkStep featuresStep = pyramid.GetStepTo(ChunkStatus.FEATURES);
        ChunkStep initializeLightStep = pyramid.GetStepTo(ChunkStatus.INITIALIZE_LIGHT);
        ChunkStep lightStep = pyramid.GetStepTo(ChunkStatus.LIGHT);

        (
            int structureStartsRadius,
            int structureReferencesRadius,
            int biomeRadius,
            int terrainRadius,
            int featuresRadius,
            int initializeLightRadius) = GetLightGenerationDependencyRadii();

        StaticCache2D<ChunkAccess?> chunks = StaticCache2D<ChunkAccess?>.Create(
            centerPosition.X,
            centerPosition.Z,
            structureStartsRadius,
            (chunkX, chunkZ) =>
                GetOrCreateGenerationChunk(new ChunkPos(chunkX, chunkZ)));

        var context = new WorldGenContext(
            this,
            _generator,
            _lightEngine,
            StructureState);

        foreach (ChunkPos dependencyPosition in
                 ChunkPos.RangeClosed(centerPosition, structureStartsRadius))
        {
            ProtoChunk dependencyChunk = (ProtoChunk)chunks.Get(
                dependencyPosition.X,
                dependencyPosition.Z);
            ApplyGenerationStep(
                structureStartsStep,
                context,
                chunks,
                dependencyChunk);
        }

        foreach (ChunkPos dependencyPosition in
                 ChunkPos.RangeClosed(centerPosition, structureReferencesRadius))
        {
            ProtoChunk dependencyChunk = (ProtoChunk)chunks.Get(
                dependencyPosition.X,
                dependencyPosition.Z);
            ApplyGenerationStep(
                structureReferencesStep,
                context,
                chunks,
                dependencyChunk);
        }

        foreach (ChunkPos dependencyPosition in
                 ChunkPos.RangeClosed(centerPosition, biomeRadius))
        {
            ProtoChunk dependencyChunk = (ProtoChunk)chunks.Get(
                dependencyPosition.X,
                dependencyPosition.Z);
            ApplyGenerationStep(
                biomesStep,
                context,
                chunks,
                dependencyChunk);
        }

        foreach (ChunkPos dependencyPosition in
                 ChunkPos.RangeClosed(centerPosition, terrainRadius))
        {
            ProtoChunk dependencyChunk = (ProtoChunk)chunks.Get(
                dependencyPosition.X,
                dependencyPosition.Z);
            ApplyGenerationStep(
                terrainStep,
                context,
                chunks,
                dependencyChunk);
        }

        foreach (ChunkPos dependencyPosition in
                 ChunkPos.RangeClosed(centerPosition, featuresRadius))
        {
            ProtoChunk dependencyChunk = (ProtoChunk)chunks.Get(
                dependencyPosition.X,
                dependencyPosition.Z);
            ApplyGenerationStep(
                featuresStep,
                context,
                chunks,
                dependencyChunk);
        }

        foreach (ChunkPos dependencyPosition in
                 ChunkPos.RangeClosed(centerPosition, initializeLightRadius))
        {
            ProtoChunk dependencyChunk = (ProtoChunk)chunks.Get(
                dependencyPosition.X,
                dependencyPosition.Z);
            ApplyGenerationStep(
                initializeLightStep,
                context,
                chunks,
                dependencyChunk);
        }

        ProtoChunk centerChunk = (ProtoChunk)chunks.Get(
            centerPosition.X,
            centerPosition.Z);
        ApplyGenerationStep(
            lightStep,
            context,
            chunks,
            centerChunk);
    }

    private static (
        int StructureStartsRadius,
        int StructureReferencesRadius,
        int BiomeRadius,
        int TerrainRadius,
        int FeaturesRadius,
        int InitializeLightRadius)
        GetLightGenerationDependencyRadii()
    {
        ChunkStep lightStep = ChunkPyramid.GenerationPyramid
            .GetStepTo(ChunkStatus.LIGHT);

        int structureStartsRadius =
            lightStep.GetAccumulatedRadiusOf(ChunkStatus.STRUCTURE_STARTS);
        int structureReferencesRadius =
            lightStep.GetAccumulatedRadiusOf(ChunkStatus.STRUCTURE_REFERENCES);
        int biomeRadius =
            lightStep.GetAccumulatedRadiusOf(ChunkStatus.BIOMES);
        int terrainRadius =
            lightStep.GetAccumulatedRadiusOf(ChunkStatus.TERRAIN);
        int featuresRadius =
            lightStep.GetAccumulatedRadiusOf(ChunkStatus.FEATURES);
        int initializeLightRadius =
            lightStep.GetAccumulatedRadiusOf(ChunkStatus.INITIALIZE_LIGHT);

        if (structureStartsRadius < 0 ||
            structureReferencesRadius < 0 ||
            biomeRadius < 0 ||
            terrainRadius < 0 ||
            featuresRadius < 0 ||
            initializeLightRadius < 0)
        {
            throw new InvalidOperationException(
                "The LIGHT dependency pyramid does not describe its required generation windows.");
        }

        return (
            structureStartsRadius,
            structureReferencesRadius,
            biomeRadius,
            terrainRadius,
            featuresRadius,
            initializeLightRadius);
    }

    private void PruneGenerationChunks()
    {
        if (_chunks.Count == 0)
        {
            _generationChunks.Clear();
            return;
        }

        int radius = GetLightGenerationDependencyRadii().StructureStartsRadius;
        var requiredChunks = new HashSet<ChunkPos>();
        foreach (ChunkPos loadedPosition in _chunks.Keys)
        {
            foreach (ChunkPos dependencyPosition in ChunkPos.RangeClosed(loadedPosition, radius))
                requiredChunks.Add(dependencyPosition);
        }

        foreach (ChunkPos cachedPosition in _generationChunks.Keys.ToArray())
        {
            if (!requiredChunks.Contains(cachedPosition))
                _generationChunks.Remove(cachedPosition);
        }
    }

    private static void ApplyGenerationStep(
        ChunkStep step,
        WorldGenContext context,
        StaticCache2D<ChunkAccess?> chunks,
        ProtoChunk chunk)
    {
        if (chunk.GetPersistedStatus().IsOrAfter(step.TargetStatus))
            return;

        ChunkAccess generatedChunk = step.Apply(context, chunks, chunk)
            .AsTask()
            .GetAwaiter()
            .GetResult();
        if (!ReferenceEquals(generatedChunk, chunk))
            throw new InvalidOperationException(
                $"The {step.TargetStatus} task returned a different chunk instance for {chunk.GetPos()}.");
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
        {
            if (!_chunks.Remove(position))
                return false;

            PruneGenerationChunks();
            return true;
        }
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
