using Asteria.Serialization;
using Asteria.World.LevelGen;
using Asteria.Util;
using Asteria.World.LevelGen.Synth;

namespace Asteria.World.LevelGen.DensityFunctions.Generator;

/// <summary>
/// End outer-island density field from Minecraft Java 26.3.
/// </summary>
public sealed class EndIslandFunction : DensityFunction
{
    private const float ISLAND_THRESHOLD = -0.9f;

    public static MapCodec<EndIslandFunction> CODEC { get; } =
        MapCodec<EndIslandFunction>.Unit(static () => new EndIslandFunction());

    public DensitySampler CompileSampler(DensityFunction.CompileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IRandomSource islandRandom = context.CreateEndIslandRandom();
        islandRandom.ConsumeCount(17292);
        var islandNoise = new SimplexNoise(islandRandom, true);
        return new Sampler(islandNoise);
    }

    public Interval Range() =>
        Interval.Of(-0.84375f, 0.5625f);

    public int DomainAxes() =>
        DensityFunction.AXIS_X | DensityFunction.AXIS_Z;

    public DensityFunction RewriteChildren(DfRewriteRule rule) =>
        this;

    private static float GetHeightValue(SimplexNoise islandNoise, int sectionX, int sectionZ)
    {
        int chunkX = sectionX / 2;
        int chunkZ = sectionZ / 2;
        int subSectionX = sectionX % 2;
        int subSectionZ = sectionZ % 2;
        float offset = -100.0f;

        for (int xOffset = -12; xOffset <= 12; xOffset++)
        {
            for (int zOffset = -12; zOffset <= 12; zOffset++)
            {
                long totalChunkX = chunkX + xOffset;
                long totalChunkZ = chunkZ + zOffset;
                if (totalChunkX * totalChunkX + totalChunkZ * totalChunkZ <= 4096L ||
                    !(islandNoise.Get(totalChunkX, totalChunkZ) < ISLAND_THRESHOLD))
                {
                    continue;
                }

                float islandSize =
                    (Math.Abs(totalChunkX) * 3439.0f +
                     Math.Abs(totalChunkZ) * 147.0f) % 13.0f + 9.0f;

                double xd = subSectionX - xOffset * 2;
                double zd = subSectionZ - zOffset * 2;
                float newOffset = 100.0f -
                    (float)Math.Sqrt(xd * xd + zd * zd) * islandSize;
                newOffset = Math.Clamp(newOffset, -100.0f, 80.0f);
                offset = Math.Max(offset, newOffset);
            }
        }

        return offset;
    }

    private sealed record Sampler(SimplexNoise IslandNoise) : DensitySampler
    {
        public void SampleVolume(
            SamplerContext context,
            DensityBuffer outputBuffer,
            DensityVolume volume)
        {
            for (int z = 0; z < volume.SizeZ; z++)
            {
                int blockZ = volume.BlockZ(z);
                for (int x = 0; x < volume.SizeX; x++)
                {
                    int blockX = volume.BlockX(x);
                    float value = SampleValue(context, blockX, 0, blockZ);
                    int index = volume.IndexUnchecked(x, 0, z);
                    outputBuffer.SetRange(index, volume.SizeY, value);
                }
            }
        }

        public float SampleValue(
            SamplerContext context,
            int blockX,
            int blockY,
            int blockZ) =>
            (GetHeightValue(IslandNoise, blockX / 8, blockZ / 8) - 8.0f) / 128.0f;
    }
}
