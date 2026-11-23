using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Operations;

internal static class ClampFunctionChecks
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

    public static void Run()
    {
        CheckScalarClampAndNaN();
        CheckVolumeClamp();
        CheckRangeAndDomainAxes();
        CheckRewrite();
    }

    private static void CheckScalarClampAndNaN()
    {
        Require(Sample(DensityFunctions.Clamp(DensityFunctions.Constant(-2.0f), -1.0f, 3.0f)) == -1.0f &&
            Sample(DensityFunctions.Clamp(DensityFunctions.Constant(2.0f), -1.0f, 3.0f)) == 2.0f &&
            Sample(DensityFunctions.Clamp(DensityFunctions.Constant(5.0f), -1.0f, 3.0f)) == 3.0f,
            "ClampFunction must return min below range, unchanged input in range and max above range.");

        Require(float.IsNaN(Sample(DensityFunctions.Clamp(
                    new Fixture(Interval.INFINITE, DensityFunction.AXIS_X,
                        new CountingSampler(float.NaN)),
                    -2.0f,
                    2.0f))),
            "Mth.clamp(float) must preserve NaN using the vanilla comparison followed by Math.min.");

        Require(Sample(DensityFunctions.Clamp(DensityFunctions.Constant(0.0f), 5.0f, 2.0f)) == 5.0f,
            "Mth.clamp(float) must preserve the vanilla branch order when runtime bounds are inverted.");
        Require(float.IsNaN(Sample(DensityFunctions.Clamp(DensityFunctions.Constant(float.NaN), 5.0f, 2.0f))),
            "Mth.clamp(float) must preserve NaN even when runtime bounds are inverted.");

        DensityFunction receiver = DensityFunctions.Constant(9.0f);
        Require(Sample(receiver.Clamp(1.0f, 4.0f)) == 4.0f,
            "DensityFunction.clamp must forward to DensityFunctions.clamp.");
    }

    private static void CheckVolumeClamp()
    {
        var input = new Fixture(
            Interval.Of(-5.0f, 5.0f),
            DensityFunction.AXIS_Y,
            new CountingSampler(7.0f));
        DensityFunction function = DensityFunctions.Clamp(input, -2.0f, 3.0f);
        var volume = new DensityVolume(2, 2, 2, -1, 10, 20);
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());

        function.CompileSampler(Context).SampleVolume(SamplerContext.EmptyUncached, output, volume);
        Require(Enumerable.Range(0, output.Size()).All(index => output.Get(index) == 3.0f) &&
            input.Sampler.VolumeCalls == 1,
            "ClampFunction.Sampler.sampleVolume must map Mth.clamp over exactly one sampled input buffer.");
    }

    private static void CheckRangeAndDomainAxes()
    {
        DensityFunction crossing = new Fixture(
            Interval.Of(-4.0f, 6.0f),
            DensityFunction.AXIS_X | DensityFunction.AXIS_Z,
            new CountingSampler(0.0f));
        DensityFunction clamped = DensityFunctions.Clamp(crossing, -2.0f, 3.0f);
        Require(clamped.Range().Equals(Interval.Of(-2.0f, 3.0f)) &&
            clamped.DomainAxes() == (DensityFunction.AXIS_X | DensityFunction.AXIS_Z),
            "ClampFunction.range must use Interval.clamp and domainAxes must inherit the child's axes.");

        Require(DensityFunctions.Clamp(
                    new Fixture(Interval.Of(4.0f, 9.0f), 0, new CountingSampler(5.0f)),
                    -2.0f,
                    3.0f).Range().Equals(Interval.OfExact(3.0f)) &&
            DensityFunctions.Clamp(
                    new Fixture(Interval.Of(-9.0f, -4.0f), 0, new CountingSampler(-5.0f)),
                    -2.0f,
                    3.0f).Range().Equals(Interval.OfExact(-2.0f)),
            "ClampFunction.range must collapse intervals fully above or below the clamped range to the corresponding boundary.");
    }

    private static void CheckRewrite()
    {
        DensityFunction input = DensityFunctions.Constant(5.0f);
        var function = new ClampFunction(input, -2.0f, 3.0f);
        Require(ReferenceEquals(function.RewriteChildren(child => child), function),
            "ClampFunction.rewriteChildren must preserve node identity when the child is unchanged.");

        DensityFunction rewritten = function.RewriteChildren(_ => DensityFunctions.Constant(-8.0f));
        Require(rewritten is ClampFunction clamp &&
            clamp.Min == -2.0f && clamp.Max == 3.0f &&
            Sample(clamp) == -2.0f,
            "ClampFunction.rewriteChildren must replace only the child and retain both clamp bounds.");
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
