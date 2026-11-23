using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Operations;

internal static class LerpFunctionChecks
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
        CheckCompileVariants();
        CheckScalarBranches();
        CheckVolumeSampling();
        CheckRangeAndDomainAxes();
        CheckRewrite();
    }

    private static void CheckCompileVariants()
    {
        DensityFunction alpha = new Fixture(
            Interval.Of(0.0f, 1.0f), DensityFunction.AXIS_Y, new CountingSampler(0.25f));
        DensityFunction dynamicFirst = new Fixture(
            Interval.Of(-10.0f, 10.0f), DensityFunction.AXIS_X, new CountingSampler(4.0f));
        DensityFunction dynamicSecond = new Fixture(
            Interval.Of(-10.0f, 10.0f), DensityFunction.AXIS_Z, new CountingSampler(8.0f));

        Require(Compile(DensityFunctions.Lerp(alpha, DensityFunctions.Constant(2.0f), dynamicSecond))
                is LerpFunction.ConstFirstSampler &&
            Compile(DensityFunctions.Lerp(alpha, dynamicFirst, DensityFunctions.Constant(8.0f)))
                is LerpFunction.ConstSecondSampler &&
            Compile(DensityFunctions.Lerp(alpha, dynamicFirst, dynamicSecond))
                is LerpFunction.Sampler,
            "LerpFunction.compileSampler must select the constant-first, constant-second and generic sampler variants in vanilla order.");

        Require(DensityFunctions.Lerp(alpha, 2.0f, dynamicSecond)
                is LerpFunction,
            "DensityFunctions.lerp must expose the overload that accepts a constant first value.");
    }

    private static void CheckScalarBranches()
    {
        var alphaZero = new Fixture(
            Interval.OfExact(0.0f), DensityFunction.AXIS_Y, new CountingSampler(0.0f));
        var alphaOne = new Fixture(
            Interval.OfExact(1.0f), DensityFunction.AXIS_Y, new CountingSampler(1.0f));
        var alphaHalf = new Fixture(
            Interval.OfExact(0.5f), DensityFunction.AXIS_Y, new CountingSampler(0.5f));
        var first = new Fixture(
            Interval.OfExact(4.0f), DensityFunction.AXIS_X, new CountingSampler(4.0f));
        var second = new Fixture(
            Interval.OfExact(8.0f), DensityFunction.AXIS_Z, new CountingSampler(8.0f));

        var constFirstZero = (LerpFunction.ConstFirstSampler)Compile(
            DensityFunctions.Lerp(alphaZero, DensityFunctions.Constant(4.0f), second));
        Require(constFirstZero.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == 4.0f &&
            second.Sampler.PointCalls == 0,
            "LerpFunction.ConstFirstSampler must not sample the second child when alpha is zero.");

        var constFirstOne = (LerpFunction.ConstFirstSampler)Compile(
            DensityFunctions.Lerp(alphaOne, DensityFunctions.Constant(4.0f), second));
        Require(constFirstOne.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == 8.0f,
            "LerpFunction.ConstFirstSampler must return the second child when alpha is one.");

        var constFirstHalf = (LerpFunction.ConstFirstSampler)Compile(
            DensityFunctions.Lerp(alphaHalf, DensityFunctions.Constant(4.0f), second));
        Require(constFirstHalf.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == 6.0f,
            "LerpFunction.ConstFirstSampler must interpolate for alpha values other than zero and one.");

        var constSecondZero = (LerpFunction.ConstSecondSampler)Compile(
            DensityFunctions.Lerp(alphaZero, first, DensityFunctions.Constant(8.0f)));
        Require(constSecondZero.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == 4.0f,
            "LerpFunction.ConstSecondSampler must return the first child when alpha is zero.");

        var constSecondOne = (LerpFunction.ConstSecondSampler)Compile(
            DensityFunctions.Lerp(alphaOne, first, DensityFunctions.Constant(8.0f)));
        Require(constSecondOne.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == 8.0f &&
            first.Sampler.PointCalls == 1,
            "LerpFunction.ConstSecondSampler must skip the first child when alpha is one.");

        var genericZero = (LerpFunction.Sampler)Compile(DensityFunctions.Lerp(alphaZero, first, second));
        int firstBefore = first.Sampler.PointCalls;
        int secondBefore = second.Sampler.PointCalls;
        Require(genericZero.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == 4.0f &&
            first.Sampler.PointCalls == firstBefore + 1 &&
            second.Sampler.PointCalls == secondBefore,
            "LerpFunction.Sampler must not sample the second child when alpha is zero.");

        var genericOne = (LerpFunction.Sampler)Compile(DensityFunctions.Lerp(alphaOne, first, second));
        secondBefore = second.Sampler.PointCalls;
        Require(genericOne.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == 8.0f &&
            second.Sampler.PointCalls == secondBefore + 1,
            "LerpFunction.Sampler must select the second child when alpha is one.");

        var genericHalf = (LerpFunction.Sampler)Compile(DensityFunctions.Lerp(alphaHalf, first, second));
        Require(genericHalf.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == 6.0f,
            "LerpFunction.Sampler must interpolate the two child samples for intermediate alpha values.");

        Require(DensityFunctions.Lerp(DensityFunctions.Constant(-0.5f),
                    DensityFunctions.Constant(2.0f), DensityFunctions.Constant(8.0f))
                .CompileSampler(Context).SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == -3.0f &&
            DensityFunctions.Lerp(DensityFunctions.Constant(1.5f),
                    DensityFunctions.Constant(2.0f), DensityFunctions.Constant(8.0f))
                .CompileSampler(Context).SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == 11.0f,
            "LerpFunction must preserve extrapolation for alpha values outside the unit interval.");
    }

    private static void CheckVolumeSampling()
    {
        var alpha = new Fixture(
            Interval.OfExact(0.25f), DensityFunction.AXIS_Y, new CountingSampler(0.25f));
        var dynamicFirst = new Fixture(
            Interval.OfExact(4.0f), DensityFunction.AXIS_X, new CountingSampler(4.0f));
        var dynamicSecond = new Fixture(
            Interval.OfExact(8.0f), DensityFunction.AXIS_Z, new CountingSampler(8.0f));
        var volume = new DensityVolume(2, 2, 2, -1, 10, 20);
        var arena = new CountingArena();
        SamplerContext context = SamplerContext.CreateBuilder().UseBufferArena(arena).Build();

        DensityFunction[] functions =
        {
            DensityFunctions.Lerp(alpha, DensityFunctions.Constant(4.0f), dynamicSecond),
            DensityFunctions.Lerp(alpha, dynamicFirst, DensityFunctions.Constant(8.0f)),
            DensityFunctions.Lerp(alpha, dynamicFirst, dynamicSecond)
        };
        foreach (DensityFunction function in functions)
        {
            DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
            function.CompileSampler(Context).SampleVolume(context, output, volume);
            Require(Enumerable.Range(0, output.Size())
                    .All(index => output.Get(index) == 5.0f),
                "All three Lerp sampler variants must apply alpha interpolation across a volume.");
        }

        Require(arena.Acquired == arena.Released,
            "Every Lerp sampler variant must release its scoped scratch buffers after volume sampling.");
    }

    private static void CheckRangeAndDomainAxes()
    {
        var alpha = new Fixture(
            Interval.Of(-1.0f, 2.0f), DensityFunction.AXIS_Y, new CountingSampler(0.5f));
        var first = new Fixture(
            Interval.Of(2.0f, 4.0f), DensityFunction.AXIS_X, new CountingSampler(3.0f));
        var second = new Fixture(
            Interval.Of(6.0f, 8.0f), DensityFunction.AXIS_Z, new CountingSampler(7.0f));

        DensityFunction function = DensityFunctions.Lerp(alpha, first, second);
        Require(function.Range().Equals(Interval.Lerp(alpha.Range(), first.Range(), second.Range())),
            "LerpFunction.range must delegate to the exact Interval.lerp operation.");
        Require(function.DomainAxes() ==
                (DensityFunction.AXIS_X | DensityFunction.AXIS_Y | DensityFunction.AXIS_Z),
            "LerpFunction.domainAxes must union the three child coordinate masks.");
    }

    private static void CheckRewrite()
    {
        DensityFunction alpha = DensityFunctions.Constant(0.5f);
        DensityFunction first = DensityFunctions.Constant(2.0f);
        DensityFunction second = DensityFunctions.Constant(8.0f);
        var function = new LerpFunction(alpha, first, second);

        Require(ReferenceEquals(function.RewriteChildren(child => child), function),
            "LerpFunction.rewriteChildren must preserve node identity when all children are unchanged.");

        DensityFunction rewritten = function.RewriteChildren(_ => DensityFunctions.Constant(10.0f));
        Require(rewritten is LerpFunction lerp &&
            lerp.Alpha.Equals(DensityFunctions.Constant(10.0f)) &&
            lerp.First.Equals(DensityFunctions.Constant(10.0f)) &&
            lerp.Second.Equals(DensityFunctions.Constant(10.0f)),
            "LerpFunction.rewriteChildren must rewrite alpha, first and second in the declared order.");
    }

    private static DensitySampler Compile(DensityFunction function) =>
        function.CompileSampler(Context);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
