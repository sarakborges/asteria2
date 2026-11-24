using Asteria.Content.Blocks;
using Asteria.Content.Identifiers;
using Asteria.Core;
using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.World.LevelGen.Synth;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;

namespace Asteria.World.LevelGen.Materials;

/// <summary>
/// Mutable per-column/per-block input state consumed while compiled material rules are evaluated.
/// The service-backed constructor mirrors the Minecraft Java 26.3 material-rule context.
/// </summary>
public sealed class MaterialRuleContext
{
    private readonly WorldGenerationContext? _worldGenerationContext;
    private readonly RandomState? _randomState;
    private readonly Func<BlockPos, Holder<Biome>>? _biomeGetter;
    private readonly IReadOnlySet<Holder<Biome>>? _possibleBiomes;
    private readonly MaterialSystem? _materialSystem;
    private readonly DensityVolume? _expectedVolume;
    private readonly DensitySamplerSet? _densitySamplers;
    private readonly DensityVolume? _preliminarySurfaceVolume;
    private DensityBuffer? _preliminarySurfaceBuffer;
    private readonly Dictionary<RegistryKey<NormalNoise>, Func<double>> _noiseSamplers2D = new();
    private readonly Dictionary<RegistryKey<NormalNoise>, Func<double>> _noiseSamplers3D = new();
    private Holder<Biome>? _biome;
    private long _lastSurfaceSecondaryUpdate = long.MinValue;
    private double _surfaceSecondary;
    private long _lastMinSurfaceLevelUpdate = long.MinValue;
    private int _minSurfaceLevel = int.MaxValue;

    private long _lastUpdateXZ = long.MinValue + 1;
    private long _lastUpdateY = long.MinValue + 1;

    public MaterialRuleContext()
    {
    }

    /// <summary>Compatibility constructor for isolated condition evaluation and parity checks.</summary>
    public MaterialRuleContext(
        WorldGenerationContext worldGenerationContext,
        RandomState? randomState = null,
        Func<BlockPos, Holder<Biome>>? biomeGetter = null,
        IReadOnlySet<Holder<Biome>>? possibleBiomes = null)
    {
        _worldGenerationContext = worldGenerationContext ??
            throw new ArgumentNullException(nameof(worldGenerationContext));
        _randomState = randomState;
        _biomeGetter = biomeGetter;
        _possibleBiomes = possibleBiomes;
    }

    /// <summary>
    /// Creates the fully connected context used by the chunk material pass. The expected volume
    /// is narrowed to the highest occupied section and the preliminary-surface density is sampled
    /// only when a condition actually requests its value.
    /// </summary>
    public MaterialRuleContext(
        MaterialSystem materialSystem,
        RandomState randomState,
        DensityVolume expectedVolume,
        DensitySamplerSet densitySamplers,
        Func<BlockPos, Holder<Biome>> biomeGetter,
        WorldGenerationContext worldGenerationContext,
        IReadOnlySet<Holder<Biome>>? possibleBiomes = null)
    {
        _materialSystem = materialSystem ?? throw new ArgumentNullException(nameof(materialSystem));
        _randomState = randomState ?? throw new ArgumentNullException(nameof(randomState));
        _expectedVolume = expectedVolume ?? throw new ArgumentNullException(nameof(expectedVolume));
        _densitySamplers = densitySamplers ?? throw new ArgumentNullException(nameof(densitySamplers));
        _biomeGetter = biomeGetter ?? throw new ArgumentNullException(nameof(biomeGetter));
        _worldGenerationContext = worldGenerationContext ??
            throw new ArgumentNullException(nameof(worldGenerationContext));
        _possibleBiomes = possibleBiomes;
        _preliminarySurfaceVolume = new DensityVolume(
            expectedVolume.SizeX,
            1,
            expectedVolume.SizeZ,
            expectedVolume.MinBlockX,
            0,
            expectedVolume.MinBlockZ);
    }

    public IReadOnlySet<Holder<Biome>>? PossibleBiomes => _possibleBiomes;

    public long LastUpdateXZ => _lastUpdateXZ;
    public long LastUpdateY => _lastUpdateY;

    public int BlockX { get; private set; }
    public int BlockY { get; private set; }
    public int BlockZ { get; private set; }
    public int SurfaceGradientX { get; private set; }
    public int SurfaceGradientZ { get; private set; }
    public int WaterHeight { get; private set; } = int.MinValue;
    public int StoneDepthBelow { get; private set; }
    public int StoneDepthAbove { get; private set; }
    public int SurfaceDepth { get; private set; }

