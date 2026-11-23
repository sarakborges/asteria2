using Asteria.Serialization;
using Asteria.Util;
using Asteria.World.LevelGen;

namespace Asteria.World.LevelGen.DensityFunctions.Operations;

/// <summary>
/// Finds the uppermost positive density sample on a vertical cell grid,
/// matching Minecraft Java 26.3 FindTopSurfaceFunction.
/// </summary>
public sealed record FindTopSurfaceFunction(
    DensityFunction Density,
    DensityFunction UpperBound,
    int LowerBound,
    int CellHeight) : DensityFunction
{
    public static MapCodec<FindTopSurfaceFunction> CODEC { get; } =
        RecordCodecBuilder.MapCodec<FindTopSurfaceFunction>(instance =>
            instance.Group(
                    DensityFunction.CODEC.FieldOf("density")
                        .ForGetter<FindTopSurfaceFunction>(static function => function.Density),
                    DensityFunction.CODEC.FieldOf("upper_bound")
                        .ForGetter<FindTopSurfaceFunction>(static function => function.UpperBound),
                    Codecs.IntRange(
                            NoiseSettings.DimensionMinY * 2,
                            NoiseSettings.DimensionMaxY * 2)
                        .FieldOf("lower_bound")
                        .ForGetter<FindTopSurfaceFunction>(static function => function.LowerBound),
                    Codecs.IntRange(1, int.MaxValue)
                        .FieldOf("cell_height")
                        .ForGetter<FindTopSurfaceFunction>(static function => function.CellHeight))
                .Apply(
                    instance,
                    static (density, upperBound, lowerBound, cellHeight) =>
                        new FindTopSurfaceFunction(density, upperBound, lowerBound, cellHeight)));

    public DensitySampler CompileSampler(DensityFunction.CompileContext context)
    {
        DensitySampler sampler = new Sampler(
            Density.CompileSampler(context),
            UpperBound.CompileSampler(context),
            LowerBound,
            CellHeight);
        return new SliceFunction.YSampler(sampler, 0);
    }

    public DensityFunction RewriteChildren(DfRewriteRule rule)
    {
        DensityFunction density = rule(Density);
        DensityFunction upperBound = rule(UpperBound);

        return ReferenceEquals(density, Density) &&
            ReferenceEquals(upperBound, UpperBound)
            ? this
            : new FindTopSurfaceFunction(density, upperBound, LowerBound, CellHeight);
    }

    public Interval Range() =>
        Interval.Of(LowerBound, Math.Max(LowerBound, UpperBound.Range().Max()));

    public int DomainAxes() =>
        (Density.DomainAxes() | UpperBound.DomainAxes()) & ~DensityFunction.AXIS_Y;

    private sealed record Sampler(
        DensitySampler Density,
        DensitySampler UpperBound,
        int LowerBound,
        int CellHeight) : DensitySampler
    {
        public void SampleVolume(
            SamplerContext context,
            DensityBuffer outputBuffer,
            DensityVolume volume)
        {
            if (volume.SizeY != 1)
                throw new ArgumentException(
                    $"Cannot sample with sizeY={volume.SizeY}",
                    nameof(volume));

            UpperBound.SampleVolume(context, outputBuffer, volume);

            int index = 0;
            for (int z = 0; z < volume.SizeZ; z++)
            {
                int blockZ = volume.BlockZ(z);
                for (int x = 0; x < volume.SizeX; x++)
                {
                    int blockX = volume.BlockX(x);
                    float upperBound = outputBuffer.Get(index);
                    outputBuffer.Set(
                        index,
                        FindSurfaceFrom(context, blockX, blockZ, upperBound));
                    index++;
                }
            }
        }

        public float SampleValue(
            SamplerContext context,
            int blockX,
            int blockY,
            int blockZ)
        {
            float upperBound = UpperBound.SampleValue(
                context,
                blockX,
                blockY,
                blockZ);
            return FindSurfaceFrom(context, blockX, blockZ, upperBound);
        }

        private int FindSurfaceFrom(
            SamplerContext context,
            int x,
            int z,
            float upperBound)
        {
            int topY = unchecked(Mth.Floor(upperBound / CellHeight) * CellHeight);
            if (topY <= LowerBound)
                return LowerBound;

            for (int probeY = topY; probeY >= LowerBound; probeY = unchecked(probeY - CellHeight))
            {
                if (Density.SampleValue(context, x, probeY, z) > 0.0f)
                    return probeY;
            }

            return LowerBound;
        }
    }
}
