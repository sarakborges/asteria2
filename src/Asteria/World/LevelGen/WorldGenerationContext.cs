using Asteria.World;

namespace Asteria.World.LevelGen;

/// <summary>
/// Vertical world-generation bounds and sea level used to resolve VerticalAnchor values.
/// </summary>
public sealed class WorldGenerationContext
{
    public int MinGenY { get; }
    public int GenDepth { get; }
    public int SeaLevel { get; }

    public WorldGenerationContext(int minGenY, int genDepth, int seaLevel)
    {
        MinGenY = minGenY;
        GenDepth = genDepth;
        SeaLevel = seaLevel;
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
        : this(
            Math.Max(
                (heightAccessor ?? throw new ArgumentNullException(nameof(heightAccessor))).GetMinY(),
                (generator ?? throw new ArgumentNullException(nameof(generator))).GetMinY()),
            Math.Min(heightAccessor.GetHeight(), generator.GetGenDepth()),
            generator.GetSeaLevel())
    {
    }

    public int GetMinGenY() => MinGenY;
    public int GetGenDepth() => GenDepth;
    public int SeaLevel() => SeaLevel;

    public int GetMaxGenY() => unchecked(MinGenY + GenDepth - 1);
}
