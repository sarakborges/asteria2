using Asteria.Util;

namespace Asteria.World.LevelGen.DensityFunctions.Operations;

/// <summary>
/// Ordered threshold selection across a list of density functions from Minecraft Java 26.3.
/// </summary>
public sealed record IntervalSelectFunction(
    DensityFunction Input,
    IReadOnlyList<float> Thresholds,
    IReadOnlyList<DensityFunction> Functions) : DensityFunction
{
    public DensitySampler CompileSampler(DensityFunction.CompileContext context)
    {
        DensitySampler input = Input.CompileSampler(context);
        if (Thresholds.Count == 1)
        {
            DensitySampler ifBelow = Functions[0].CompileSampler(context);
            DensitySampler ifAbove = Functions[^1].CompileSampler(context);
            return new SingleThresholdSampler(input, Thresholds[0], ifBelow, ifAbove);
        }

        DensitySampler[] samplers = Functions
            .Select(function => function.CompileSampler(context))
            .ToArray();

        return new Sampler(input, Thresholds.ToArray(), samplers);
    }

    public DensityFunction RewriteChildren(DfRewriteRule rule)
    {
        DensityFunction input = rule(Input);
        DensityFunction[] rewrittenFunctions = Functions
            .Select(function => rule(function))
            .ToArray();

        bool functionsChanged = rewrittenFunctions
            .Where((function, index) => !ReferenceEquals(function, Functions[index]))
            .Any();

        return ReferenceEquals(input, Input) && !functionsChanged
            ? this
            : new IntervalSelectFunction(Input: input, Thresholds, rewrittenFunctions);
    }

    public Interval Range() =>
        Interval.Encapsulating(Functions.Select(function => function.Range()).ToArray());

    public int DomainAxes()
    {
        int axes = Input.DomainAxes();
        foreach (DensityFunction function in Functions)
            axes |= function.DomainAxes();

        return axes;
    }

    private sealed record SingleThresholdSampler(
        DensitySampler Input,
        float Threshold,
        DensitySampler IfBelow,
        DensitySampler IfAbove) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Input.SampleVolume(context, outputBuffer, volume);
            using ScopedDensityBuffer ifBelowBuffer = context.AcquireBuffer(volume);
            IfBelow.SampleVolume(context, ifBelowBuffer, volume);
            using ScopedDensityBuffer ifAboveBuffer = context.AcquireBuffer(volume);
            IfAbove.SampleVolume(context, ifAboveBuffer, volume);

            for (int index = 0; index < outputBuffer.Size(); index++)
            {
                float input = outputBuffer.Get(index);
                outputBuffer.Set(
                    index,
                    input < Threshold
                        ? ifBelowBuffer.Get(index)
                        : ifAboveBuffer.Get(index));
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            float input = Input.SampleValue(context, blockX, blockY, blockZ);
            return input < Threshold
                ? IfBelow.SampleValue(context, blockX, blockY, blockZ)
                : IfAbove.SampleValue(context, blockX, blockY, blockZ);
        }
    }

    private sealed record Sampler(
        DensitySampler Input,
        float[] Thresholds,
        DensitySampler[] Samplers) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Input.SampleVolume(context, outputBuffer, volume);
            var buffers = new ScopedDensityBuffer?[Samplers.Length];

            try
            {
                for (int index = 0; index < Samplers.Length; index++)
                {
                    ScopedDensityBuffer buffer = context.AcquireBuffer(volume);
                    buffers[index] = buffer;
                    Samplers[index].SampleVolume(context, buffer, volume);
                }

                for (int index = 0; index < outputBuffer.Size(); index++)
                {
                    int selectedSampler = SelectSamplerIndex(outputBuffer.Get(index));
                    outputBuffer.Set(index, buffers[selectedSampler]!.Get(index));
                }
            }
            finally
            {
                foreach (ScopedDensityBuffer? buffer in buffers)
                    buffer?.Close();
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            float input = Input.SampleValue(context, blockX, blockY, blockZ);
            return Samplers[SelectSamplerIndex(input)]
                .SampleValue(context, blockX, blockY, blockZ);
        }

        private int SelectSamplerIndex(float input)
        {
            for (int index = 0; index < Thresholds.Length; index++)
            {
                if (input < Thresholds[index])
                    return index;
            }

            return Samplers.Length - 1;
        }
    }
}
