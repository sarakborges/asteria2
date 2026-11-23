namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Resolves a density function to a context-bound sampler, matching Minecraft Java 26.3.
/// </summary>
public interface DensitySamplerSet
{
    DensitySampler.Bound Get(DensityFunction function);

    float SampleValue(
        DensityFunction function,
        int blockX,
        int blockY,
        int blockZ) =>
        Get(function).SampleValue(blockX, blockY, blockZ);
}
