namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Point and volume sampling contract from Minecraft Java 26.3 DensitySampler.
/// </summary>
public interface DensitySampler
{
    void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume);

    float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ);

    static void SampleVolumeNaive(
        SamplerContext context,
        DensityBuffer outputBuffer,
        DensityVolume volume,
        DensitySampler sampler)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(outputBuffer);
        ArgumentNullException.ThrowIfNull(volume);
        ArgumentNullException.ThrowIfNull(sampler);

        int index = 0;
        for (int z = 0; z < volume.SizeZ; z++)
        {
            int blockZ = volume.BlockZ(z);
            for (int x = 0; x < volume.SizeX; x++)
            {
                int blockX = volume.BlockX(x);
                for (int y = 0; y < volume.SizeY; y++)
                {
                    int blockY = volume.BlockY(y);
                    outputBuffer.Set(index++, sampler.SampleValue(context, blockX, blockY, blockZ));
                }
            }
        }
    }

    Bound Bind(SamplerContext context) => new(this, context);

    public readonly record struct Bound(DensitySampler Sampler, SamplerContext Context)
    {
        public float SampleValue(int blockX, int blockY, int blockZ) =>
            Sampler.SampleValue(Context, blockX, blockY, blockZ);

        public void SampleVolume(DensityBuffer outputBuffer, DensityVolume volume) =>
            Sampler.SampleVolume(Context, outputBuffer, volume);

        public ScopedDensityBuffer SampleVolume(DensityVolume volume)
        {
            ScopedDensityBuffer buffer = Context.AcquireBuffer(volume);
            SampleVolume(buffer, volume);
            return buffer;
        }
    }
}
