namespace Asteria.World.LevelGen.Materials;

/// <summary>
/// Mutable per-column/per-block input state consumed while compiled material rules are evaluated.
/// Service-backed biome, density, surface and noise accessors are added with MaterialSystem.
/// </summary>
public sealed class MaterialRuleContext
{
    private long _lastUpdateXZ = long.MinValue + 1;
    private long _lastUpdateY = long.MinValue + 1;

    public long LastUpdateXZ => _lastUpdateXZ;
    public long LastUpdateY => _lastUpdateY;

    public int BlockX { get; private set; }
    public int BlockY { get; private set; }
    public int BlockZ { get; private set; }
    public int SurfaceGradientX { get; private set; }
    public int SurfaceGradientZ { get; private set; }
    public int WaterHeight { get; private set; }
    public int StoneDepthBelow { get; private set; }
    public int StoneDepthAbove { get; private set; }
    public int SurfaceDepth { get; private set; }

    public void UpdateXZ(
        int blockX,
        int blockZ,
        int surfaceGradientX,
        int surfaceGradientZ,
        int surfaceDepth = 0)
    {
        _lastUpdateXZ = unchecked(_lastUpdateXZ + 1);
        _lastUpdateY = unchecked(_lastUpdateY + 1);
        BlockX = blockX;
        BlockZ = blockZ;
        SurfaceGradientX = surfaceGradientX;
        SurfaceGradientZ = surfaceGradientZ;
        SurfaceDepth = surfaceDepth;
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
        BlockY = blockY;
        WaterHeight = waterHeight;
        StoneDepthBelow = stoneDepthBelow;
        StoneDepthAbove = stoneDepthAbove;
    }
}
