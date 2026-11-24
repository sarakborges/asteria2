using Asteria.Util.Context;

namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Density sampler selected from a typed SamplerContext field, matching Minecraft Java 26.3.
/// </summary>
public sealed record ContextBoundSampler<TSampler>(
    ContextKey<TSampler> Key,
    TSampler FallbackSampler) : DensitySampler
    where TSampler : DensitySampler
{
    public void SampleVolume(
        SamplerContext context,
        DensityBuffer outputBuffer,
        DensityVolume volume)
    {
        TSampler sampler = context.GetFieldOrDefault(Key, FallbackSampler);
        sampler.SampleVolume(context, outputBuffer, volume);
    }

    public float SampleValue(
        SamplerContext context,
        int blockX,
        int blockY,
        int blockZ)
    {
        TSampler sampler = context.GetFieldOrDefault(Key, FallbackSampler);
        return sampler.SampleValue(context, blockX, blockY, blockZ);
    }
}
