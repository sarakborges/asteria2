using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.DensityFunctions.Operations;

internal static class UnaryFunctionChecks
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

    public static void Run()
    {
        CheckTypeAndIdOrder();
        CheckScalarOperations();
        CheckRangesAndDomainAxes();
        CheckVolumeSampling();
        CheckRewrite();
    }

    private static void CheckTypeAndIdOrder()
    {
        UnaryFunction.Type[] expected =
        {
            UnaryFunction.Type.ABS,
            UnaryFunction.Type.SQUARE,
            UnaryFunction.Type.CUBE,
            UnaryFunction.Type.SQRT,
            UnaryFunction.Type.HALF_NEGATIVE,
            UnaryFunction.Type.QUARTER_NEGATIVE,
            UnaryFunction.Type.RECIPROCAL,
            UnaryFunction.Type.NEGATE,
            UnaryFunction.Type.SQUEEZE,
            UnaryFunction.Type.LOG,
            UnaryFunction.Type.SIGN
        };
        string[] ids =
        {
            "abs", "square", "cube", "sqrt", "half_negative", "quarter_negative",
            "reciprocal", "negate", "squeeze", "log", "sign"
        };
        Require(Enum.GetValues<UnaryFunction.Type>().SequenceEqual(expected),
            "UnaryFunction.Type must preserve the exact Minecraft 26.3 enum order.");
        for (int index = 0; index < expected.Length; index++)
            Require(UnaryFunction.GetId(expected[index]) == ids[index],
                "UnaryFunction.Type ids must match the 26.3 density-function registry names.");
    }

    private static void CheckScalarOperations()
    {
        Require(Sample(DensityFunctions.Abs(DensityFunctions.Constant(-2.0f))) == 2.0f,
            "abs must return the non-negative magnitude.");
        Require(Sample(DensityFunctions.Square(DensityFunctions.Constant(-3.0f))) == 9.0f,
            "square must multiply the child by itself.");
        Require(Sample(DensityFunctions.Cube(DensityFunctions.Constant(-2.0f))) == -8.0f,
            "cube must preserve the sign of an odd power.");
        Require(Sample(DensityFunctions.Sqrt(DensityFunctions.Constant(9.0f))) == 3.0f,
            "sqrt must return the square root.");
        Require(Sample(DensityFunctions.HalfNegative(DensityFunctions.Constant(-4.0f))) == -2.0f &&
            Sample(DensityFunctions.HalfNegative(DensityFunctions.Constant(4.0f))) == 4.0f,
            "half_negative must halve only values below zero.");
        Require(Sample(DensityFunctions.QuarterNegative(DensityFunctions.Constant(-4.0f))) == -1.0f &&
            Sample(DensityFunctions.QuarterNegative(DensityFunctions.Constant(4.0f))) == 4.0f,
            "quarter_negative must quarter only values below zero.");
        Require(Sample(DensityFunctions.Reciprocal(DensityFunctions.Constant(4.0f))) == 0.25f,
            "reciprocal must return one divided by the input.");
        Require(Sample(DensityFunctions.Negate(DensityFunctions.Constant(4.0f))) == -4.0f,
            "negate must invert the input sign.");
        Require(Math.Abs(Sample(DensityFunctions.Squeeze(DensityFunctions.Constant(2.0f))) - 0.45833334f) < 0.000001f &&
            Math.Abs(Sample(DensityFunctions.Squeeze(DensityFunctions.Constant(-2.0f))) + 0.45833334f) < 0.000001f,
            "squeeze must clamp to [-1,1] before applying its odd cubic.");
        Require(Math.Abs(Sample(DensityFunctions.Log(DensityFunctions.Constant(4.0f))) - (float)Math.Log(4.0)) < 0.000001f,
            "log must compute the natural logarithm.");
        Require(Sample(DensityFunctions.Sign(DensityFunctions.Constant(-4.0f))) == -1.0f &&
            Sample(DensityFunctions.Sign(DensityFunctions.Constant(0.0f))) == 0.0f &&
            Sample(DensityFunctions.Sign(DensityFunctions.Constant(4.0f))) == 1.0f,
            "sign must return -1, 0 or 1.");

        DensityFunction receiver = DensityFunctions.Constant(-4.0f);
        Require(Sample(receiver.Abs()) == 4.0f &&
            Sample(receiver.Square()) == 16.0f &&
            Sample(receiver.Cube()) == -64.0f &&
            Sample(receiver.HalfNegative()) == -2.0f &&
            Sample(receiver.QuarterNegative()) == -1.0f &&
            Sample(receiver.Negate()) == 4.0f,
            "DensityFunction default unary methods must delegate to the matching DensityFunctions factories.");
    }

    private static void CheckRangesAndDomainAxes()
    {
        Require(DensityFunctions.Abs(DensityFunctions.Constant(-2.0f)).Range()
                .Equals(Interval.OfExact(2.0f)) &&
            DensityFunctions.Square(DensityFunctions.Constant(-3.0f)).Range()
                .Equals(Interval.OfExact(9.0f)) &&
            DensityFunctions.Cube(DensityFunctions.Constant(-2.0f)).Range()
                .Equals(Interval.OfExact(-8.0f)) &&
            DensityFunctions.Sqrt(DensityFunctions.Constant(9.0f)).Range()
                .Equals(Interval.OfExact(3.0f)),
            "UnaryFunction range must return exact transformed singleton intervals.");

        DensityFunction rangeInput = new RangeFixture(Interval.Of(-3.0f, 2.0f));
        Require(DensityFunctions.Abs(rangeInput).Range().Equals(Interval.Of(0.0f, 3.0f)) &&
            DensityFunctions.Square(rangeInput).Range().Equals(Interval.Of(0.0f, 9.0f)) &&
            DensityFunctions.Cube(rangeInput).Range().Equals(Interval.Of(-27.0f, 8.0f)) &&
            DensityFunctions.HalfNegative(rangeInput).Range().Equals(Interval.Of(-1.5f, 2.0f)) &&
            DensityFunctions.QuarterNegative(rangeInput).Range().Equals(Interval.Of(-0.75f, 2.0f)) &&
            DensityFunctions.Negate(rangeInput).Range().Equals(Interval.Of(-2.0f, 3.0f)) &&
            DensityFunctions.Squeeze(rangeInput).Range().Equals(
                Interval.Encapsulating(-0.45833334f, 0.45833334f)) &&
            DensityFunctions.Sign(rangeInput).Range().Equals(Interval.Of(-1.0f, 1.0f)),
            "UnaryFunction ranges must transform the input interval using vanilla interval operations.");

        Require(DensityFunctions.Reciprocal(rangeInput).Range().Equals(Interval.INFINITE) &&
            DensityFunctions.Log(DensityFunctions.Constant(4.0f)).Range().Equals(Interval.OfExact((float)Math.Log(4.0))),
            "Reciprocal and log must use the dedicated interval range helpers.");

        var axisFixture = new AxisFixture(DensityFunction.AXIS_X | DensityFunction.AXIS_Z);
        Require(DensityFunctions.Negate(axisFixture).DomainAxes() ==
                (DensityFunction.AXIS_X | DensityFunction.AXIS_Z),
            "UnaryFunction.domainAxes must inherit the child domain-axis mask.");
    }

    private static void CheckVolumeSampling()
    {
        DensityFunction function = DensityFunctions.Squeeze(
            DensityFunctions.Constant(2.0f));
        DensitySampler sampler = function.CompileSampler(Context);
        var volume = new DensityVolume(2, 2, 2, -4, 50, 9);
        DensityBuffer buffer = DensityBuffer.CreateUnpooled(volume.Size());

        sampler.SampleVolume(SamplerContext.EmptyUncached, buffer, volume);
        Require(Enumerable.Range(0, buffer.Size()).All(index =>
                Math.Abs(buffer.Get(index) - 0.45833334f) < 0.000001f),
            "Unary sampler volume methods must transform each child buffer entry.");

        DensityFunction nested = DensityFunctions.Abs(
            DensityFunctions.Negate(DensityFunctions.Constant(-2.0f)));
        Require(Sample(nested) == 2.0f,
            "Nested unary nodes must compile their child sampler tree before applying the parent mapping.");
    }

    private static void CheckRewrite()
    {
        DensityFunction input = DensityFunctions.Constant(-5.0f);
        var node = new UnaryFunction(UnaryFunction.Type.ABS, input);
        DensityFunction same = node.RewriteChildren(function => function);
        Require(ReferenceEquals(same, node),
            "UnaryFunction.rewriteChildren must return the same node when the child identity is unchanged.");

        DensityFunction rewritten = node.RewriteChildren(_ => DensityFunctions.Constant(7.0f));
        Require(rewritten is UnaryFunction rewrittenNode &&
            rewrittenNode.Operation == UnaryFunction.Type.ABS &&
            Sample(rewrittenNode) == 7.0f,
            "UnaryFunction.rewriteChildren must preserve its operation while replacing a rewritten child.");
    }

    private sealed record RangeFixture(Interval RangeValue) : DensityFunction
    {
        public DensitySampler CompileSampler(DensityFunction.CompileContext context) =>
            throw new NotSupportedException();

        public DensityFunction RewriteChildren(DfRewriteRule rule) => this;

        public Interval Range() => RangeValue;

        public int DomainAxes() => DensityFunction.AXIS_Y;
    }

    private sealed record AxisFixture(int Axes) : DensityFunction
    {
        public DensitySampler CompileSampler(DensityFunction.CompileContext context) =>
            throw new NotSupportedException();

        public DensityFunction RewriteChildren(DfRewriteRule rule) => this;

        public Interval Range() => Interval.INFINITE;

        public int DomainAxes() => Axes;
    }

    private static float Sample(DensityFunction function) =>
        function.CompileSampler(Context)
            .SampleValue(SamplerContext.EmptyUncached, 0, 0, 0);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
