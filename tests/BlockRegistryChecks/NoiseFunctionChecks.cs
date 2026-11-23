using Asteria.Content.Registries;
using Asteria.Util;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.Synth;

internal static class NoiseFunctionChecks
{
    private sealed class RecordingNoise : Noise
    {
        public List<(double X, double Y, double Z)> Samples { get; } = new();

        public Interval GetRange() => Interval.Of(-10000.0f, 10000.0f);

        public float Get(double x, double y) => Get(x, y, 0.0);

        public float Get(double x, double y, double z)
        {
            Samples.Add((x, y, z));
            return (float)(x + 10.0 * y + 100.0 * z);
        }
    }

    private sealed class CompileContext(Noise noise) : DensityFunction.CompileContext
    {
        public int NoiseRequests { get; private set; }

        public Noise CreateNoiseSampler(Holder<NormalNoise> parameters)
        {
            NoiseRequests++;
            return noise;
        }

        public IRandomSource CreateRandom(Asteria.Content.Identifiers.ResourceLocation seed) =>
            throw new InvalidOperationException();

#pragma warning disable CS0618
        public IRandomSource CreateEndIslandRandom() =>
            throw new InvalidOperationException();
#pragma warning restore CS0618
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

    private sealed class ConstantSampler(float value) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume) =>
            outputBuffer.Fill(value);

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            value;
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

    private static readonly Holder<NormalNoise> NoiseHolder =
        Holder<NormalNoise>.Direct(NormalNoise.CreateBuilder().Build());

    public static void Run()
    {
        CheckNoShiftSamplerAndFactories();
        CheckShiftedXzSampling();
        CheckShiftedXyzSampling();
        CheckRangeAxesAndRewrite();
    }

    private static void CheckNoShiftSamplerAndFactories()
    {
        var noise = new RecordingNoise();
        var context = new CompileContext(noise);
        DensityFunction function = DensityFunctions.Noise(NoiseHolder, 2.0, 3.0);

        Require(function is NoiseFunction noiseFunction &&
            noiseFunction.XzScale == 2.0 &&
            noiseFunction.YScale == 3.0 &&
            noiseFunction.ShiftX.Equals(DensityFunctions.Zero()) &&
            noiseFunction.ShiftY.Equals(DensityFunctions.Zero()) &&
            noiseFunction.ShiftZ.Equals(DensityFunctions.Zero()),
            "DensityFunctions.noise must construct a NoiseFunction with vanilla zero shift defaults.");

        DensitySampler sampler = function.CompileSampler(context);
        Require(sampler is NoiseFunction.Sampler && context.NoiseRequests == 1,
            "NoiseFunction must resolve NormalNoise once and use the no-shift sampler when all shift children are zero.");

        Require(sampler.SampleValue(SamplerContext.EmptyUncached, 2, 4, 6) == 1324.0f,
            "NoiseFunction.Sampler must scale X/Z by xzScale and Y by yScale before sampling.");

        var volume = new DensityVolume(2, 2, 2, -1, 10, 20);
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        sampler.SampleVolume(SamplerContext.EmptyUncached, output, volume);
        Require(noise.Samples.Count == 9 &&
            output.Get(0) == 3998.0f,
            "NoiseFunction.Sampler.sampleVolume must clear the output buffer and accumulate the scaled Noise volume.");

        Require(DensityFunctions.Noise(NoiseHolder) is NoiseFunction defaultScale &&
            defaultScale.XzScale == 1.0 && defaultScale.YScale == 1.0 &&
            DensityFunctions.Noise(NoiseHolder, 4.0) is NoiseFunction yOnlyScale &&
            yOnlyScale.XzScale == 1.0 && yOnlyScale.YScale == 4.0,
            "NoiseFunction factories must preserve the vanilla default and Y-only scale overloads.");
    }

    private static void CheckShiftedXzSampling()
    {
        var noise = new RecordingNoise();
        var context = new CompileContext(noise);
        DensityFunction function = DensityFunctions.ShiftedNoise2d(
            DensityFunctions.Constant(3.0f),
            DensityFunctions.Constant(-5.0f),
            2.0,
            NoiseHolder);
        DensitySampler sampler = function.CompileSampler(context);

        Require(sampler is NoiseFunction.ShiftedXzSampler,
            "NoiseFunction must select ShiftedXzSampler when X/Z shifts exist but Y shift is zero.");
        Require(sampler.SampleValue(SamplerContext.EmptyUncached, -2, 10, 20) == 3799.0f,
            "ShiftedXzSampler must add the X/Z shift samples after horizontal scaling and leave Y unshifted.");

        var volume = new DensityVolume(2, 2, 2, -1, 10, 20);
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        var arena = new CountingArena();
        SamplerContext sampleContext = SamplerContext.CreateBuilder().UseBufferArena(arena).Build();
        sampler.SampleVolume(sampleContext, output, volume);

        Require(output.Get(0) == 3899.0f && arena.Acquired == 1 && arena.Released == 1,
            "ShiftedXzSampler volume path must apply both shifts and release its Z scratch buffer.");
    }

