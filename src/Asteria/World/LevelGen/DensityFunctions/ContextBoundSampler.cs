using Asteria.Util.Context;

namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Density sampler selected from a SamplerContext field, matching Minecraft Java 26.3.
/// </summary>
public sealed record ContextBoundSampler(
    ContextKey<DensitySampler> Key,
    DensitySampler FallbackSampler) : DensitySampler
{
    public void SampleVolume(
        SamplerContext context,
        DensityBuffer outputBuffer,
        DensityVolume volume)
    {
        DensitySampler sampler = context.GetFieldOrDefault(Key, FallbackSampler);
        sampler.SampleVolume(context, outputBuffer, volume);
    }

    public float SampleValue(
        SamplerContext context,
        int blockX,
        int blockY,
        int blockZ)
    {
        DensitySampler sampler = context.GetFieldOrDefault(Key, FallbackSampler);
        return sampler.SampleValue(context, blockX, blockY, blockZ);
    }
}
