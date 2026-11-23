using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions.Generator;

namespace Asteria.World.LevelGen.DensityFunctions.Operations;

/// <summary>
/// Conditional density-function branch selection from Minecraft Java 26.3.
/// </summary>
public sealed record RangeChoiceFunction(
    DensityFunction Input,
    float MinInclusive,
    float MaxExclusive,
    DensityFunction WhenInRange,
    DensityFunction WhenOutOfRange) : DensityFunction
{
    public DensitySampler CompileSampler(DensityFunction.CompileContext context)
    {
        DensitySampler input = Input.CompileSampler(context);

        if (WhenInRange is ConstantFunction whenInRange &&
            WhenOutOfRange is ConstantFunction whenOutOfRange)
        {
            return new ConstSampler(
                input,
                MinInclusive,
                MaxExclusive,
                whenInRange.Value,
                whenOutOfRange.Value);
        }

        return new Sampler(
            input,
            MinInclusive,
            MaxExclusive,
            WhenInRange.CompileSampler(context),
            WhenOutOfRange.CompileSampler(context));
    }

    public DensityFunction RewriteChildren(DfRewriteRule rule)
    {
        DensityFunction input = rule(Input);
        DensityFunction whenInRange = rule(WhenInRange);
        DensityFunction whenOutOfRange = rule(WhenOutOfRange);

        return ReferenceEquals(input, Input) &&
            ReferenceEquals(whenInRange, WhenInRange) &&
            ReferenceEquals(whenOutOfRange, WhenOutOfRange)
            ? this
            : new RangeChoiceFunction(
                input,
                MinInclusive,
                MaxExclusive,
                whenInRange,
                whenOutOfRange);
    }

    public Interval Range() =>
        Interval.Encapsulating(WhenInRange.Range(), WhenOutOfRange.Range());

    public int DomainAxes() =>
        Input.DomainAxes() | WhenInRange.DomainAxes() | WhenOutOfRange.DomainAxes();

    public sealed record ConstSampler(
        DensitySampler Input,
        float MinInclusive,
        float MaxExclusive,
        float WhenInRange,
        float WhenOutOfRange) : DensitySampler
    {
        private float Choose(float input) =>
            input >= MinInclusive && input < MaxExclusive
                ? WhenInRange
                : WhenOutOfRange;

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Input.SampleVolume(context, outputBuffer, volume);
            for (int index = 0; index < outputBuffer.Size(); index++)
                outputBuffer.Set(index, Choose(outputBuffer.Get(index)));
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Choose(Input.SampleValue(context, blockX, blockY, blockZ));
    }

    public sealed record Sampler(
        DensitySampler Input,
        float MinInclusive,
        float MaxExclusive,
        DensitySampler WhenInRange,
        DensitySampler WhenOutOfRange) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            WhenInRange.SampleVolume(context, outputBuffer, volume);
            using ScopedDensityBuffer inputBuffer = context.AcquireBuffer(volume);
            Input.SampleVolume(context, inputBuffer, volume);
            using ScopedDensityBuffer whenOutOfRangeBuffer = context.AcquireBuffer(volume);
            WhenOutOfRange.SampleVolume(context, whenOutOfRangeBuffer, volume);

            for (int index = 0; index < outputBuffer.Size(); index++)
            {
                float input = inputBuffer.Get(index);
                if (input >= MinInclusive && input < MaxExclusive)
                    continue;

                outputBuffer.Set(index, whenOutOfRangeBuffer.Get(index));
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            float inputValue = Input.SampleValue(context, blockX, blockY, blockZ);
            return inputValue >= MinInclusive && inputValue < MaxExclusive
                ? WhenInRange.SampleValue(context, blockX, blockY, blockZ)
                : WhenOutOfRange.SampleValue(context, blockX, blockY, blockZ);
        }
    }
}
