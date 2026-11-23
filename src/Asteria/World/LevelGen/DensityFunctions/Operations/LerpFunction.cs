using Asteria.Core;
using Asteria.Util;

namespace Asteria.World.LevelGen.DensityFunctions.Operations;

/// <summary>
/// Three-input interpolation density function from Minecraft Java 26.3.
/// </summary>
public sealed record LerpFunction(
    DensityFunction Alpha,
    DensityFunction First,
    DensityFunction Second) : DensityFunction
{
    public DensitySampler CompileSampler(DensityFunction.CompileContext context)
    {
        // Vanilla eagerly compiles all three children before selecting a constant branch.
        DensitySampler alpha = Alpha.CompileSampler(context);
        DensitySampler first = First.CompileSampler(context);
        DensitySampler second = Second.CompileSampler(context);

        if (First is ConstantFunction firstConstant)
            return new ConstFirstSampler(alpha, firstConstant.Value, second);

        if (Second is ConstantFunction secondConstant)
            return new ConstSecondSampler(alpha, first, secondConstant.Value);

        return new Sampler(alpha, first, second);
    }

    public DensityFunction RewriteChildren(DfRewriteRule rule)
    {
        DensityFunction alpha = rule(Alpha);
        DensityFunction first = rule(First);
        DensityFunction second = rule(Second);

        return ReferenceEquals(alpha, Alpha) &&
            ReferenceEquals(first, First) &&
            ReferenceEquals(second, Second)
            ? this
            : new LerpFunction(alpha, first, second);
    }

    public Interval Range() =>
        Interval.Lerp(Alpha.Range(), First.Range(), Second.Range());

    public int DomainAxes() =>
        Alpha.DomainAxes() | First.DomainAxes() | Second.DomainAxes();

    public sealed record ConstFirstSampler(
        DensitySampler Alpha,
        float First,
        DensitySampler Second) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Alpha.SampleVolume(context, outputBuffer, volume);
            using ScopedDensityBuffer secondBuffer = context.AcquireBuffer(volume);
            Second.SampleVolume(context, secondBuffer, volume);

            for (int index = 0; index < outputBuffer.Size(); index++)
            {
                float alpha = outputBuffer.Get(index);
                float second = secondBuffer.Get(index);
                if (alpha == 0.0f)
                {
                    outputBuffer.Set(index, First);
                    continue;
                }

                if (alpha == 1.0f)
                {
                    outputBuffer.Set(index, second);
                    continue;
                }

                outputBuffer.Set(index, Mth.Lerp(alpha, First, second));
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            float alpha = Alpha.SampleValue(context, blockX, blockY, blockZ);
            if (alpha == 0.0f)
                return First;
            if (alpha == 1.0f)
                return Second.SampleValue(context, blockX, blockY, blockZ);

            return Mth.Lerp(
                alpha,
                First,
                Second.SampleValue(context, blockX, blockY, blockZ));
        }
    }

    public sealed record ConstSecondSampler(
        DensitySampler Alpha,
        DensitySampler First,
        float Second) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Alpha.SampleVolume(context, outputBuffer, volume);
            using ScopedDensityBuffer firstBuffer = context.AcquireBuffer(volume);
            First.SampleVolume(context, firstBuffer, volume);

            for (int index = 0; index < outputBuffer.Size(); index++)
            {
                float alpha = outputBuffer.Get(index);
                float first = firstBuffer.Get(index);
                if (alpha == 0.0f)
                {
                    outputBuffer.Set(index, first);
                    continue;
                }

                if (alpha == 1.0f)
                {
                    outputBuffer.Set(index, Second);
                    continue;
                }

                outputBuffer.Set(index, Mth.Lerp(alpha, first, Second));
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            float alpha = Alpha.SampleValue(context, blockX, blockY, blockZ);
            if (alpha == 0.0f)
                return First.SampleValue(context, blockX, blockY, blockZ);
            if (alpha == 1.0f)
                return Second;

            return Mth.Lerp(
                alpha,
                First.SampleValue(context, blockX, blockY, blockZ),
                Second);
        }
    }

    public sealed record Sampler(
        DensitySampler Alpha,
        DensitySampler First,
        DensitySampler Second) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Alpha.SampleVolume(context, outputBuffer, volume);
            using ScopedDensityBuffer firstBuffer = context.AcquireBuffer(volume);
            First.SampleVolume(context, firstBuffer, volume);
            using ScopedDensityBuffer secondBuffer = context.AcquireBuffer(volume);
            Second.SampleVolume(context, secondBuffer, volume);

            for (int index = 0; index < outputBuffer.Size(); index++)
            {
                float alpha = outputBuffer.Get(index);
                float first = firstBuffer.Get(index);
                float second = secondBuffer.Get(index);
                if (alpha == 0.0f)
                {
                    outputBuffer.Set(index, first);
                    continue;
                }

                if (alpha == 1.0f)
                {
                    outputBuffer.Set(index, second);
                    continue;
                }

                outputBuffer.Set(index, Mth.Lerp(alpha, first, second));
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            float alpha = Alpha.SampleValue(context, blockX, blockY, blockZ);
            if (alpha == 0.0f)
                return First.SampleValue(context, blockX, blockY, blockZ);
            if (alpha == 1.0f)
                return Second.SampleValue(context, blockX, blockY, blockZ);

            return Mth.Lerp(
                alpha,
                First.SampleValue(context, blockX, blockY, blockZ),
                Second.SampleValue(context, blockX, blockY, blockZ));
        }
    }
}
