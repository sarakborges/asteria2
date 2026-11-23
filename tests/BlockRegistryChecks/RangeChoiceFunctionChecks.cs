using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Operations;

internal static class RangeChoiceFunctionChecks
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
        CheckInclusiveAndExclusiveBounds();
        CheckCompileSpecialization();
        CheckGenericScalarSelection();
        CheckVolumeAndBufferLifecycle();
        CheckRangeAxesAndRewrite();
    }

    private static void CheckInclusiveAndExclusiveBounds()
    {
        DensityFunction input = new Fixture(
            Interval.Of(-10.0f, 10.0f),
            DensityFunction.AXIS_X,
            new CountingSampler(0.0f));

        Require(Sample(DensityFunctions.RangeChoice(
                    new Fixture(Interval.OfExact(2.0f), 0, new CountingSampler(2.0f)),
                    2.0f, 5.0f,
                    DensityFunctions.Constant(11.0f),
                    DensityFunctions.Constant(-7.0f))) == 11.0f,
            "RangeChoiceFunction must include the lower boundary.");
        Require(Sample(DensityFunctions.RangeChoice(
                    new Fixture(Interval.OfExact(4.0f), 0, new CountingSampler(4.0f)),
                    2.0f, 5.0f,
                    DensityFunctions.Constant(11.0f),
                    DensityFunctions.Constant(-7.0f))) == 11.0f,
            "RangeChoiceFunction must select the in-range branch for interior values.");
        Require(Sample(DensityFunctions.RangeChoice(
                    new Fixture(Interval.OfExact(5.0f), 0, new CountingSampler(5.0f)),
                    2.0f, 5.0f,
                    DensityFunctions.Constant(11.0f),
                    DensityFunctions.Constant(-7.0f))) == -7.0f,
            "RangeChoiceFunction must exclude the upper boundary.");
        Require(Sample(DensityFunctions.RangeChoice(
                    new Fixture(Interval.OfExact(1.999f), 0, new CountingSampler(1.999f)),
                    2.0f, 5.0f,
                    DensityFunctions.Constant(11.0f),
                    DensityFunctions.Constant(-7.0f))) == -7.0f,
            "RangeChoiceFunction must select the out-of-range branch below the lower boundary.");

        Require(input.DomainAxes() == DensityFunction.AXIS_X,
            "RangeChoiceFunction test fixture must retain its own input domain.");
    }

    private static void CheckCompileSpecialization()
    {
        DensityFunction input = new Fixture(
            Interval.Of(-10.0f, 10.0f),
            DensityFunction.AXIS_Y,
            new CountingSampler(3.0f));
        DensityFunction dynamicBranch = new Fixture(
            Interval.Of(-1.0f, 1.0f),
            DensityFunction.AXIS_Z,
            new CountingSampler(0.5f));

        Require(Compile(DensityFunctions.RangeChoice(
                    input, 0.0f, 4.0f,
                    DensityFunctions.Constant(1.0f),
                    DensityFunctions.Constant(2.0f))) is RangeChoiceFunction.ConstSampler,
            "RangeChoiceFunction must create ConstSampler when both outputs are ConstantFunction nodes.");

        Require(Compile(DensityFunctions.RangeChoice(
                    input, 0.0f, 4.0f,
                    DensityFunctions.Constant(1.0f),
                    dynamicBranch)) is RangeChoiceFunction.Sampler &&
            Compile(DensityFunctions.RangeChoice(
                    input, 0.0f, 4.0f,
                    dynamicBranch,
                    DensityFunctions.Constant(2.0f))) is RangeChoiceFunction.Sampler,
            "RangeChoiceFunction must use the generic sampler when either output branch is dynamic.");
    }

    private static void CheckGenericScalarSelection()
    {
        var inputSampler = new CountingSampler(2.0f);
        var inRangeSampler = new CountingSampler(7.0f);
        var outRangeSampler = new CountingSampler(-3.0f);
        DensityFunction function = DensityFunctions.RangeChoice(
            new Fixture(Interval.OfExact(2.0f), DensityFunction.AXIS_X, inputSampler),
            1.0f,
            3.0f,
            new Fixture(Interval.OfExact(7.0f), DensityFunction.AXIS_Y, inRangeSampler),
            new Fixture(Interval.OfExact(-3.0f), DensityFunction.AXIS_Z, outRangeSampler));

        Require(Sample(function) == 7.0f &&
            inRangeSampler.PointCalls == 1 &&
            outRangeSampler.PointCalls == 0,
            "RangeChoiceFunction.Sampler must evaluate only the selected output branch during point sampling.");
    }

    private static void CheckVolumeAndBufferLifecycle()
    {
        var input = new Fixture(Interval.Of(0.0f, 10.0f), DensityFunction.AXIS_X,
            new CountingSampler(2.0f));
        var inRange = new Fixture(Interval.OfExact(10.0f), DensityFunction.AXIS_Y,
            new CountingSampler(10.0f));
        var outRange = new Fixture(Interval.OfExact(-10.0f), DensityFunction.AXIS_Z,
            new CountingSampler(-10.0f));
        DensityFunction function = DensityFunctions.RangeChoice(input, 1.0f, 3.0f, inRange, outRange);
        var volume = new DensityVolume(2, 2, 2, -1, 10, 20);
        var arena = new CountingArena();
        SamplerContext context = SamplerContext.CreateBuilder().UseBufferArena(arena).Build();
        DensityBuffer buffer = DensityBuffer.CreateUnpooled(volume.Size());

        function.CompileSampler(Context).SampleVolume(context, buffer, volume);
        Require(Enumerable.Range(0, buffer.Size()).All(index => buffer.Get(index) == 10.0f) &&
            input.Sampler.VolumeCalls == 1 &&
            inRange.Sampler.VolumeCalls == 1 &&
            outRange.Sampler.VolumeCalls == 1,
            "RangeChoiceFunction.Sampler.sampleVolume must fill the in-range output first, sample input and out-of-range buffers, then keep the selected values.");
        Require(arena.Acquired == 2 && arena.Released == 2,
            "RangeChoiceFunction generic volume sampling must release both scoped scratch buffers.");
    }

    private static void CheckRangeAxesAndRewrite()
    {
        DensityFunction input = new Fixture(Interval.Of(-10.0f, 10.0f), DensityFunction.AXIS_X,
            new CountingSampler(0.0f));
        DensityFunction whenIn = new Fixture(Interval.Of(-2.0f, 3.0f), DensityFunction.AXIS_Y,
            new CountingSampler(1.0f));
        DensityFunction whenOut = new Fixture(Interval.Of(8.0f, 9.0f), DensityFunction.AXIS_Z,
            new CountingSampler(8.0f));
        var function = new RangeChoiceFunction(input, -4.0f, 5.0f, whenIn, whenOut);

        Require(function.Range().Equals(Interval.Of(-2.0f, 9.0f)) &&
            function.DomainAxes() ==
                (DensityFunction.AXIS_X | DensityFunction.AXIS_Y | DensityFunction.AXIS_Z),
            "RangeChoiceFunction range must encapsulate the two output ranges and domainAxes must union all three children.");

        Require(ReferenceEquals(function.RewriteChildren(child => child), function),
            "RangeChoiceFunction.rewriteChildren must preserve identity when no child changes.");

        DensityFunction rewritten = function.RewriteChildren(_ => DensityFunctions.Constant(4.0f));
        Require(rewritten is RangeChoiceFunction choice &&
            choice.MinInclusive == -4.0f &&
            choice.MaxExclusive == 5.0f &&
            choice.Input.Equals(DensityFunctions.Constant(4.0f)) &&
            choice.WhenInRange.Equals(DensityFunctions.Constant(4.0f)) &&
            choice.WhenOutOfRange.Equals(DensityFunctions.Constant(4.0f)),
            "RangeChoiceFunction.rewriteChildren must rewrite input and both output branches in order while retaining thresholds.");
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