    private static void CheckShiftedXyzSampling()
    {
        var noise = new RecordingNoise();
        var context = new CompileContext(noise);
        var function = new NoiseFunction(
            NoiseHolder,
            XzScale: 2.0,
            YScale: 3.0,
            ShiftX: DensityFunctions.Constant(3.0f),
            ShiftY: DensityFunctions.Constant(7.0f),
            ShiftZ: DensityFunctions.Constant(-5.0f));
        DensitySampler sampler = function.CompileSampler(context);

        Require(sampler is NoiseFunction.ShiftedXyzSampler,
            "NoiseFunction must select ShiftedXyzSampler when a Y shift is present.");
        Require(sampler.SampleValue(SamplerContext.EmptyUncached, -2, 10, 20) == 3869.0f,
            "ShiftedXyzSampler must add X/Y/Z shifts after their axis-specific coordinate scaling.");

        var volume = new DensityVolume(2, 2, 2, -1, 10, 20);
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        var arena = new CountingArena();
        SamplerContext sampleContext = SamplerContext.CreateBuilder().UseBufferArena(arena).Build();
        sampler.SampleVolume(sampleContext, output, volume);

        Require(output.Get(0) == 3969.0f && arena.Acquired == 2 && arena.Released == 2,
            "ShiftedXyzSampler volume path must apply all three shifts and release Y/Z scratch buffers.");
    }

    private static void CheckRangeAxesAndRewrite()
    {
        var noise = new RecordingNoise();
        var context = new CompileContext(noise);
        var function = new NoiseFunction(
            NoiseHolder,
            XzScale: 0.0,
            YScale: 0.0,
            ShiftX: DensityFunctions.Zero(),
            ShiftY: DensityFunctions.Zero(),
            ShiftZ: DensityFunctions.Zero());

        Require(function.Range().Equals(NoiseHolder.Value.GetRange()) &&
            function.DomainAxes() == DensityFunction.NO_AXES,
            "NoiseFunction.range must match the holder value and zero scales must remove corresponding coordinate axes.");

        var horizontalOnly = new NoiseFunction(
            NoiseHolder, 2.0, 0.0,
            DensityFunctions.Zero(), DensityFunctions.Zero(), DensityFunctions.Zero());
        var verticalOnly = new NoiseFunction(
            NoiseHolder, 0.0, 3.0,
            DensityFunctions.Zero(), DensityFunctions.Zero(), DensityFunctions.Zero());
        Require(horizontalOnly.DomainAxes() == (DensityFunction.AXIS_X | DensityFunction.AXIS_Z) &&
            verticalOnly.DomainAxes() == DensityFunction.AXIS_Y,
            "NoiseFunction.domainAxes must preserve only coordinate axes whose scale is non-zero.");

        var shiftedAxes = new NoiseFunction(
            NoiseHolder, 0.0, 0.0,
            new Fixture(Interval.INFINITE, DensityFunction.AXIS_X, new ConstantSampler(1.0f)),
            new Fixture(Interval.INFINITE, DensityFunction.AXIS_Y, new ConstantSampler(1.0f)),
            new Fixture(Interval.INFINITE, DensityFunction.AXIS_Z, new ConstantSampler(1.0f)));
        Require(shiftedAxes.DomainAxes() == DensityFunction.ALL_AXES,
            "NoiseFunction.domainAxes must union all shift-child axes even when the base scales are zero.");

        Require(ReferenceEquals(function.RewriteChildren(child => child), function),
            "NoiseFunction.rewriteChildren must preserve identity when all shift children are unchanged.");

        DensityFunction rewritten = function.RewriteChildren(_ => DensityFunctions.Constant(2.0f));
        Require(rewritten is NoiseFunction newNoise &&
            newNoise.XzScale == function.XzScale &&
            newNoise.YScale == function.YScale &&
            newNoise.ShiftX.Equals(DensityFunctions.Constant(2.0f)) &&
            newNoise.ShiftY.Equals(DensityFunctions.Constant(2.0f)) &&
            newNoise.ShiftZ.Equals(DensityFunctions.Constant(2.0f)),
            "NoiseFunction.rewriteChildren must replace all three shift children and retain holder/scales.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