    /// <summary>Surface noise is sampled at most once for each updated XZ column.</summary>
    public double SurfaceSecondary
    {
        get
        {
            if (_lastSurfaceSecondaryUpdate != _lastUpdateXZ)
            {
                _surfaceSecondary = _materialSystem?.GetSurfaceSecondary(BlockX, BlockZ) ?? 0.0;
                _lastSurfaceSecondaryUpdate = _lastUpdateXZ;
            }

            return _surfaceSecondary;
        }
    }

    /// <summary>
    /// Preliminary surface is sampled from the chunk's 16x1x16 density volume on first use;
    /// positions outside that volume fall back to the bound sampler.
    /// </summary>
    public int MinSurfaceLevel
    {
        get
        {
            if (_lastMinSurfaceLevelUpdate != _lastUpdateXZ)
            {
                if (_materialSystem is null)
                {
                    _minSurfaceLevel = int.MaxValue;
                }
                else
                {
                    DensityVolume volume = _preliminarySurfaceVolume ??
                        throw new InvalidOperationException("Preliminary-surface volume was not initialized.");
                    DensitySamplerSet samplers = _densitySamplers ??
                        throw new InvalidOperationException("Preliminary-surface sampling requires density samplers.");
                    DensityFunction function = _materialSystem.PreliminarySurfaceFunction;

                    _preliminarySurfaceBuffer ??= SamplePreliminarySurface(samplers, function, volume);
                    int index = volume.IndexOfBlock(BlockX, 0, BlockZ);
                    float preliminarySurface = index == DensityVolume.NO_BLOCK
                        ? samplers.SampleValue(function, BlockX, 0, BlockZ)
                        : _preliminarySurfaceBuffer.Get(index);
                    _minSurfaceLevel = unchecked(Mth.Floor(preliminarySurface) + SurfaceDepth - 8);
                }

                _lastMinSurfaceLevelUpdate = _lastUpdateXZ;
            }

            return _minSurfaceLevel;
        }
    }

    private static DensityBuffer SamplePreliminarySurface(
        DensitySamplerSet samplers,
        DensityFunction function,
        DensityVolume volume)
    {
        DensityBuffer buffer = DensityBuffer.CreateUnpooled(volume.Size());
        samplers.Get(function).SampleVolume(buffer, volume);
        return buffer;
    }

    public Holder<Biome> GetBiome()
    {
        if (_biome is not null)
            return _biome;

        Func<BlockPos, Holder<Biome>> getter = _biomeGetter ??
            throw new InvalidOperationException("Biome conditions require a biome getter.");
        return _biome = getter(new BlockPos(BlockX, BlockY, BlockZ));
    }

    public int GetSeaLevel() =>
        _materialSystem?.GetSeaLevel() ??
        (_worldGenerationContext ??
            throw new InvalidOperationException("Sea level requires a WorldGenerationContext."))
            .SeaLevel();

    public Func<double> GetNoiseSampler(RegistryKey<NormalNoise> noiseId, bool is3D)
    {
        Dictionary<RegistryKey<NormalNoise>, Func<double>> samplers =
            is3D ? _noiseSamplers3D : _noiseSamplers2D;
        if (samplers.TryGetValue(noiseId, out Func<double>? cached))
            return cached;

        RandomState randomState = _randomState ??
            throw new InvalidOperationException("Noise conditions require a RandomState.");
        Noise noise = randomState.GetOrCreateNoise(noiseId);
        long lastUpdate = is3D ? unchecked(_lastUpdateY - 1L) : unchecked(_lastUpdateXZ - 1L);
        double lastValue = 0.0;

        Func<double> sampler = () =>
        {
            long currentUpdate = is3D ? _lastUpdateY : _lastUpdateXZ;
            if (lastUpdate != currentUpdate)
            {
                lastValue = is3D
                    ? noise.Get(BlockX, BlockY, BlockZ)
                    : noise.Get(BlockX, 0.0, BlockZ);
                lastUpdate = currentUpdate;
            }

            return lastValue;
        };

        samplers.Add(noiseId, sampler);
        return sampler;
    }

