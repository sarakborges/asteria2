using Asteria.World;

namespace Asteria.World.LevelGen;

/// <summary>
/// Vertical world-generation bounds and sea level used to resolve VerticalAnchor values.
/// </summary>
public sealed class WorldGenerationContext
{
    public int MinGenY { get; }
    public int GenDepth { get; }
    public int SeaLevelValue { get; }

    public WorldGenerationContext(int minGenY, int genDepth, int seaLevel)
    {
        MinGenY = minGenY;
        GenDepth = genDepth;
        SeaLevelValue = seaLevel;
    }

    public WorldGenerationContext(LevelHeightAccessor heightAccessor, int seaLevel)
        : this(
            (heightAccessor ?? throw new ArgumentNullException(nameof(heightAccessor))).GetMinY(),
            heightAccessor.GetHeight(),
            seaLevel)
    {
    }

    public WorldGenerationContext(
        NoiseBasedChunkGenerator generator,
        LevelHeightAccessor heightAccessor)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(heightAccessor);

        int minGenY = Math.Max(heightAccessor.GetMinY(), generator.GetMinY());
        long worldMaxExclusive = (long)heightAccessor.GetMinY() + heightAccessor.GetHeight();
        long generatorMaxExclusive = (long)generator.GetMinY() + generator.GetGenDepth();
        long maxGenYExclusive = Math.Min(worldMaxExclusive, generatorMaxExclusive);
        long genDepth = Math.Max(0L, maxGenYExclusive - minGenY);

        if (genDepth > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(heightAccessor), "The intersected generation height exceeds the supported range.");

        MinGenY = minGenY;
        GenDepth = (int)genDepth;
        SeaLevelValue = generator.GetSeaLevel();
    }

    public int GetMinGenY() => MinGenY;
    public int GetGenDepth() => GenDepth;
    public int SeaLevel() => SeaLevelValue;

    public int ResolveAnchorY(VerticalAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        return anchor.ResolveY(this);
    }

    public int GetMaxGenY() => unchecked(MinGenY + GenDepth - 1);
}
