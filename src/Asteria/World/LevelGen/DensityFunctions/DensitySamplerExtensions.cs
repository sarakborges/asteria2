namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// C# extension surface for the vanilla DensitySampler.bind default interface method.
/// </summary>
public static class DensitySamplerExtensions
{
    public static DensitySampler.Bound Bind(this DensitySampler sampler, SamplerContext context)
    {
        ArgumentNullException.ThrowIfNull(sampler);
        ArgumentNullException.ThrowIfNull(context);
        return new DensitySampler.Bound(sampler, context);
    }
}