    public Func<float> GetDensitiesInChunk(DensityFunction function, bool prefill)
    {
        ArgumentNullException.ThrowIfNull(function);
        DensitySamplerSet samplers = _densitySamplers ??
            throw new InvalidOperationException("Chunk density sampling requires a material-rule context.");
        DensityVolume volume = _expectedVolume ??
            throw new InvalidOperationException("Chunk density sampling requires an expected volume.");
        DensitySampler.Bound sampler = samplers.Get(function);

        if (!prefill)
            return () => sampler.SampleValue(BlockX, BlockY, BlockZ);

        DensityBuffer buffer = DensityBuffer.CreateUnpooled(volume.Size());
        sampler.SampleVolume(buffer, volume);
        return () =>
        {
            int index = volume.IndexOfBlock(BlockX, BlockY, BlockZ);
            return index == DensityVolume.NO_BLOCK
                ? sampler.SampleValue(BlockX, BlockY, BlockZ)
                : buffer.Get(index);
        };
    }

    public BlockState GetBand(int worldX, int blockY, int worldZ) =>
        (_materialSystem ??
            throw new InvalidOperationException("Clay-band material rules require a connected MaterialSystem."))
            .GetBand(worldX, blockY, worldZ);

    public int ResolveAnchorY(VerticalAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        return (_worldGenerationContext ??
            throw new InvalidOperationException("Vertical anchors require a WorldGenerationContext."))
            .ResolveAnchorY(anchor);
    }

    public PositionalRandomFactory GetOrCreateRandomFactory(ResourceLocation name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return (_randomState ??
            throw new InvalidOperationException("Positional random factories require a RandomState."))
            .GetOrCreateRandomFactory(name);
    }

    public void UpdateXZ(
        int blockX,
        int blockZ,
        int surfaceGradientX,
        int surfaceGradientZ,
        int? surfaceDepth = null,
        double? surfaceSecondary = null,
        int? minSurfaceLevel = null)
    {
        _lastUpdateXZ = unchecked(_lastUpdateXZ + 1);
        _lastUpdateY = unchecked(_lastUpdateY + 1);
        BlockX = blockX;
        BlockZ = blockZ;
        SurfaceGradientX = surfaceGradientX;
        SurfaceGradientZ = surfaceGradientZ;
        SurfaceDepth = surfaceDepth ?? _materialSystem?.GetSurfaceDepth(blockX, blockZ) ?? 0;

        if (surfaceSecondary.HasValue)
        {
            _surfaceSecondary = surfaceSecondary.Value;
            _lastSurfaceSecondaryUpdate = _lastUpdateXZ;
        }
        else
        {
            _lastSurfaceSecondaryUpdate = long.MinValue;
        }

        if (minSurfaceLevel.HasValue)
        {
            _minSurfaceLevel = minSurfaceLevel.Value;
            _lastMinSurfaceLevelUpdate = _lastUpdateXZ;
        }
        else
        {
            _lastMinSurfaceLevelUpdate = long.MinValue;
        }
    }

    /// <summary>Creates an XZ-scoped evaluator invalidated only by column updates.</summary>
    public Conditions.ConditionEvaluator LazyXZCondition(Func<bool> compute)
    {
        ArgumentNullException.ThrowIfNull(compute);
        long lastUpdate = unchecked(_lastUpdateXZ - 1L);
        bool? result = null;
        return () =>
        {
            if (lastUpdate == _lastUpdateXZ)
                return result ?? throw new InvalidOperationException("Update triggered but the result is null");
            lastUpdate = _lastUpdateXZ;
            result = compute();
            return result.Value;
        };
    }

    /// <summary>Creates a Y-scoped evaluator invalidated only by block-height updates.</summary>
    public Conditions.ConditionEvaluator LazyYCondition(Func<bool> compute)
    {
        ArgumentNullException.ThrowIfNull(compute);
        long lastUpdate = unchecked(_lastUpdateY - 1L);
        bool? result = null;
        return () =>
        {
            if (lastUpdate == _lastUpdateY)
                return result ?? throw new InvalidOperationException("Update triggered but the result is null");
            lastUpdate = _lastUpdateY;
            result = compute();
            return result.Value;
        };
    }

    public void UpdateY(
        int stoneDepthAbove,
        int stoneDepthBelow,
        int waterHeight,
        int blockY)
    {
        _lastUpdateY = unchecked(_lastUpdateY + 1);
        _biome = null;
        BlockY = blockY;
        WaterHeight = waterHeight;
        StoneDepthBelow = stoneDepthBelow;
        StoneDepthAbove = stoneDepthAbove;
    }
}
