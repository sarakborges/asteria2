using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions;

namespace Asteria.World.LevelGen.DensityFunctions.Generator;

/// <summary>
/// Constant density-function node from Minecraft Java 26.3.
/// </summary>
public sealed record ConstantFunction(float Value) : DensityFunction
{
    public DensitySampler CompileSampler(DensityFunction.CompileContext context) =>
        new Sampler(Value);

    public Interval Range() =>
        Interval.OfExact(Value);

    public int DomainAxes() =>
        DensityFunction.NO_AXES;

    public DensityFunction RewriteChildren(DfRewriteRule rule) =>
        this;

    public sealed record Sampler(float Value) : DensitySampler
    {
        public void SampleVolume(
            SamplerContext context,
            DensityBuffer outputBuffer,
            DensityVolume volume) =>
            outputBuffer.Fill(Value);

        public float SampleValue(
            SamplerContext context,
            int blockX,
            int blockY,
            int blockZ) =>
            Value;
    }
}
