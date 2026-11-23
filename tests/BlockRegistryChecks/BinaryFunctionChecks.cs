using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.DensityFunctions.Operations;

internal static class BinaryFunctionChecks
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

    private sealed record RangeSampler(Interval ValueRange, float Value) : DensitySampler
    {
        public int PointSamples { get; private set; }
        public int VolumeSamples { get; private set; }

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            VolumeSamples++;
            outputBuffer.Fill(Value);
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            PointSamples++;
            return Value;
        }
    }

    private sealed record FixtureFunction(
        Interval ValueRange,
        int Axes,
        RangeSampler Sampler) : DensityFunction
    {
        public DensitySampler CompileSampler(DensityFunction.CompileContext context) => Sampler;
        public DensityFunction RewriteChildren(DfRewriteRule rule) => this;
        public Interval Range() => ValueRange;
        public int DomainAxes() => Axes;
    }

    public static void Run()
    {
        CheckTypeAndIds();
        CheckScalarOperationsAndSpecializations();
        CheckZeroShortCircuit();
        CheckRangesAndDomainAxes();
        CheckVolumeSamplersAndArenaLifecycle();
        CheckNonOverlappingRangePruning();
        CheckRewriteAndFactories();
    }

    private static void CheckTypeAndIds()
    {
        BinaryFunction.Type[] expected =
        {
            BinaryFunction.Type.ADD,
            BinaryFunction.Type.SUB,
            BinaryFunction.Type.MUL,
            BinaryFunction.Type.DIV,
            BinaryFunction.Type.MIN,
            BinaryFunction.Type.MAX
        };
        string[] ids = { "add", "sub", "mul", "div", "min", "max" };
        Require(Enum.GetValues<BinaryFunction.Type>().SequenceEqual(expected),
            "BinaryFunction.Type must preserve the exact Minecraft 26.3 enum order.");

        for (int index = 0; index < expected.Length; index++)
            Require(BinaryFunction.GetId(expected[index]) == ids[index],
                "BinaryFunction.Type ids must match the 26.3 registry names.");
    }

    private static void CheckScalarOperationsAndSpecializations()
    {
        DensityFunction constant = DensityFunctions.Constant(6.0f);
        DensityFunction negative = DensityFunctions.Constant(-2.0f);

        Require(Sample(DensityFunctions.Add(constant, negative)) == 4.0f &&
            Sample(DensityFunctions.Sub(constant, negative)) == 8.0f &&
            Sample(DensityFunctions.Mul(constant, negative)) == -12.0f &&
            Sample(DensityFunctions.Div(constant, negative)) == -3.0f &&
            Sample(DensityFunctions.Min(constant, negative)) == -2.0f &&
            Sample(DensityFunctions.Max(constant, negative)) == 6.0f,
            "BinaryFunction must evaluate all six scalar operations with their left/right operand order.");

        Require(Sample(DensityFunctions.Constant(8.0f).Sub(DensityFunctions.Constant(3.0f))) == 5.0f &&
            Sample(DensityFunctions.Constant(8.0f).Div(DensityFunctions.Constant(2.0f))) == 4.0f &&
            Sample(DensityFunctions.Constant(8.0f).Add(3.0f)) == 11.0f &&
            Sample(DensityFunctions.Constant(8.0f).Mul(0.5f)) == 4.0f,
            "DensityFunction default binary methods must delegate through the runtime factory API.");

        DensityFunction dynamic = new FixtureFunction(
            Interval.Of(-10.0f, 10.0f),
            DensityFunction.AXIS_X,
            new RangeSampler(Interval.Of(-10.0f, 10.0f), -3.0f));

        Require(Compile(DensityFunctions.Add(dynamic, 5.0f)) is BinaryFunction.ConstAddSampler &&
            Compile(DensityFunctions.Add(DensityFunctions.Constant(5.0f), dynamic)) is BinaryFunction.ConstAddSampler &&
            Compile(DensityFunctions.Sub(5.0f, dynamic)) is BinaryFunction.ConstSubSampler &&
            Compile(DensityFunctions.Sub(dynamic, 5.0f)) is BinaryFunction.ConstAddSampler &&
            Compile(DensityFunctions.Mul(dynamic, 5.0f)) is BinaryFunction.ConstMulSampler &&
            Compile(DensityFunctions.Mul(DensityFunctions.Constant(5.0f), dynamic)) is BinaryFunction.ConstMulSampler &&
            Compile(DensityFunctions.Div(5.0f, dynamic)) is BinaryFunction.ConstDivSampler &&
            Compile(DensityFunctions.Div(dynamic, 5.0f)) is BinaryFunction.ConstMulSampler,
            "BinaryFunction.compileSampler must select the vanilla constant-specialized sampler in operand order.");

        Require(Sample(DensityFunctions.Sub(DensityFunctions.Constant(5.0f), dynamic)) == 8.0f &&
            Sample(DensityFunctions.Div(DensityFunctions.Constant(5.0f), dynamic)) == -5.0f,
            "Constant-left subtraction and division samplers must preserve their non-commutative operand order.");
    }

    private static void CheckZeroShortCircuit()
    {
        var rightSamplerForMul = new RangeSampler(Interval.INFINITE, 9.0f);
        var leftZeroForMul = new RangeSampler(Interval.OfExact(0.0f), 0.0f);
        DensityFunction mul = new BinaryFunction(
            BinaryFunction.Type.MUL,
            new FixtureFunction(Interval.OfExact(0.0f), 0, leftZeroForMul),
            new FixtureFunction(Interval.INFINITE, 0, rightSamplerForMul));

        Require(Sample(mul) == 0.0f && rightSamplerForMul.PointSamples == 0,
            "BinaryFunction.MulSampler.sampleValue must return zero without sampling the right operand when left is zero.");

        var rightSamplerForDiv = new RangeSampler(Interval.INFINITE, 0.0f);
        var leftZeroForDiv = new RangeSampler(Interval.OfExact(0.0f), 0.0f);
        DensityFunction div = new BinaryFunction(
            BinaryFunction.Type.DIV,
            new FixtureFunction(Interval.OfExact(0.0f), 0, leftZeroForDiv),
            new FixtureFunction(Interval.INFINITE, 0, rightSamplerForDiv));

        Require(Sample(div) == 0.0f && rightSamplerForDiv.PointSamples == 0,
            "BinaryFunction.DivSampler.sampleValue must return zero without sampling the right operand when left is zero.");

        Require(float.IsNaN(Sample(DensityFunctions.Constant(0.0f).Div(
                    new FixtureFunction(Interval.OfExact(0.0f), 0,
                        new RangeSampler(Interval.OfExact(0.0f), 0.0f))))),
            "ConstDivSampler must retain constant-over-zero IEEE float division rather than inheriting the general DivSampler zero short-circuit.");
    }

    private static void CheckRangesAndDomainAxes()
    {
        DensityFunction left = new RangeFixture(Interval.Of(-3.0f, 2.0f), DensityFunction.AXIS_X);
        DensityFunction right = new RangeFixture(Interval.Of(4.0f, 5.0f), DensityFunction.AXIS_Z);

        Require(DensityFunctions.Add(left, right).Range().Equals(Interval.Of(1.0f, 7.0f)) &&
            DensityFunctions.Sub(left, right).Range().Equals(Interval.Of(-8.0f, -2.0f)) &&
            DensityFunctions.Mul(left, right).Range().Equals(Interval.Of(-15.0f, 10.0f)) &&
            DensityFunctions.Div(left, right).Range().Equals(Interval.Of(-0.75f, 0.5f)) &&
            DensityFunctions.Min(left, right).Range().Equals(Interval.Of(-3.0f, 2.0f)) &&
            DensityFunctions.Max(left, right).Range().Equals(Interval.Of(4.0f, 5.0f)),
            "BinaryFunction.range must delegate each operation to the exact interval arithmetic operation.");

        Require(DensityFunctions.Add(left, right).DomainAxes() ==
                (DensityFunction.AXIS_X | DensityFunction.AXIS_Z),
            "BinaryFunction.domainAxes must OR the domain-axis masks from both children.");
    }

    private static void CheckVolumeSamplersAndArenaLifecycle()
    {
        var arena = new CountingArena();
        SamplerContext context = SamplerContext.CreateBuilder().UseBufferArena(arena).Build();
        var volume = new DensityVolume(2, 2, 2, -1, 10, 20);

        (DensityFunction Function, float Expected)[] cases =
        {
            (DensityFunctions.Add(DensityFunctions.Constant(4.0f), DensityFunctions.Constant(3.0f)), 7.0f),
            (DensityFunctions.Sub(DensityFunctions.Constant(4.0f), DensityFunctions.Constant(3.0f)), 1.0f),
            (DensityFunctions.Mul(DensityFunctions.Constant(4.0f), DensityFunctions.Constant(3.0f)), 12.0f),
            (DensityFunctions.Div(DensityFunctions.Constant(4.0f), DensityFunctions.Constant(2.0f)), 2.0f),
            (DensityFunctions.Min(DensityFunctions.Constant(4.0f), DensityFunctions.Constant(3.0f)), 3.0f),
            (DensityFunctions.Max(DensityFunctions.Constant(4.0f), DensityFunctions.Constant(3.0f)), 4.0f)
        };

        foreach ((DensityFunction function, float expected) in cases)
        {
            var output = DensityBuffer.CreateUnpooled(volume.Size());
            function.CompileSampler(Context).SampleVolume(context, output, volume);
            Require(Enumerable.Range(0, output.Size()).All(index => output.Get(index) == expected),
                "All binary sampler records must fill output volumes with their scalar operation results.");
        }

        DensityFunction dynamic = new FixtureFunction(
            Interval.Of(1.0f, 3.0f),
            DensityFunction.AXIS_X,
            new RangeSampler(Interval.Of(1.0f, 3.0f), 2.0f));
        DensityFunction dynamicRight = new FixtureFunction(
            Interval.Of(2.0f, 4.0f),
            DensityFunction.AXIS_Z,
            new RangeSampler(Interval.Of(2.0f, 4.0f), 3.0f));

        DensityFunction[] genericCases =
        {
            DensityFunctions.Add(dynamic, dynamicRight),
            DensityFunctions.Sub(dynamic, dynamicRight),
            DensityFunctions.Mul(dynamic, dynamicRight),
            DensityFunctions.Div(dynamic, dynamicRight),
            DensityFunctions.Min(dynamic, dynamicRight),
            DensityFunctions.Max(dynamic, dynamicRight)
        };
        float[] expectedGeneric = { 5.0f, -1.0f, 6.0f, 2.0f / 3.0f, 2.0f, 3.0f };
        foreach (int index in Enumerable.Range(0, genericCases.Length))
        {
            var output = DensityBuffer.CreateUnpooled(volume.Size());
            genericCases[index].CompileSampler(Context).SampleVolume(context, output, volume);
            Require(Math.Abs(output.Get(0) - expectedGeneric[index]) < 0.000001f,
                "Generic binary volume samplers must evaluate both child buffers with the expected operation.");
        }

        Require(arena.Acquired == arena.Released,
            "Binary samplers that need a second input buffer must release every scoped buffer after volume evaluation.");
    }

    private static void CheckNonOverlappingRangePruning()
    {
        var leftSampler = new RangeSampler(Interval.Of(10.0f, 20.0f), 12.0f);
        var rightSampler = new RangeSampler(Interval.Of(-5.0f, 1.0f), -2.0f);
        DensityFunction left = new FixtureFunction(Interval.Of(10.0f, 20.0f), 0, leftSampler);
        DensityFunction right = new FixtureFunction(Interval.Of(-5.0f, 1.0f), 0, rightSampler);

        Require(ReferenceEquals(Compile(DensityFunctions.Min(left, right)), rightSampler) &&
            ReferenceEquals(Compile(DensityFunctions.Max(left, right)), leftSampler),
            "BinaryFunction.min/max must return the already dominant child sampler when child ranges do not overlap.");

        var alternateLeft = new RangeSampler(Interval.Of(-5.0f, 1.0f), -2.0f);
        var alternateRight = new RangeSampler(Interval.Of(10.0f, 20.0f), 12.0f);
        DensityFunction a = new FixtureFunction(Interval.Of(-5.0f, 1.0f), 0, alternateLeft);
        DensityFunction b = new FixtureFunction(Interval.Of(10.0f, 20.0f), 0, alternateRight);

        Require(ReferenceEquals(Compile(DensityFunctions.Min(a, b)), alternateLeft) &&
            ReferenceEquals(Compile(DensityFunctions.Max(a, b)), alternateRight),
            "BinaryFunction.min/max must preserve the other non-overlap pruning branch.");
    }

    private static void CheckRewriteAndFactories()
    {
        DensityFunction left = DensityFunctions.Constant(2.0f);
        DensityFunction right = DensityFunctions.Constant(3.0f);
        var node = new BinaryFunction(BinaryFunction.Type.SUB, left, right);

        Require(ReferenceEquals(node.RewriteChildren(function => function), node),
            "BinaryFunction.rewriteChildren must retain identity when neither child changes.");

        DensityFunction rewritten = node.RewriteChildren(_ => DensityFunctions.Constant(7.0f));
        Require(rewritten is BinaryFunction rewrittenNode &&
            rewrittenNode.Operation == BinaryFunction.Type.SUB &&
            Sample(rewrittenNode) == 0.0f,
            "BinaryFunction.rewriteChildren must replace both children and preserve the operation.");

        Require(Sample(DensityFunctions.Add(left, right)) == 5.0f &&
            Sample(DensityFunctions.Sub(left, right)) == -1.0f &&
            Sample(DensityFunctions.Mul(left, right)) == 6.0f &&
            Sample(DensityFunctions.Div(left, right)) == 2.0f,
            "DensityFunctions factories must create working binary nodes.");
    }

    private static float Sample(DensityFunction function) =>
        function.CompileSampler(Context)
            .SampleValue(SamplerContext.EmptyUncached, 0, 0, 0);

    private static DensitySampler Compile(DensityFunction function) =>
        function.CompileSampler(Context);

    private sealed record RangeFixture(Interval ValueRange, int Axes) : DensityFunction
    {
        public DensitySampler CompileSampler(DensityFunction.CompileContext context) =>
            new ConstantFunction.Sampler(ValueRange.Min());

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

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
