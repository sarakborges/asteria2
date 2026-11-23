namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Cache-cell-backed density sampler from Minecraft Java 26.3.
/// </summary>
public sealed record CachingDensitySampler(int Id, DensitySampler Input) : DensitySampler
{
    public void SampleVolume(
        SamplerContext context,
        DensityBuffer outputBuffer,
        DensityVolume volume) =>
        context.SampleVolumeCached(Id, Input, outputBuffer, volume);

    public float SampleValue(
        SamplerContext context,
        int blockX,
        int blockY,
        int blockZ) =>
        context.SampleValueCached(Id, Input, blockX, blockY, blockZ);
}
