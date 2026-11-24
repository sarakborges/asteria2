using System.Collections.Concurrent;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Content.Biomes;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.Synth;
using Asteria.World.LevelGen.Materials;

namespace Asteria.World.LevelGen;

/// <summary>
/// Seeded world-generation random state, density samplers and pooled density-buffer ownership
/// from Minecraft Java 26.3. Material-system ownership follows with the material-generation stage.
/// </summary>
public sealed class RandomState : DensityFunction.CompileContext
{
    private readonly long _seed;
    private readonly bool _useLegacyRandomSource;
    private readonly HolderGetter<NormalNoise> _noises;
    private readonly PositionalRandomFactory _random;
    private readonly NoiseRouter _router;
    private readonly DensityFunctionCompiler _densityFunctionCompiler;

    private readonly ConcurrentDictionary<RegistryKey<NormalNoise>, Lazy<Noise>> _noiseInstances = new();
    private readonly ConcurrentDictionary<ResourceLocation, Lazy<PositionalRandomFactory>> _positionalRandoms = new();

    private readonly object _densityBufferPoolLock = new();
    private readonly object _materialSystemLock = new();
    private MaterialSystem? _materialSystem;
    private NoiseGeneratorSettings? _materialSystemSettings;
    private readonly List<DensityBufferPool> _densityBufferPools = new(16);

    private const int MaxDensityBufferPools = 16;
    private const int MaxDensityBufferAgeTicks = 20;

    public long Seed => _seed;
    public bool UseLegacyRandomSource => _useLegacyRandomSource;
    public NoiseRouter Router => _router;

