using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions.Generator;

namespace Asteria.World.LevelGen.DensityFunctions.Operations;

/// <summary>
/// Power density-function operation from Minecraft Java 26.3.
/// </summary>
public sealed record PowFunction(DensityFunction Base, DensityFunction Exponent) : DensityFunction
{
    public DensitySampler CompileSampler(DensityFunction.CompileContext context)
    {
        // Vanilla compiles base and exponent before choosing either constant optimization.
        DensitySampler baseSampler = Base.CompileSampler(context);
        DensitySampler exponentSampler = Exponent.CompileSampler(context);

        if (Base is ConstantFunction constantBase)
            return new ConstBaseSampler(constantBase.Value, exponentSampler);

        if (Exponent is ConstantFunction constantExponent)
            return CompileConstantExponent(baseSampler, constantExponent.Value);

        return new Sampler(baseSampler, exponentSampler);
    }

    private static DensitySampler CompileConstantExponent(DensitySampler baseSampler, float exponent)
    {
        float absoluteExponent = Math.Abs(exponent);
        DensitySampler? specialized = absoluteExponent switch
        {
            0.5f => new UnaryFunction.SqrtSampler(baseSampler),
            1.0f => baseSampler,
            2.0f => new UnaryFunction.SquareSampler(baseSampler),
            3.0f => new UnaryFunction.CubeSampler(baseSampler),
            _ => null
        };

        if (specialized is null)
            return new ConstExponentSampler(baseSampler, exponent);

        return exponent >= 0.0f
            ? specialized
            : new UnaryFunction.ReciprocalSampler(specialized);
    }

    public DensityFunction RewriteChildren(DfRewriteRule rule)
    {
        DensityFunction @base = rule(Base);
        DensityFunction exponent = rule(Exponent);
        return ReferenceEquals(@base, Base) && ReferenceEquals(exponent, Exponent)
            ? this
            : new PowFunction(@base, exponent);
    }

    public Interval Range() =>
        Interval.Pow(Base.Range(), Exponent.Range());

    public int DomainAxes() =>
        Base.DomainAxes() | Exponent.DomainAxes();

    public sealed record ConstBaseSampler(float Base, DensitySampler Exponent) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Exponent.SampleVolume(context, outputBuffer, volume);
            for (int index = 0; index < outputBuffer.Size(); index++)
                outputBuffer.Set(index, (float)Math.Pow(Base, outputBuffer.Get(index)));
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            (float)Math.Pow(Base, Exponent.SampleValue(context, blockX, blockY, blockZ));
    }

    public sealed record Sampler(DensitySampler Base, DensitySampler Exponent) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Base.SampleVolume(context, outputBuffer, volume);
            using ScopedDensityBuffer exponentBuffer = context.AcquireBuffer(volume);
            Exponent.SampleVolume(context, exponentBuffer, volume);
            for (int index = 0; index < outputBuffer.Size(); index++)
            {
                float baseValue = outputBuffer.Get(index);
                float exponentValue = exponentBuffer.Get(index);
                outputBuffer.Set(index, (float)Math.Pow(baseValue, exponentValue));
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            (float)Math.Pow(
                Base.SampleValue(context, blockX, blockY, blockZ),
                Exponent.SampleValue(context, blockX, blockY, blockZ));
    }

    public sealed record ConstExponentSampler(DensitySampler Base, double Exponent) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Base.SampleVolume(context, outputBuffer, volume);
            for (int index = 0; index < outputBuffer.Size(); index++)
                outputBuffer.Set(index, (float)Math.Pow(outputBuffer.Get(index), Exponent));
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            (float)Math.Pow(Base.SampleValue(context, blockX, blockY, blockZ), Exponent);
    }
}
