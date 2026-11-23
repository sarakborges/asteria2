using Asteria.Content.Registries;
using Asteria.Util;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.Synth;

internal static class ShiftNoiseFunctionChecks
{
    private sealed class RecordingNoise : Noise
    {
        public List<(double X, double Y, double Z)> Samples { get; } = new();

        public Interval GetRange() => Interval.Of(-2.0f, 3.0f);
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
        CheckShiftA();
        CheckShift();
        CheckShiftBPointAndTransposedVolume();
        CheckRangeAxesAndRewrite();
    }

    private static void CheckShiftA()
    {
        var noise = new RecordingNoise();
        var compileContext = new CompileContext(noise);
        DensityFunction function = DensityFunctions.ShiftA(NoiseHolder);
        DensitySampler sampler = function.CompileSampler(compileContext);

        Require(sampler is BinaryFunction.ConstMulSampler &&
            compileContext.NoiseRequests == 1 &&
            sampler.SampleValue(SamplerContext.EmptyUncached, 8, 16, 12) == 1208.0f,
            "ShiftA must sample X/Z at quarter scale, use Y=0, and multiply the result by four.");

        var volume = new DensityVolume(2, 2, 2, 10, 30, 50);
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        sampler.SampleVolume(SamplerContext.EmptyUncached, output, volume);
        Require(output.Get(0) == 1208.0f,
            "ShiftA must use the same 2D noise coordinates for volume sampling.");
    }

    private static void CheckShift()
    {
        var noise = new RecordingNoise();
        var sampler = DensityFunctions.Shift(NoiseHolder).CompileSampler(new CompileContext(noise));
        Require(sampler is BinaryFunction.ConstMulSampler &&
            sampler.SampleValue(SamplerContext.EmptyUncached, 8, 16, 12) == 1368.0f,
            "Shift must sample all three coordinates at quarter scale and multiply the result by four.");

        var volume = new DensityVolume(2, 2, 2, 10, 30, 50);
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        sampler.SampleVolume(SamplerContext.EmptyUncached, output, volume);
        Require(output.Get(0) == 1368.0f,
            "Shift volume sampling must preserve the full XYZ coordinate contribution.");
    }

    private static void CheckShiftBPointAndTransposedVolume()
    {
        var noise = new RecordingNoise();
        var context = new CompileContext(noise);
        DensitySampler sampler = DensityFunctions.ShiftB(NoiseHolder).CompileSampler(context);

        Require(sampler is ShiftNoiseFunction.ShiftB.Sampler &&
            sampler.SampleValue(SamplerContext.EmptyUncached, 8, 16, 12) == 92.0f,
            "ShiftB point sampling must swap block Z/X into noise X/Y, set noise Z to zero, and multiply by four.");

        var volume = new DensityVolume(
            2, 3, 2,
            10, 30, 50,
            2, 4, 3);
        var arena = new CountingArena();
        SamplerContext sampleContext = SamplerContext.CreateBuilder().UseBufferArena(arena).Build();
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        sampler.SampleVolume(sampleContext, output, volume);

        for (int z = 0; z < volume.SizeZ; z++)
        {
            for (int x = 0; x < volume.SizeX; x++)
            {
                float expected = volume.BlockZ(z) + 10.0f * volume.BlockX(x);
                for (int y = 0; y < volume.SizeY; y++)
                {
                    int index = volume.IndexUnchecked(x, y, z);
                    Require(output.Get(index) == expected,
                        "ShiftB must transpose block steps and replicate each sampled value over all original Y samples.");
                }
            }
        }

        Require(arena.Acquired == 1 && arena.Released == 1,
            "ShiftB must release its single transposed density buffer after volume sampling.");
    }

    private static void CheckRangeAxesAndRewrite()
    {
        Interval scaledRange = Interval.Mul(NoiseHolder.Value.GetRange(), Interval.OfExact(4.0f));
        DensityFunction shiftA = DensityFunctions.ShiftA(NoiseHolder);
        DensityFunction shiftB = DensityFunctions.ShiftB(NoiseHolder);
        DensityFunction shift = DensityFunctions.Shift(NoiseHolder);

        Require(shiftA.Range().Equals(scaledRange) &&
            shiftB.Range().Equals(scaledRange) &&
            shift.Range().Equals(scaledRange),
            "All ShiftNoiseFunction variants must multiply the NormalNoise range by exactly four.");

        Require(shiftA.DomainAxes() == (DensityFunction.AXIS_X | DensityFunction.AXIS_Z) &&
            shiftB.DomainAxes() == (DensityFunction.AXIS_X | DensityFunction.AXIS_Z) &&
            shift.DomainAxes() == DensityFunction.ALL_AXES,
            "ShiftA/ShiftB must depend on X/Z while Shift depends on X/Y/Z.");

        Require(!ReferenceEquals(shiftA.RewriteChildren(function => function), shiftA) &&
            shiftA.RewriteChildren(function => function).Equals(shiftA) &&
            !ReferenceEquals(shiftB.RewriteChildren(function => function), shiftB) &&
            shiftB.RewriteChildren(function => function).Equals(shiftB) &&
            !ReferenceEquals(shift.RewriteChildren(function => function), shift) &&
            shift.RewriteChildren(function => function).Equals(shift),
            "ShiftNoiseFunction rewriteChildren must return a fresh equal record instance, matching vanilla.");

        Require(shiftA is ShiftNoiseFunction.ShiftA &&
            shiftB is ShiftNoiseFunction.ShiftB &&
            shift is ShiftNoiseFunction.Shift,
            "DensityFunctions.shiftA/shiftB/shift must construct their corresponding reference types.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
