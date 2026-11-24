using Asteria.Content.Identifiers;
using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.World.LevelGen.Synth;
using Asteria.World.LevelGen;

namespace Asteria.World.LevelGen.Materials;

/// <summary>
/// Mutable per-column/per-block input state consumed while compiled material rules are evaluated.
/// Service-backed biome, density, surface and noise accessors are added with MaterialSystem.
/// </summary>
public sealed class MaterialRuleContext
{
    private readonly WorldGenerationContext? _worldGenerationContext;
    private readonly RandomState? _randomState;
    private readonly Func<BlockPos, Holder<Biome>>? _biomeGetter;
    private readonly IReadOnlySet<Holder<Biome>>? _possibleBiomes;
    private readonly Dictionary<RegistryKey<NormalNoise>, Func<double>> _noiseSamplers2D = new();
    private readonly Dictionary<RegistryKey<NormalNoise>, Func<double>> _noiseSamplers3D = new();
    private Holder<Biome>? _biome;

    public MaterialRuleContext()
    {
    }

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

    public IReadOnlySet<Holder<Biome>>? PossibleBiomes => _possibleBiomes;

    public Holder<Biome> GetBiome()
    {
        if (_biome is not null)
            return _biome;

        Func<BlockPos, Holder<Biome>> getter = _biomeGetter ??
            throw new InvalidOperationException("Biome conditions require a biome getter.");
        return _biome = getter(new BlockPos(BlockX, BlockY, BlockZ));
    }

    public int GetSeaLevel() =>
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

    private long _lastUpdateXZ = long.MinValue + 1;
    private long _lastUpdateY = long.MinValue + 1;

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
    public double SurfaceSecondary { get; private set; }
    public int MinSurfaceLevel { get; private set; } = int.MaxValue;

    public void UpdateXZ(
        int blockX,
        int blockZ,
        int surfaceGradientX,
        int surfaceGradientZ,
        int surfaceDepth = 0,
        double surfaceSecondary = 0.0,
        int minSurfaceLevel = int.MaxValue)
    {
        _lastUpdateXZ = unchecked(_lastUpdateXZ + 1);
        _lastUpdateY = unchecked(_lastUpdateY + 1);
        BlockX = blockX;
        BlockZ = blockZ;
        SurfaceGradientX = surfaceGradientX;
        SurfaceGradientZ = surfaceGradientZ;
        SurfaceDepth = surfaceDepth;
        SurfaceSecondary = surfaceSecondary;
        MinSurfaceLevel = minSurfaceLevel;
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
