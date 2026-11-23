using Asteria.Util;

namespace Asteria.World.LevelGen.DensityFunctions.Operations;

/// <summary>
/// Unary density-function operations from Minecraft Java 26.3.
/// </summary>
public sealed record UnaryFunction(UnaryFunction.Type Operation, DensityFunction Input) : DensityFunction
{
    public enum Type
    {
        ABS,
        SQUARE,
        CUBE,
        SQRT,
        HALF_NEGATIVE,
        QUARTER_NEGATIVE,
        RECIPROCAL,
        NEGATE,
        SQUEEZE,
        LOG,
        SIGN
    }

    public static string GetId(Type type) =>
        type switch
        {
            Type.ABS => "abs",
            Type.SQUARE => "square",
            Type.CUBE => "cube",
            Type.SQRT => "sqrt",
            Type.HALF_NEGATIVE => "half_negative",
            Type.QUARTER_NEGATIVE => "quarter_negative",
            Type.RECIPROCAL => "reciprocal",
            Type.NEGATE => "negate",
            Type.SQUEEZE => "squeeze",
            Type.LOG => "log",
            Type.SIGN => "sign",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

    public DensitySampler CompileSampler(DensityFunction.CompileContext context)
    {
        DensitySampler input = Input.CompileSampler(context);
        return Operation switch
        {
            Type.ABS => new AbsSampler(input),
            Type.SQUARE => new SquareSampler(input),
            Type.CUBE => new CubeSampler(input),
            Type.SQRT => new SqrtSampler(input),
            Type.HALF_NEGATIVE => new LeakyReLUSampler(input, 0.5f),
            Type.QUARTER_NEGATIVE => new LeakyReLUSampler(input, 0.25f),
            Type.RECIPROCAL => new ReciprocalSampler(input),
            Type.NEGATE => new NegateSampler(input),
            Type.SQUEEZE => new SqueezeSampler(input),
            Type.LOG => new LogSampler(input),
            Type.SIGN => new SignSampler(input),
            _ => throw new ArgumentOutOfRangeException(nameof(Operation))
        };
    }

    public DensityFunction RewriteChildren(DfRewriteRule rule)
    {
        DensityFunction rewrittenInput = rule(Input);
        return ReferenceEquals(rewrittenInput, Input)
            ? this
            : new UnaryFunction(Operation, rewrittenInput);
    }

    public Interval Range()
    {
        Interval inputRange = Input.Range();
        return Operation switch
        {
            Type.ABS => Interval.Abs(inputRange),
            Type.SQUARE => Interval.Square(inputRange),
            Type.CUBE => Interval.MapMonotonic(inputRange, static value => value * value * value),
            Type.SQRT => Interval.MapMonotonic(inputRange, static value => (float)Math.Sqrt(value)),
            Type.HALF_NEGATIVE => Interval.MapMonotonic(inputRange, static value => LeakyRelU(value, 0.5f)),
            Type.QUARTER_NEGATIVE => Interval.MapMonotonic(inputRange, static value => LeakyRelU(value, 0.25f)),
            Type.RECIPROCAL => Interval.Reciprocal(inputRange),
            Type.NEGATE => Interval.Sub(Interval.OfExact(0.0f), inputRange),
            Type.SQUEEZE => Interval.MapMonotonic(inputRange, Squeeze),
            Type.LOG => Interval.Log(inputRange),
            Type.SIGN => Interval.Sign(inputRange),
            _ => throw new ArgumentOutOfRangeException(nameof(Operation))
        };
    }

    public int DomainAxes() => Input.DomainAxes();

    private static float LeakyRelU(float input, float negativeFactor) =>
        input < 0.0f ? input * negativeFactor : input;

    private static float Squeeze(float input)
    {
        float clamped = Math.Clamp(input, -1.0f, 1.0f);
        return clamped / 2.0f - clamped * clamped * clamped / 24.0f;
    }

    private static void MapBuffer(
        DensitySampler input,
        SamplerContext context,
        DensityBuffer outputBuffer,
        DensityVolume volume,
        Func<float, float> operation)
    {
        input.SampleVolume(context, outputBuffer, volume);
        for (int index = 0; index < outputBuffer.Size(); index++)
            outputBuffer.Set(index, operation(outputBuffer.Get(index)));
    }

    public sealed record AbsSampler(DensitySampler Input) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume) =>
            MapBuffer(Input, context, outputBuffer, volume, MathF.Abs);

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            MathF.Abs(Input.SampleValue(context, blockX, blockY, blockZ));
    }

    public sealed record SquareSampler(DensitySampler Input) : DensitySampler
    {
        private static float Apply(float value) => value * value;

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume) =>
            MapBuffer(Input, context, outputBuffer, volume, Apply);

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Apply(Input.SampleValue(context, blockX, blockY, blockZ));
    }

    public sealed record CubeSampler(DensitySampler Input) : DensitySampler
    {
        private static float Apply(float value) => value * value * value;

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume) =>
            MapBuffer(Input, context, outputBuffer, volume, Apply);

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Apply(Input.SampleValue(context, blockX, blockY, blockZ));
    }

    public sealed record SqrtSampler(DensitySampler Input) : DensitySampler
    {
        private static float Apply(float value) => (float)Math.Sqrt(value);

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume) =>
            MapBuffer(Input, context, outputBuffer, volume, Apply);

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Apply(Input.SampleValue(context, blockX, blockY, blockZ));
    }

    public sealed record LeakyReLUSampler(DensitySampler Input, float NegativeFactor) : DensitySampler
    {
        private static float Apply(float negativeFactor, float value) =>
            LeakyRelU(value, negativeFactor);

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume) =>
            MapBuffer(Input, context, outputBuffer, volume, value => Apply(NegativeFactor, value));

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Apply(NegativeFactor, Input.SampleValue(context, blockX, blockY, blockZ));
    }

    public sealed record ReciprocalSampler(DensitySampler Input) : DensitySampler
    {
        private static float Apply(float value) => 1.0f / value;

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume) =>
            MapBuffer(Input, context, outputBuffer, volume, Apply);

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Apply(Input.SampleValue(context, blockX, blockY, blockZ));
    }

    public sealed record NegateSampler(DensitySampler Input) : DensitySampler
    {
        private static float Apply(float value) => -value;

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume) =>
            MapBuffer(Input, context, outputBuffer, volume, Apply);

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Apply(Input.SampleValue(context, blockX, blockY, blockZ));
    }

    public sealed record SqueezeSampler(DensitySampler Input) : DensitySampler
    {
        private static float Apply(float value) => Squeeze(value);

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume) =>
            MapBuffer(Input, context, outputBuffer, volume, Apply);

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Apply(Input.SampleValue(context, blockX, blockY, blockZ));
    }

    public sealed record LogSampler(DensitySampler Input) : DensitySampler
    {
        private static float Apply(float value) => (float)Math.Log(value);

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume) =>
            MapBuffer(Input, context, outputBuffer, volume, Apply);

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Apply(Input.SampleValue(context, blockX, blockY, blockZ));
    }

    public sealed record SignSampler(DensitySampler Input) : DensitySampler
    {
        private static float Apply(float value) =>
            value > 0.0f ? 1.0f : value < 0.0f ? -1.0f : value;

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume) =>
            MapBuffer(Input, context, outputBuffer, volume, Apply);

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Apply(Input.SampleValue(context, blockX, blockY, blockZ));
    }
}
