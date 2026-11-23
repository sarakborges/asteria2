using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Util;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.Synth;

internal static class DensityFunctionChecks
{
    private sealed class UnusedCompileContext : DensityFunction.CompileContext
    {
        public Noise CreateNoiseSampler(Holder<NormalNoise> parameters) =>
            throw new InvalidOperationException("Constant functions must not resolve noise.");

        public IRandomSource CreateRandom(ResourceLocation seed) =>
            throw new InvalidOperationException("Constant functions must not resolve random streams.");

#pragma warning disable CS0618
        public IRandomSource CreateEndIslandRandom() =>
            throw new InvalidOperationException("Constant functions must not resolve End Island random streams.");
#pragma warning restore CS0618
    }

    public static void Run()
    {
        CheckAxisDomainContract();
        CheckConstantNode();
        CheckRewriteSequence();
        CheckCompileContextSurface();
    }

    private static void CheckAxisDomainContract()
    {
        Require(DensityFunction.AXIS_X == 1 &&
            DensityFunction.AXIS_Y == 2 &&
            DensityFunction.AXIS_Z == 4 &&
            DensityFunction.NO_AXES == 0 &&
            DensityFunction.ALL_AXES == 7,
            "DensityFunction axis constants must preserve Minecraft's bit-mask layout.");

        Require(DensityFunction.AxesFrom(Direction.Axis.X) == DensityFunction.AXIS_X &&
            DensityFunction.AxesFrom(Direction.Axis.Y) == DensityFunction.AXIS_Y &&
            DensityFunction.AxesFrom(Direction.Axis.Z) == DensityFunction.AXIS_Z,
            "DensityFunction.axesFrom must map each Direction.Axis singleton to its vanilla bit.");
    }

    private static void CheckConstantNode()
    {
        DensityFunction zero = DensityFunctions.Zero();
        Require(ReferenceEquals(zero, DensityFunctions.Zero()) &&
            zero is ConstantFunction zeroConstant &&
            zeroConstant.Value == 0.0f,
            "DensityFunctions.zero must return the shared zero ConstantFunction singleton.");

        DensityFunction first = DensityFunctions.Constant(2.75f);
        DensityFunction second = DensityFunctions.Constant(2.75f);
        Require(first is ConstantFunction constant &&
            constant.Value == 2.75f &&
            first.Equals(second) &&
            !ReferenceEquals(first, second),
            "DensityFunctions.constant must allocate a value-equal ConstantFunction for each call.");

        Require(first.Range().Equals(Interval.OfExact(2.75f)) &&
            first.DomainAxes() == DensityFunction.NO_AXES,
            "ConstantFunction.range and domainAxes must return its exact value interval and no coordinate axes.");

        var context = new UnusedCompileContext();
        DensitySampler sampler = first.CompileSampler(context);
        Require(sampler.SampleValue(SamplerContext.EmptyUncached, -100, 64, 200) == 2.75f &&
            sampler.SampleValue(SamplerContext.EmptyUncached, 900, -4, -200) == 2.75f,
            "ConstantFunction.Sampler must ignore sampled world coordinates.");

        var buffer = DensityBuffer.CreateUnpooled(10);
        buffer.Fill(99.0f);
        var volume = new DensityVolume(2, 2, 2, -1, 10, 20);
        sampler.SampleVolume(SamplerContext.EmptyUncached, buffer, volume);
        Require(Enumerable.Range(0, volume.Size()).All(index => buffer.Get(index) == 2.75f) &&
            buffer.Get(8) == 99.0f && buffer.Get(9) == 99.0f,
            "ConstantFunction.Sampler.sampleVolume must fill exactly the active buffer size, preserving unused capacity.");

        int rewrites = 0;
        DensityFunction unchanged = first.RewriteChildren(function =>
        {
            rewrites++;
            return DensityFunctions.Zero();
        });
        Require(ReferenceEquals(unchanged, first) && rewrites == 0,
            "ConstantFunction.rewriteChildren must return itself without applying the rewrite rule.");
    }

    private static void CheckRewriteSequence()
    {
        DensityFunction original = DensityFunctions.Constant(3.0f);
        var seen = new List<float>();

        DfRewriteRule sequence = DfRewriteRules.Sequence(
            function =>
            {
                seen.Add(((ConstantFunction)function).Value);
                return DensityFunctions.Constant(((ConstantFunction)function).Value + 1.0f);
            },
            function =>
            {
                seen.Add(((ConstantFunction)function).Value);
                return DensityFunctions.Constant(((ConstantFunction)function).Value * 2.0f);
            },
            function =>
            {
                seen.Add(((ConstantFunction)function).Value);
                return function;
            });

        DensityFunction result = sequence(original);
        Require(seen.SequenceEqual(new[] { 3.0f, 4.0f, 8.0f }) &&
            ((ConstantFunction)result).Value == 8.0f,
            "DfRewriteRules.sequence must apply rules in declaration order and pass each result to the next rule.");

        Require(ReferenceEquals(DfRewriteRules.Sequence()(original), original),
            "An empty DfRewriteRules.sequence must preserve the original density function.");
    }

    private static void CheckCompileContextSurface()
    {
        Type contextType = typeof(DensityFunction.CompileContext);
        Require(contextType.IsInterface &&
            contextType.GetMethod("CreateNoiseSampler") is not null &&
            contextType.GetMethod("CreateRandom") is not null &&
            contextType.GetMethod("CreateEndIslandRandom") is not null,
            "DensityFunction.CompileContext must expose noise, positional seed and End Island random dependencies.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
