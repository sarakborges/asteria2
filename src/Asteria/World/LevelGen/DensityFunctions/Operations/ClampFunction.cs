using Asteria.Core;
using Asteria.Util;

namespace Asteria.World.LevelGen.DensityFunctions.Operations;

/// <summary>
/// Density value clamp node from Minecraft Java 26.3.
/// </summary>
public sealed record ClampFunction(DensityFunction Input, float Min, float Max) : DensityFunction
{
    public DensitySampler CompileSampler(DensityFunction.CompileContext context) =>
        new Sampler(Input.CompileSampler(context), Min, Max);

    public DensityFunction RewriteChildren(DfRewriteRule rule)
    {
        DensityFunction input = rule(Input);
        return ReferenceEquals(input, Input)
            ? this
            : new ClampFunction(input, Min, Max);
    }

    public Interval Range() =>
        Interval.Clamp(Input.Range(), Min, Max);

    public int DomainAxes() =>
        Input.DomainAxes();

    public sealed record Sampler(DensitySampler Input, float Min, float Max) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Input.SampleVolume(context, outputBuffer, volume);
            for (int index = 0; index < outputBuffer.Size(); index++)
                outputBuffer.Set(index, Mth.Clamp(outputBuffer.Get(index), Min, Max));
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Mth.Clamp(Input.SampleValue(context, blockX, blockY, blockZ), Min, Max);
    }
}
