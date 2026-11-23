using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.DensityFunctions.Operations;

internal static class RoundFunctionChecks
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
        CheckTypeIdsAndIntegerSpecialization();
        CheckScalarOperations();
        CheckMultipleAndZeroBehavior();
        CheckVolumeAndBufferLifecycle();
        CheckRangeDomainAndRewrite();
    }

    private static void CheckTypeIdsAndIntegerSpecialization()
    {
        RoundFunction.Type[] expectedTypes =
        {
            RoundFunction.Type.FLOOR,
            RoundFunction.Type.ROUND,
            RoundFunction.Type.CEIL,
            RoundFunction.Type.TRUNCATE
        };
        string[] expectedIds = { "floor", "round", "ceil", "truncate" };
        Require(Enum.GetValues<RoundFunction.Type>().SequenceEqual(expectedTypes),
            "RoundFunction.Type must preserve the exact Minecraft 26.3 enum order.");
        for (int index = 0; index < expectedTypes.Length; index++)
            Require(RoundFunction.GetId(expectedTypes[index]) == expectedIds[index],
                "RoundFunction type ids must match the vanilla density-function registry names.");

        var dynamic = new Fixture(
            Interval.Of(-10.0f, 10.0f),
            DensityFunction.AXIS_X,
            new CountingSampler(2.4f));

        Require(Compile(DensityFunctions.Round(RoundFunction.Type.FLOOR, dynamic))
                .GetType().Name == "IntegerMultipleSampler",
            "RoundFunction must use IntegerMultipleSampler for a constant multiple exactly equal to one.");

        Require(Compile(DensityFunctions.Round(
                    RoundFunction.Type.FLOOR, dynamic, DensityFunctions.Constant(2.0f)))
                .GetType().Name == "Sampler" &&
            Compile(DensityFunctions.Round(
                    RoundFunction.Type.FLOOR, dynamic,
                    new Fixture(Interval.OfExact(1.0f), 0, new CountingSampler(1.0f))))
                .GetType().Name == "Sampler",
            "RoundFunction must use the generic sampler for non-unit constants and dynamic multiples.");
    }

    private static void CheckScalarOperations()
    {
        Require(Sample(DensityFunctions.Floor(DensityFunctions.Constant(1.8f))) == 1.0f &&
            Sample(DensityFunctions.Floor(DensityFunctions.Constant(-1.8f))) == -2.0f,
            "RoundFunction.FLOOR must use mathematical floor for positive and negative values.");

        Require(Sample(DensityFunctions.Round(DensityFunctions.Constant(1.5f))) == 2.0f &&
            Sample(DensityFunctions.Round(DensityFunctions.Constant(-1.5f))) == -1.0f &&
            Sample(DensityFunctions.Round(DensityFunctions.Constant(-1.6f))) == -2.0f,
            "RoundFunction.ROUND must match Java Math.round, including negative half ties toward positive infinity.");

        Require(Sample(DensityFunctions.Ceil(DensityFunctions.Constant(1.2f))) == 2.0f &&
            Sample(DensityFunctions.Ceil(DensityFunctions.Constant(-1.2f))) == -1.0f,
            "RoundFunction.CEIL must use mathematical ceiling.");

        Require(Sample(DensityFunctions.Truncate(DensityFunctions.Constant(1.9f))) == 1.0f &&
            Sample(DensityFunctions.Truncate(DensityFunctions.Constant(-1.9f))) == -1.0f &&
            Sample(DensityFunctions.Truncate(DensityFunctions.Constant(0.0f))) == 0.0f,
            "RoundFunction.TRUNCATE must round toward zero.");

        Require(Sample(DensityFunctions.Round(DensityFunctions.Constant(float.NaN))) == 0.0f &&
            Sample(DensityFunctions.Round(DensityFunctions.Constant(float.PositiveInfinity))) == int.MaxValue &&
            Sample(DensityFunctions.Round(DensityFunctions.Constant(float.NegativeInfinity))) == int.MinValue,
            "RoundFunction.ROUND must preserve Java Math.round NaN and saturating infinity behavior.");
    }

    private static void CheckMultipleAndZeroBehavior()
    {
        Require(Sample(DensityFunctions.Floor(
                    DensityFunctions.Constant(5.7f),
                    DensityFunctions.Constant(2.0f))) == 4.0f &&
            Sample(DensityFunctions.Ceil(
                    DensityFunctions.Constant(5.7f),
                    DensityFunctions.Constant(2.0f))) == 6.0f &&
            Sample(DensityFunctions.Round(
                    DensityFunctions.Constant(5.0f),
                    DensityFunctions.Constant(2.0f))) == 6.0f &&
            Sample(DensityFunctions.Truncate(
                    DensityFunctions.Constant(-5.7f),
                    DensityFunctions.Constant(2.0f))) == -4.0f,
            "RoundFunction must divide by the multiple, round the quotient, then multiply by the multiple.");

        Require(Sample(DensityFunctions.Floor(
                    DensityFunctions.Constant(-5.7f),
                    DensityFunctions.Constant(0.0f))) == -5.7f &&
            float.IsNaN(Sample(DensityFunctions.Round(
                    DensityFunctions.Constant(float.NaN),
                    DensityFunctions.Constant(0.0f))),
            "RoundFunction generic sampler must return the input unchanged when multiple is zero.");

        var multipleSampler = new CountingSampler(2.0f);
        DensityFunction dynamicMultiple = new Fixture(
            Interval.OfExact(2.0f),
            DensityFunction.AXIS_Y,
            multipleSampler);
        Require(Sample(DensityFunctions.Ceil(
                    new Fixture(Interval.OfExact(5.7f), DensityFunction.AXIS_X, new CountingSampler(5.7f)),
                    dynamicMultiple)) == 6.0f &&
            multipleSampler.PointCalls == 1,
            "RoundFunction must sample dynamic multiple density functions.");
    }

    private static void CheckVolumeAndBufferLifecycle()
    {
        var arena = new CountingArena();
        SamplerContext context = SamplerContext.CreateBuilder().UseBufferArena(arena).Build();
        var volume = new DensityVolume(2, 2, 2, -1, 10, 20);

        DensityFunction generic = DensityFunctions.Round(
            RoundFunction.Type.CEIL,
            new Fixture(Interval.OfExact(5.7f), DensityFunction.AXIS_X, new CountingSampler(5.7f)),
            new Fixture(Interval.OfExact(2.0f), DensityFunction.AXIS_Y, new CountingSampler(2.0f)));
        DensityBuffer genericOutput = DensityBuffer.CreateUnpooled(volume.Size());
        generic.CompileSampler(Context).SampleVolume(context, genericOutput, volume);
        Require(Enumerable.Range(0, genericOutput.Size()).All(index => genericOutput.Get(index) == 6.0f) &&
            arena.Acquired == 1 && arena.Released == 1,
            "RoundFunction generic volume sampler must evaluate a multiple buffer and release it.");

        DensityFunction integer = DensityFunctions.Round(
            RoundFunction.Type.ROUND,
            new Fixture(Interval.OfExact(-1.5f), DensityFunction.AXIS_X, new CountingSampler(-1.5f)));
        DensityBuffer integerOutput = DensityBuffer.CreateUnpooled(volume.Size());
        integer.CompileSampler(Context).SampleVolume(context, integerOutput, volume);
        Require(Enumerable.Range(0, integerOutput.Size()).All(index => integerOutput.Get(index) == -1.0f),
            "RoundFunction integer-multiple sampler must map its operation across the active volume.");
    }

    private static void CheckRangeDomainAndRewrite()
    {
        DensityFunction input = new Fixture(
            Interval.Of(-3.5f, 6.5f),
            DensityFunction.AXIS_X,
            new CountingSampler(1.5f));
        DensityFunction multiple = new Fixture(
            Interval.Of(1.0f, 2.0f),
            DensityFunction.AXIS_Z,
            new CountingSampler(1.5f));
        var function = new RoundFunction(RoundFunction.Type.FLOOR, input, multiple);

        Interval expectedRange = Interval.Mul(
            Interval.MapMonotonic(
                Interval.Div(input.Range(), multiple.Range()),
                value => (float)Math.Floor(value)),
            multiple.Range());
        Require(function.Range().Equals(expectedRange) &&
            function.DomainAxes() == (DensityFunction.AXIS_X | DensityFunction.AXIS_Z),
            "RoundFunction.range must apply interval divide, monotonic integer rounding and multiply; domainAxes must union both inputs.");

        Require(ReferenceEquals(function.RewriteChildren(child => child), function),
            "RoundFunction.rewriteChildren must preserve identity when neither child changes.");

        DensityFunction rewritten = function.RewriteChildren(_ => DensityFunctions.Constant(7.0f));
        Require(rewritten is RoundFunction round &&
            round.Operation == RoundFunction.Type.FLOOR &&
            round.Input.Equals(DensityFunctions.Constant(7.0f)) &&
            round.Multiple.Equals(DensityFunctions.Constant(7.0f)),
            "RoundFunction.rewriteChildren must rewrite input then multiple and retain the operation.");
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
