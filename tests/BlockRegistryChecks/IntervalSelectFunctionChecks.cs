using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Operations;

internal static class IntervalSelectFunctionChecks
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

    private sealed class ThrowingSampler : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume) =>
            throw new InvalidOperationException("expected sampler failure");

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            throw new InvalidOperationException("expected sampler failure");
    }

    private sealed record Fixture(
        Interval ValueRange,
        int Axes,
        DensitySampler Sampler) : DensityFunction
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
        CheckThresholdSelection();
        CheckSamplerSpecialization();
        CheckVolumeSelectionAndCleanup();
        CheckRangeAxesAndRewrite();
    }

    private static void CheckThresholdSelection()
    {
        DensityFunction[] outputs =
        {
            DensityFunctions.Constant(10.0f),
            DensityFunctions.Constant(20.0f),
            DensityFunctions.Constant(30.0f)
        };
        float[] thresholds = { 1.0f, 3.0f };

        Require(Sample(DensityFunctions.IntervalSelect(
                    DensityFunctions.Constant(0.0f), thresholds, outputs)) == 10.0f,
            "IntervalSelectFunction must select the first output when input is below the first threshold.");
        Require(Sample(DensityFunctions.IntervalSelect(
                    DensityFunctions.Constant(1.0f), thresholds, outputs)) == 20.0f,
            "IntervalSelectFunction thresholds must be strict: equality with a threshold selects the next output.");
        Require(Sample(DensityFunctions.IntervalSelect(
                    DensityFunctions.Constant(2.0f), thresholds, outputs)) == 20.0f,
            "IntervalSelectFunction must select the matching middle interval.");
        Require(Sample(DensityFunctions.IntervalSelect(
                    DensityFunctions.Constant(3.0f), thresholds, outputs)) == 30.0f &&
            Sample(DensityFunctions.IntervalSelect(
                    DensityFunctions.Constant(float.NaN), thresholds, outputs)) == 30.0f,
            "IntervalSelectFunction must select the last output when no threshold is greater than the input, including NaN.");
    }

    private static void CheckSamplerSpecialization()
    {
        var inputSampler = new CountingSampler(2.0f);
        var firstSampler = new CountingSampler(10.0f);
        var lastSampler = new CountingSampler(30.0f);
        DensityFunction input = new Fixture(
            Interval.Of(-10.0f, 10.0f), DensityFunction.AXIS_X, inputSampler);
        DensityFunction first = new Fixture(
            Interval.OfExact(10.0f), DensityFunction.AXIS_Y, firstSampler);
        DensityFunction last = new Fixture(
            Interval.OfExact(30.0f), DensityFunction.AXIS_Z, lastSampler);

        DensityFunction oneThreshold = DensityFunctions.IntervalSelect(
            input, new[] { 3.0f }, new[] { first, last });
        Require(Compile(oneThreshold).GetType().Name == "SingleThresholdSampler",
            "IntervalSelectFunction must use its dedicated sampler when exactly one threshold is supplied.");

        Require(Sample(oneThreshold) == 10.0f &&
            firstSampler.PointCalls == 1 &&
            lastSampler.PointCalls == 0,
            "SingleThresholdSampler point evaluation must call only the selected output.");

        DensityFunction multipleThresholds = DensityFunctions.IntervalSelect(
            input,
            new[] { 1.0f, 3.0f },
            new[]
            {
                first,
                new Fixture(Interval.OfExact(20.0f), 0, new CountingSampler(20.0f)),
                last
            });
        Require(Compile(multipleThresholds).GetType().Name == "Sampler",
            "IntervalSelectFunction must use its generic sampler for multiple thresholds.");
    }

    private static void CheckVolumeSelectionAndCleanup()
    {
        var volume = new DensityVolume(2, 2, 2, -1, 10, 20);
        var arena = new CountingArena();
        SamplerContext context = SamplerContext.CreateBuilder().UseBufferArena(arena).Build();

        var inputSampler = new CountingSampler(2.0f);
        var outputSamplers = new[]
        {
            new CountingSampler(10.0f),
            new CountingSampler(20.0f),
            new CountingSampler(30.0f)
        };
        DensityFunction function = DensityFunctions.IntervalSelect(
            new Fixture(Interval.OfExact(2.0f), 0, inputSampler),
            new[] { 1.0f, 3.0f },
            outputSamplers.Select(sampler =>
                (DensityFunction)new Fixture(Interval.OfExact(sampler == outputSamplers[1] ? 20.0f :
                    sampler == outputSamplers[2] ? 30.0f : 10.0f), 0, sampler)).ToArray());

        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        function.CompileSampler(Context).SampleVolume(context, output, volume);
        Require(Enumerable.Range(0, output.Size()).All(index => output.Get(index) == 20.0f) &&
            outputSamplers.All(sampler => sampler.VolumeCalls == 1) &&
            arena.Acquired == 3 && arena.Released == 3,
            "Generic IntervalSelectFunction volume evaluation must sample each output, select the threshold branch and release all buffers.");

        var oneThresholdArena = new CountingArena();
        SamplerContext oneThresholdContext = SamplerContext.CreateBuilder()
            .UseBufferArena(oneThresholdArena)
            .Build();
        DensityFunction single = DensityFunctions.IntervalSelect(
            DensityFunctions.Constant(4.0f),
            new[] { 3.0f },
            new[] { DensityFunctions.Constant(10.0f), DensityFunctions.Constant(30.0f) });
        DensityBuffer singleOutput = DensityBuffer.CreateUnpooled(volume.Size());
        single.CompileSampler(Context).SampleVolume(oneThresholdContext, singleOutput, volume);
        Require(Enumerable.Range(0, singleOutput.Size()).All(index => singleOutput.Get(index) == 30.0f) &&
            oneThresholdArena.Acquired == 2 && oneThresholdArena.Released == 2,
            "Single-threshold volume sampler must sample both output buffers and release them.");

        var failureArena = new CountingArena();
        SamplerContext failureContext = SamplerContext.CreateBuilder().UseBufferArena(failureArena).Build();
        DensityFunction failing = DensityFunctions.IntervalSelect(
            DensityFunctions.Constant(0.0f),
            new[] { -1.0f, 1.0f },
            new DensityFunction[]
            {
                DensityFunctions.Constant(10.0f),
                new Fixture(Interval.INFINITE, 0, new ThrowingSampler()),
                DensityFunctions.Constant(30.0f)
            });
        try
        {
            failing.CompileSampler(Context).SampleVolume(
                failureContext, DensityBuffer.CreateUnpooled(volume.Size()), volume);
            throw new InvalidOperationException("IntervalSelectFunction should propagate a child sampler failure.");
        }
        catch (InvalidOperationException exception) when (exception.Message == "expected sampler failure")
        {
        }

        Require(failureArena.Acquired == 2 && failureArena.Released == 2,
            "IntervalSelectFunction must release all buffers acquired before a child sampler throws.");
    }

    private static void CheckRangeAxesAndRewrite()
    {
        DensityFunction input = new Fixture(
            Interval.Of(-10.0f, 10.0f), DensityFunction.AXIS_X, new CountingSampler(0.0f));
        DensityFunction first = new Fixture(
            Interval.Of(-2.0f, 3.0f), DensityFunction.AXIS_Y, new CountingSampler(1.0f));
        DensityFunction second = new Fixture(
            Interval.Of(8.0f, 9.0f), DensityFunction.AXIS_Z, new CountingSampler(8.0f));
        DensityFunction function = DensityFunctions.IntervalSelect(
            input, new[] { 1.0f }, new[] { first, second });

        Require(function.Range().Equals(Interval.Of(-2.0f, 9.0f)) &&
            function.DomainAxes() ==
                (DensityFunction.AXIS_X | DensityFunction.AXIS_Y | DensityFunction.AXIS_Z),
            "IntervalSelectFunction.range must encapsulate all outputs and domainAxes must union input and output axes.");

        Require(ReferenceEquals(function.RewriteChildren(child => child), function),
            "IntervalSelectFunction.rewriteChildren must preserve identity when no child changes.");

        DensityFunction rewritten = function.RewriteChildren(_ => DensityFunctions.Constant(5.0f));
        Require(rewritten is IntervalSelectFunction select &&
            select.Thresholds.SequenceEqual(new[] { 1.0f }) &&
            select.Input.Equals(DensityFunctions.Constant(5.0f)) &&
            select.Functions.Count == 2 &&
            select.Functions.All(child => child.Equals(DensityFunctions.Constant(5.0f))),
            "IntervalSelectFunction.rewriteChildren must rewrite input followed by all output functions while preserving thresholds.");
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
