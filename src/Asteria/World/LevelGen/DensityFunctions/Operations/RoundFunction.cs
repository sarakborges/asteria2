using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions.Generator;

namespace Asteria.World.LevelGen.DensityFunctions.Operations;

/// <summary>
/// Multiple-aware floor/round/ceiling/truncate operation from Minecraft Java 26.3.
/// </summary>
public sealed record RoundFunction(
    RoundFunction.Type Operation,
    DensityFunction Input,
    DensityFunction Multiple) : DensityFunction
{
    public enum Type
    {
        FLOOR,
        ROUND,
        CEIL,
        TRUNCATE
    }

    public static string GetId(Type type) =>
        type switch
        {
            Type.FLOOR => "floor",
            Type.ROUND => "round",
            Type.CEIL => "ceil",
            Type.TRUNCATE => "truncate",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

    private static float RoundToInteger(float input, Type type) =>
        type switch
        {
            Type.FLOOR => (float)Math.Floor(input),
            Type.ROUND => JavaRound(input),
            Type.CEIL => (float)Math.Ceiling(input),
            Type.TRUNCATE => input > 0.0f
                ? (float)Math.Floor(input)
                : (float)Math.Ceiling(input),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

    private static float JavaRound(float input)
    {
        // Java Math.round(float): floor(a + 0.5f), converted to a saturated int.
        if (float.IsNaN(input))
            return 0.0f;

        double rounded = Math.Floor((double)(input + 0.5f));
        if (rounded >= int.MaxValue)
            return int.MaxValue;
        if (rounded <= int.MinValue)
            return int.MinValue;

        return (float)(int)rounded;
    }

    public DensitySampler CompileSampler(DensityFunction.CompileContext context)
    {
        DensitySampler input = Input.CompileSampler(context);
        if (Multiple is not ConstantFunction constantMultiple)
            return new Sampler(Operation, input, Multiple.CompileSampler(context));

        if (constantMultiple.Value != 1.0f)
            return new Sampler(Operation, input, Multiple.CompileSampler(context));

        return new IntegerMultipleSampler(Operation, input);
    }

    public DensityFunction RewriteChildren(DfRewriteRule rule)
    {
        DensityFunction input = rule(Input);
        DensityFunction multiple = rule(Multiple);
        return ReferenceEquals(input, Input) && ReferenceEquals(multiple, Multiple)
            ? this
            : new RoundFunction(Operation, input, multiple);
    }

    public Interval Range()
    {
        Interval multipleRange = Multiple.Range();
        return Interval.Mul(
            Interval.MapMonotonic(
                Interval.Div(Input.Range(), multipleRange),
                value => RoundToInteger(value, Operation)),
            multipleRange);
    }

    public int DomainAxes() =>
        Input.DomainAxes() | Multiple.DomainAxes();

    private sealed record IntegerMultipleSampler(
        Type Operation,
        DensitySampler Input) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Input.SampleVolume(context, outputBuffer, volume);
            for (int index = 0; index < outputBuffer.Size(); index++)
                outputBuffer.Set(index, RoundToInteger(outputBuffer.Get(index), Operation));
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            RoundToInteger(Input.SampleValue(context, blockX, blockY, blockZ), Operation);
    }

    private sealed record Sampler(
        Type Operation,
        DensitySampler Input,
        DensitySampler Multiple) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Input.SampleVolume(context, outputBuffer, volume);
            using ScopedDensityBuffer multipleBuffer = context.AcquireBuffer(volume);
            Multiple.SampleVolume(context, multipleBuffer, volume);
            for (int index = 0; index < outputBuffer.Size(); index++)
            {
                float input = outputBuffer.Get(index);
                float multiple = multipleBuffer.Get(index);
                outputBuffer.Set(index, Apply(input, multiple));
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            float input = Input.SampleValue(context, blockX, blockY, blockZ);
            float multiple = Multiple.SampleValue(context, blockX, blockY, blockZ);
            return Apply(input, multiple);
        }

        private float Apply(float input, float multiple)
        {
            if (multiple == 0.0f)
                return input;

            return RoundToInteger(input / multiple, Operation) * multiple;
        }
    }
}
