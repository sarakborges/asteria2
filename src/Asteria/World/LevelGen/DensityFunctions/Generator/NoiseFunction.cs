using Asteria.Content.Registries;
using Asteria.Util;
using Asteria.World.LevelGen.Synth;

namespace Asteria.World.LevelGen.DensityFunctions.Generator;

/// <summary>
/// Noise-backed density function from Minecraft Java 26.3.
/// </summary>
public sealed record NoiseFunction(
    Holder<NormalNoise> NoiseData,
    double XzScale,
    double YScale,
    DensityFunction ShiftX,
    DensityFunction ShiftY,
    DensityFunction ShiftZ) : DensityFunction
{
    public DensitySampler CompileSampler(DensityFunction.CompileContext context)
    {
        Noise noise = context.CreateNoiseSampler(NoiseData);
        if (ShiftX.Equals(DensityFunctions.Zero()) &&
            ShiftY.Equals(DensityFunctions.Zero()) &&
            ShiftZ.Equals(DensityFunctions.Zero()))
        {
            return new Sampler(noise, XzScale, YScale);
        }

        DensitySampler shiftX = ShiftX.CompileSampler(context);
        DensitySampler shiftZ = ShiftZ.CompileSampler(context);
        if (ShiftY.Equals(DensityFunctions.Zero()))
            return new ShiftedXzSampler(shiftX, shiftZ, noise, XzScale, YScale);

        DensitySampler shiftY = ShiftY.CompileSampler(context);
        return new ShiftedXyzSampler(shiftX, shiftY, shiftZ, noise, XzScale, YScale);
    }

    public DensityFunction RewriteChildren(DfRewriteRule rule)
    {
        DensityFunction shiftX = rule(ShiftX);
        DensityFunction shiftY = rule(ShiftY);
        DensityFunction shiftZ = rule(ShiftZ);

        return ReferenceEquals(shiftX, ShiftX) &&
            ReferenceEquals(shiftY, ShiftY) &&
            ReferenceEquals(shiftZ, ShiftZ)
            ? this
            : new NoiseFunction(NoiseData, XzScale, YScale, shiftX, shiftY, shiftZ);
    }

    public Interval Range() =>
        NoiseData.Value.GetRange();

    public int DomainAxes()
    {
        int axes = DensityFunction.ALL_AXES;
        if (YScale == 0.0)
            axes &= ~DensityFunction.AXIS_Y;
        if (XzScale == 0.0)
            axes &= ~(DensityFunction.AXIS_X | DensityFunction.AXIS_Z);

        return axes | ShiftX.DomainAxes() | ShiftY.DomainAxes() | ShiftZ.DomainAxes();
    }

    public sealed record Sampler(Noise Noise, double XzScale, double YScale) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            outputBuffer.Fill(0.0f);
            Noise.AddToVolume(outputBuffer, volume, XzScale, YScale, 1.0f);
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Noise.Get(
                blockX * XzScale,
                blockY * YScale,
                blockZ * XzScale);
    }

    public sealed record ShiftedXzSampler(
        DensitySampler ShiftX,
        DensitySampler ShiftZ,
        Noise Noise,
        double XzScale,
        double YScale) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            ShiftX.SampleVolume(context, outputBuffer, volume);
            using ScopedDensityBuffer shiftZBuffer = context.AcquireBuffer(volume);
            ShiftZ.SampleVolume(context, shiftZBuffer, volume);

            int index = 0;
            for (int z = 0; z < volume.SizeZ; z++)
            {
                double baseNoiseZ = volume.BlockZ(z) * XzScale;
                for (int x = 0; x < volume.SizeX; x++)
                {
                    double baseNoiseX = volume.BlockX(x) * XzScale;
                    for (int y = 0; y < volume.SizeY; y++)
                    {
                        double noiseX = baseNoiseX + outputBuffer.Get(index);
                        double noiseY = volume.BlockY(y) * YScale;
                        double noiseZ = baseNoiseZ + shiftZBuffer.Get(index);
                        outputBuffer.Set(index, Noise.Get(noiseX, noiseY, noiseZ));
                        index++;
                    }
                }
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            double x = blockX * XzScale + ShiftX.SampleValue(context, blockX, blockY, blockZ);
            double y = blockY * YScale;
            double z = blockZ * XzScale + ShiftZ.SampleValue(context, blockX, blockY, blockZ);
            return Noise.Get(x, y, z);
        }
    }

    public sealed record ShiftedXyzSampler(
        DensitySampler ShiftX,
        DensitySampler ShiftY,
        DensitySampler ShiftZ,
        Noise Noise,
        double XzScale,
        double YScale) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            ShiftX.SampleVolume(context, outputBuffer, volume);
            using ScopedDensityBuffer shiftYBuffer = context.AcquireBuffer(volume);
            ShiftY.SampleVolume(context, shiftYBuffer, volume);
            using ScopedDensityBuffer shiftZBuffer = context.AcquireBuffer(volume);
            ShiftZ.SampleVolume(context, shiftZBuffer, volume);

            int index = 0;
            for (int z = 0; z < volume.SizeZ; z++)
            {
                double baseNoiseZ = volume.BlockZ(z) * XzScale;
                for (int x = 0; x < volume.SizeX; x++)
                {
                    double baseNoiseX = volume.BlockX(x) * XzScale;
                    for (int y = 0; y < volume.SizeY; y++)
                    {
                        double noiseX = baseNoiseX + outputBuffer.Get(index);
                        double noiseY = volume.BlockY(y) * YScale + shiftYBuffer.Get(index);
                        double noiseZ = baseNoiseZ + shiftZBuffer.Get(index);
                        outputBuffer.Set(index, Noise.Get(noiseX, noiseY, noiseZ));
                        index++;
                    }
                }
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            double x = blockX * XzScale + ShiftX.SampleValue(context, blockX, blockY, blockZ);
            double y = blockY * YScale + ShiftY.SampleValue(context, blockX, blockY, blockZ);
            double z = blockZ * XzScale + ShiftZ.SampleValue(context, blockX, blockY, blockZ);
            return Noise.Get(x, y, z);
        }
    }
}