    /// <summary>
    /// Returns the material system owned by this seeded world-generation state.
    /// A RandomState cannot be rebound to a different material configuration.
    /// </summary>
    public MaterialSystem GetOrCreateMaterialSystem(NoiseGeneratorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_materialSystemLock)
        {
            if (_materialSystem is null)
            {
                _materialSystem = new MaterialSystem(
                    this,
                    settings.DefaultBlock,
                    settings.SeaLevel,
                    settings.NoiseRouter.ChunkSurfaceLevel,
                    _random);
                _materialSystemSettings = settings;
            }
            else if (!_materialSystemSettings!.Equals(settings))
            {
                throw new InvalidOperationException(
                    "A RandomState cannot be reused with different NoiseGeneratorSettings material inputs.");
            }

            return _materialSystem;
        }
    }

    public RandomState(
        HolderGetter<NormalNoise> noises,
        long seed,
        bool useLegacyRandomSource,
        NoiseRouter router)
    {
        _noises = noises ?? throw new ArgumentNullException(nameof(noises));
        ArgumentNullException.ThrowIfNull(router);

        _seed = seed;
        _useLegacyRandomSource = useLegacyRandomSource;
        _router = router;

        IRandomSource worldgenRandom = useLegacyRandomSource
            ? new LegacyRandomSource(seed)
            : new XoroshiroRandomSource(seed);
        _random = worldgenRandom.ForkPositional();
        _densityFunctionCompiler = new DensityFunctionCompiler(this);
    }

    public static RandomState Create(
        HolderGetter<NormalNoise> noises,
        long seed,
        bool useLegacyRandomSource,
        NoiseRouter router) =>
        new(noises, seed, useLegacyRandomSource, router);

    /// <summary>
    /// Resolves a registered normal-noise holder and memoizes its seeded noise instance.
    /// </summary>
    public Noise GetOrCreateNoise(RegistryKey<NormalNoise> noise)
    {
        if (!noise.IsFor(Registries.NOISE))
            throw new ArgumentException(
                $"Noise key {noise} does not belong to {Registries.NOISE}.",
                nameof(noise));

        Lazy<Noise> lazy = _noiseInstances.GetOrAdd(
            noise,
            key => new Lazy<Noise>(
                () =>
                {
                    Holder<NormalNoise> holder = _noises.GetOrThrow(key);
                    return holder.Value.Create(_random.FromHashOf(key.Location));
                },
                LazyThreadSafetyMode.ExecutionAndPublication));

        return lazy.Value;
    }

    /// <summary>
    /// Gets the positional random factory associated with the name's hash.
    /// </summary>
    public PositionalRandomFactory GetOrCreateRandomFactory(ResourceLocation name)
    {
        ArgumentNullException.ThrowIfNull(name);

        Lazy<PositionalRandomFactory> lazy = _positionalRandoms.GetOrAdd(
            name,
            key => new Lazy<PositionalRandomFactory>(
                () => _random.FromHashOf(key).ForkPositional(),
                LazyThreadSafetyMode.ExecutionAndPublication));

        return lazy.Value;
    }

    public DensitySampler GetSampler(DensityFunction function) =>
        _densityFunctionCompiler.GetSampler(function);

    public DensitySamplerSet SamplersWithContext(SamplerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new BoundDensitySamplerSet(this, context);
    }

    public Climate.Sampler CreateClimateSampler(SamplerContext context) =>
        _router.CreateClimateSampler(SamplersWithContext(context));

    public float SampleBlockValueUncached(
        DensityFunction function,
        int blockX,
        int blockY,
        int blockZ) =>
        GetSampler(function).SampleValue(
            SamplerContext.EmptyUncached,
            blockX,
            blockY,
            blockZ);

    public Noise CreateNoiseSampler(Holder<NormalNoise> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (parameters.Is(Noises.TEMPERATURE_NETHER))
        {
#pragma warning disable CS0618
            return parameters.Value.CreateForLegacyNetherBiome(
                new LegacyRandomSource(_seed));
#pragma warning restore CS0618
        }

        if (parameters.Is(Noises.VEGETATION_NETHER))
        {
#pragma warning disable CS0618
            return parameters.Value.CreateForLegacyNetherBiome(
                new LegacyRandomSource(unchecked(_seed + 1L)));
#pragma warning restore CS0618
        }

        RegistryKey<NormalNoise> key = parameters.UnwrapKey() ??
            throw new InvalidOperationException(
                $"Normal noise holder {parameters} has no registered key.");

        return GetOrCreateNoise(key);
    }

    public IRandomSource CreateRandom(ResourceLocation seed) =>
        _useLegacyRandomSource && seed.Equals(BlendedNoise.NOISE_SEED)
            ? new LegacyRandomSource(_seed)
            : _random.FromHashOf(seed);

    public IRandomSource CreateEndIslandRandom() =>
        new LegacyRandomSource(_seed);

    /// <summary>
    /// Acquires a reusable density-buffer pool, matching RandomState.acquireDensityBufferPool in 26.3.
    /// </summary>
    public DensityBufferPool AcquireDensityBufferPool()
    {
        lock (_densityBufferPoolLock)
        {
            if (_densityBufferPools.Count == 0)
                return new DensityBufferPool(MaxDensityBufferAgeTicks);

            int lastIndex = _densityBufferPools.Count - 1;
            DensityBufferPool pool = _densityBufferPools[lastIndex];
            _densityBufferPools.RemoveAt(lastIndex);
            return pool;
        }
    }

    /// <summary>
    /// Returns a density-buffer pool to this random state, retaining at most sixteen pools.
    /// </summary>
    public void ReleaseDensityBufferPool(DensityBufferPool pool)
    {
        lock (_densityBufferPoolLock)
        {
            if (_densityBufferPools.Count < MaxDensityBufferPools)
                _densityBufferPools.Add(pool);
        }
    }

    /// <summary>
    /// Ages idle buffers and removes pools that no longer contain reusable buffers.
    /// </summary>
    public void GarbageCollect()
    {
        lock (_densityBufferPoolLock)
        {
            for (int index = _densityBufferPools.Count - 1; index >= 0; index--)
            {
                DensityBufferPool pool = _densityBufferPools[index];
                pool.GarbageCollect();
                if (pool.IsEmpty())
                    _densityBufferPools.RemoveAt(index);
            }
        }
    }

    private sealed class BoundDensitySamplerSet(
        RandomState owner,
        SamplerContext context) : DensitySamplerSet
    {
        public DensitySampler.Bound Get(DensityFunction function) =>
            owner.GetSampler(function).Bind(context);

        public float SampleValue(
            DensityFunction function,
            int blockX,
            int blockY,
            int blockZ) =>
            owner.GetSampler(function).SampleValue(
                context,
                blockX,
                blockY,
                blockZ);
    }
}
