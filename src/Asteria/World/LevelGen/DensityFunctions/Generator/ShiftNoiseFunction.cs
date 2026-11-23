using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions.Operations;
using Asteria.World.LevelGen.Synth;

namespace Asteria.World.LevelGen.DensityFunctions.Generator;

/// <summary>
/// Horizontal/3D shift functions from Minecraft Java 26.3.
/// </summary>
public interface ShiftNoiseFunction : DensityFunction
{
    const double COORDINATE_FACTOR = 0.25;
    const float VALUE_FACTOR = 4.0f;

    Holder<NormalNoise> OffsetNoise { get; }

    Interval DensityFunction.Range() =>
        Interval.Mul(OffsetNoise.Value.GetRange(), Interval.OfExact(VALUE_FACTOR));

    public sealed record ShiftA(Holder<NormalNoise> OffsetNoise) : ShiftNoiseFunction
    {
        public static MapCodec<ShiftA> CODEC { get; } =
            NormalNoise.CODEC.FieldOf("noise")
                .XMap<ShiftA>(
                    static noise => new ShiftA(noise),
                    static function => function.OffsetNoise);

        public DensitySampler CompileSampler(DensityFunction.CompileContext context)
        {
            Noise noise = context.CreateNoiseSampler(OffsetNoise);
            return new BinaryFunction.ConstMulSampler(
                new NoiseFunction.Sampler(noise, COORDINATE_FACTOR, 0.0),
                VALUE_FACTOR);
        }

        public DensityFunction RewriteChildren(DfRewriteRule rule) =>
            new ShiftA(OffsetNoise);

        public int DomainAxes() =>
            DensityFunction.AXIS_X | DensityFunction.AXIS_Z;
    }

    public sealed record ShiftB(Holder<NormalNoise> OffsetNoise) : ShiftNoiseFunction
    {
        public static MapCodec<ShiftB> CODEC { get; } =
            NormalNoise.CODEC.FieldOf("noise")
                .XMap<ShiftB>(
                    static noise => new ShiftB(noise),
                    static function => function.OffsetNoise);

        public DensitySampler CompileSampler(DensityFunction.CompileContext context)
        {
            Noise noise = context.CreateNoiseSampler(OffsetNoise);
            return new Sampler(noise);
        }

        public DensityFunction RewriteChildren(DfRewriteRule rule) =>
            new ShiftB(OffsetNoise);

        public int DomainAxes() =>
            DensityFunction.AXIS_X | DensityFunction.AXIS_Z;

        public sealed record Sampler(Noise Noise) : DensitySampler
        {
            public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
            {
                var transposedVolume = new DensityVolume(
                    volume.SizeZ,
                    volume.SizeX,
                    1,
                    volume.MinBlockZ,
                    volume.MinBlockX,
                    0,
                    volume.StepBlockZ,
                    volume.StepBlockX,
                    1);

                using ScopedDensityBuffer transposedBuffer = context.AcquireBuffer(transposedVolume);
                transposedBuffer.Fill(0.0f);
                Noise.AddToVolume(
                    transposedBuffer,
                    transposedVolume,
                    COORDINATE_FACTOR,
                    COORDINATE_FACTOR,
                    VALUE_FACTOR);

                for (int z = 0; z < volume.SizeZ; z++)
                {
                    for (int x = 0; x < volume.SizeX; x++)
                    {
                        float value = transposedBuffer.Get(
                            transposedVolume.IndexUnchecked(z, x, 0));
                        outputBuffer.SetRange(
                            volume.IndexUnchecked(x, 0, z),
                            volume.SizeY,
                            value);
                    }
                }
            }

            public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
                Noise.Get(
                    blockZ * COORDINATE_FACTOR,
                    blockX * COORDINATE_FACTOR,
                    0.0) * VALUE_FACTOR;
        }
    }

    public sealed record Shift(Holder<NormalNoise> OffsetNoise) : ShiftNoiseFunction
    {
        public static MapCodec<Shift> CODEC { get; } =
            NormalNoise.CODEC.FieldOf("noise")
                .XMap<Shift>(
                    static noise => new Shift(noise),
                    static function => function.OffsetNoise);

        public DensitySampler CompileSampler(DensityFunction.CompileContext context)
        {
            Noise noise = context.CreateNoiseSampler(OffsetNoise);
            return new BinaryFunction.ConstMulSampler(
                new NoiseFunction.Sampler(noise, COORDINATE_FACTOR, COORDINATE_FACTOR),
                VALUE_FACTOR);
        }

        public DensityFunction RewriteChildren(DfRewriteRule rule) =>
            new Shift(OffsetNoise);

        public int DomainAxes() =>
            DensityFunction.ALL_AXES;
    }
}
