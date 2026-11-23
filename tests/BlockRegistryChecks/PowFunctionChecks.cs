using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Operations;

internal static class PowFunctionChecks
{
    private static readonly DensityFunction.CompileContext Context = new TestCompileContext();

    private sealed class TestCompileContext : DensityFunction.CompileContext
    {
        public Asteria.World.LevelGen.Synth.Noise CreateNoiseSampler(
            Asteria.Content.Registries.Holder<Asteria.World.LevelGen.Synth.NormalNoise> parameters) =>
            throw new InvalidOperationException();

        public Asteria.World.LevelGen.IRandomSource CreateRandom(Asteria.Content.Identifiers.ResourceLocation seed) =>
            throw new InvalidOperationException();

#pragma warning disable CS0618
        public Asteria.World.LevelGen.IRandomSource CreateEndIslandRandom() =>
            throw new InvalidOperationException();
#pragma warning restore CS0618
    }

    private sealed class CountingSampler(float value) : DensitySampler
    {
        public int PointCalls { get; private set; }
        public int VolumeCalls { get; private set; }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            PointCalls++;
            return value;
        }

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            VolumeCalls++;
            outputBuffer.Fill(value);
        }
    }

    private sealed record Fixture(
        Interval ValueRange,
        int Axes,
        CountingSampler Sampler) : DensityFunction
    {
        public DensitySampler CompileSampler(DensityFunction.CompileContext context) => Sampler;
        public DensityFunction RewriteChildren(DfRewriteRule rule) => this;
        public Interval Range() => ValueRange;
        public int DomainAxes() => Axes;
    }

    private sealed class CountingArena : DensityBufferArena
    {
        public int Acquired { get; private set; }
        public int Released { get; private set; }

        public ScopedDensityBuffer Acquire(int size)
        {
            Acquired++;
            return new ScopedDensityBuffer(this, size, size);
        }

        public void Release(ScopedDensityBuffer buffer) => Released++;
    }

    public static void Run()
    {
        CheckCompilationPrecedenceAndOptimizations();
        CheckScalarPowers();
        CheckVolumeSamplingAndArena();
        CheckRangeAxesAndRewrite();
    }

    private static void CheckCompilationPrecedenceAndOptimizations()
    {
        var dynamicBaseSampler = new CountingSampler(4.0f);
        var dynamicExponentSampler = new CountingSampler(2.0f);
        DensityFunction dynamicBase = new Fixture(
            Interval.OfExact(4.0f), DensityFunction.AXIS_X, dynamicBaseSampler);
        DensityFunction dynamicExponent = new Fixture(
            Interval.OfExact(2.0f), DensityFunction.AXIS_Y, dynamicExponentSampler);

        Require(Compile(DensityFunctions.Pow(DensityFunctions.Constant(2.0f), DensityFunctions.Constant(3.0f)))
                is PowFunction.ConstBaseSampler,
            "PowFunction must prioritize a constant base even when exponent is also constant.");
        Require(Compile(DensityFunctions.Pow(dynamicBase, dynamicExponent)) is PowFunction.Sampler,
            "PowFunction must use the generic sampler when neither child is constant.");

        Require(Compile(DensityFunctions.Pow(dynamicBase, DensityFunctions.Constant(0.5f)))
                is UnaryFunction.SqrtSampler &&
            Compile(DensityFunctions.Pow(dynamicBase, DensityFunctions.Constant(1.0f))) == dynamicBaseSampler &&
            Compile(DensityFunctions.Pow(dynamicBase, DensityFunctions.Constant(2.0f)))
                is UnaryFunction.SquareSampler &&
            Compile(DensityFunctions.Pow(dynamicBase, DensityFunctions.Constant(3.0f)))
                is UnaryFunction.CubeSampler,
            "PowFunction must reuse the vanilla sqrt, identity, square and cube samplers for special positive exponents.");

        Require(Compile(DensityFunctions.Pow(dynamicBase, DensityFunctions.Constant(-0.5f)))
                is UnaryFunction.ReciprocalSampler { Input: UnaryFunction.SqrtSampler } &&
            Compile(DensityFunctions.Pow(dynamicBase, DensityFunctions.Constant(-1.0f)))
                is UnaryFunction.ReciprocalSampler { Input: var input } &&
            ReferenceEquals(input, dynamicBaseSampler) &&
            Compile(DensityFunctions.Pow(dynamicBase, DensityFunctions.Constant(-2.0f)))
                is UnaryFunction.ReciprocalSampler { Input: UnaryFunction.SquareSampler } &&
            Compile(DensityFunctions.Pow(dynamicBase, DensityFunctions.Constant(-3.0f)))
                is UnaryFunction.ReciprocalSampler { Input: UnaryFunction.CubeSampler },
            "PowFunction must wrap each negative special exponent sampler in UnaryFunction.ReciprocalSampler.");

        Require(Compile(DensityFunctions.Pow(dynamicBase, DensityFunctions.Constant(1.5f)))
                is PowFunction.ConstExponentSampler,
            "PowFunction must use ConstExponentSampler for non-special constant exponents.");
    }

    private static void CheckScalarPowers()
    {
        Require(Sample(DensityFunctions.Pow(DensityFunctions.Constant(2.0f), DensityFunctions.Constant(3.0f))) == 8.0f &&
            Sample(DensityFunctions.Pow(DensityFunctions.Constant(2.0f), DensityFunctions.Constant(-3.0f))) == 0.125f &&
            Sample(DensityFunctions.Pow(DensityFunctions.Constant(9.0f), DensityFunctions.Constant(0.5f))) == 3.0f &&
            Sample(DensityFunctions.Pow(DensityFunctions.Constant(4.0f), DensityFunctions.Constant(-0.5f))) == 0.5f,
            "PowFunction must evaluate constant-base and specialized exponent paths with exact powers.");

        Require(Math.Abs(Sample(DensityFunctions.Pow(
                    new Fixture(Interval.OfExact(4.0f), 0, new CountingSampler(4.0f)),
                    DensityFunctions.Constant(1.5f))) - 8.0f) < 0.000001f,
            "PowFunction.ConstExponentSampler must evaluate general constant exponents.");

        Require(Math.Abs(Sample(DensityFunctions.Pow(
                    new Fixture(Interval.OfExact(4.0f), 0, new CountingSampler(4.0f)),
                    new Fixture(Interval.OfExact(1.5f), 0, new CountingSampler(1.5f)))) - 8.0f) < 0.000001f,
            "PowFunction.Sampler must evaluate general dynamic base and exponent values.");
    }

    private static void CheckVolumeSamplingAndArena()
    {
        var arena = new CountingArena();
        SamplerContext context = SamplerContext.CreateBuilder().UseBufferArena(arena).Build();
        var volume = new DensityVolume(2, 2, 2, -1, 10, 20);

        DensityFunction[] functions =
        {
            DensityFunctions.Pow(DensityFunctions.Constant(2.0f),
                new Fixture(Interval.OfExact(3.0f), 0, new CountingSampler(3.0f))),
            DensityFunctions.Pow(
                new Fixture(Interval.OfExact(2.0f), 0, new CountingSampler(2.0f)),
                DensityFunctions.Constant(3.0f)),
            DensityFunctions.Pow(
                new Fixture(Interval.OfExact(2.0f), 0, new CountingSampler(2.0f)),
                new Fixture(Interval.OfExact(3.0f), 0, new CountingSampler(3.0f)))
        };

        foreach (DensityFunction function in functions)
        {
            DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
            function.CompileSampler(Context).SampleVolume(context, output, volume);
            Require(Enumerable.Range(0, output.Size()).All(index => output.Get(index) == 8.0f),
                "PowFunction sampler variants must evaluate all buffer entries with the expected power.");
        }

        Require(arena.Acquired == arena.Released,
            "PowFunction general sampler must release its scoped exponent buffer after volume sampling.");
    }

    private static void CheckRangeAxesAndRewrite()
    {
        DensityFunction @base = new Fixture(
            Interval.Of(2.0f, 3.0f), DensityFunction.AXIS_X, new CountingSampler(2.0f));
        DensityFunction exponent = new Fixture(
            Interval.OfExact(2.0f), DensityFunction.AXIS_Z, new CountingSampler(2.0f));
        DensityFunction function = DensityFunctions.Pow(@base, exponent);

        Require(function.Range().Equals(Interval.Pow(@base.Range(), exponent.Range())) &&
            function.DomainAxes() == (DensityFunction.AXIS_X | DensityFunction.AXIS_Z),
            "PowFunction.range and domainAxes must delegate interval powers and union child axes.");

        Require(ReferenceEquals(function.RewriteChildren(child => child), function),
            "PowFunction.rewriteChildren must preserve node identity when children do not change.");

        DensityFunction rewritten = function.RewriteChildren(_ => DensityFunctions.Constant(5.0f));
        Require(rewritten is PowFunction pow &&
            pow.Base.Equals(DensityFunctions.Constant(5.0f)) &&
            pow.Exponent.Equals(DensityFunctions.Constant(5.0f)),
            "PowFunction.rewriteChildren must rewrite base then exponent.");
    }

    private static float Sample(DensityFunction function) =>
        function.CompileSampler(Context)
            .SampleValue(SamplerContext.EmptyUncached, 0, 0, 0);

    private static DensitySampler Compile(DensityFunction function) =>
        function.CompileSampler(Context);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
