using Asteria.Core;
using Asteria.Util;

namespace Asteria.World.LevelGen.DensityFunctions.Generator;

/// <summary>
/// Axis-aligned coordinate gradient density function from Minecraft Java 26.3.
/// </summary>
public sealed record GradientFunction(
    Direction.Axis Axis,
    TilingMode Tiling,
    int FromCoordinate,
    int ToCoordinate,
    float FromValue,
    float ToValue) : DensityFunction
{
    public DensitySampler CompileSampler(DensityFunction.CompileContext context)
    {
        int coordinateRange = unchecked(ToCoordinate - FromCoordinate);
        float coordinateFactor = (ToValue - FromValue) / coordinateRange;

        return Tiling switch
        {
            TilingMode.CLAMP_TO_EDGE => new ClampedSampler(
                Axis,
                FromCoordinate,
                Math.Min(FromCoordinate, ToCoordinate),
                Math.Max(FromCoordinate, ToCoordinate),
                FromValue,
                coordinateFactor),
            TilingMode.REPEAT => new RepeatSampler(
                Axis,
                FromCoordinate,
                coordinateRange,
                FromValue,
                coordinateFactor),
            TilingMode.MIRRORED_REPEAT => new MirroredRepeatSampler(
                Axis,
                FromCoordinate,
                coordinateRange,
                FromValue,
                coordinateFactor),
            _ => throw new ArgumentOutOfRangeException(nameof(Tiling))
        };
    }

    public Interval Range() =>
        Interval.Encapsulating(FromValue, ToValue);

    public int DomainAxes() =>
        DensityFunction.AxesFrom(Axis);

    public DensityFunction RewriteChildren(DfRewriteRule rule) =>
        this;

    private interface IGradientSampler : DensitySampler
    {
        Direction.Axis Axis { get; }

        float Compute(int coordinate);
    }

    private sealed record ClampedSampler(
        Direction.Axis Axis,
        int FromCoordinate,
        int MinCoordinate,
        int MaxCoordinate,
        float FromValue,
        float CoordinateFactor) : IGradientSampler
    {
        public float Compute(int coordinate)
        {
            int relativeCoordinate =
                Mth.Clamp(coordinate, MinCoordinate, MaxCoordinate) - FromCoordinate;
            return FromValue + relativeCoordinate * CoordinateFactor;
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Compute(Axis.Choose(blockX, blockY, blockZ));

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume) =>
            SampleGradientVolume(this, outputBuffer, volume);
    }

    private sealed record RepeatSampler(
        Direction.Axis Axis,
        int FromCoordinate,
        int CoordinateRange,
        float FromValue,
        float CoordinateFactor) : IGradientSampler
    {
        public float Compute(int coordinate)
        {
            int relativeCoordinate = unchecked(coordinate - FromCoordinate);
            int wrappedCoordinate = FloorMod(relativeCoordinate, CoordinateRange);
            return FromValue + wrappedCoordinate * CoordinateFactor;
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Compute(Axis.Choose(blockX, blockY, blockZ));

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume) =>
            SampleGradientVolume(this, outputBuffer, volume);
    }

    private sealed record MirroredRepeatSampler(
        Direction.Axis Axis,
        int FromCoordinate,
        int CoordinateRange,
        float FromValue,
        float CoordinateFactor) : IGradientSampler
    {
        public float Compute(int coordinate)
        {
            int relativeCoordinate = unchecked(coordinate - FromCoordinate);
            int tileIndex = FloorDiv(relativeCoordinate, CoordinateRange);
            int localCoordinate = unchecked(relativeCoordinate - tileIndex * CoordinateRange);
            return (tileIndex & 1) == 0
                ? FromValue + localCoordinate * CoordinateFactor
                : FromValue + (CoordinateRange - localCoordinate) * CoordinateFactor;
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Compute(Axis.Choose(blockX, blockY, blockZ));

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume) =>
            SampleGradientVolume(this, outputBuffer, volume);
    }

    private static void SampleGradientVolume(
        IGradientSampler sampler,
        DensityBuffer output,
        DensityVolume volume)
    {
        if (ReferenceEquals(sampler.Axis, Direction.Axis.X))
        {
            for (int x = 0; x < volume.SizeX; x++)
            {
                float value = sampler.Compute(volume.BlockX(x));
                for (int z = 0; z < volume.SizeZ; z++)
                    output.SetRange(volume.IndexUnchecked(x, 0, z), volume.SizeY, value);
            }
        }
        else if (ReferenceEquals(sampler.Axis, Direction.Axis.Y))
        {
            for (int y = 0; y < volume.SizeY; y++)
            {
                float value = sampler.Compute(volume.BlockY(y));
                for (int z = 0; z < volume.SizeZ; z++)
                {
                    for (int x = 0; x < volume.SizeX; x++)
                        output.Set(volume.IndexUnchecked(x, y, z), value);
                }
            }
        }
        else if (ReferenceEquals(sampler.Axis, Direction.Axis.Z))
        {
            for (int z = 0; z < volume.SizeZ; z++)
            {
                float value = sampler.Compute(volume.BlockZ(z));
                output.SetRange(
                    volume.IndexUnchecked(0, 0, z),
                    volume.SizeX * volume.SizeY,
                    value);
            }
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(sampler.Axis));
        }
    }

    private static int FloorDiv(int numerator, int denominator)
    {
        int quotient = numerator / denominator;
        int remainder = numerator % denominator;
        if (remainder != 0 && (numerator ^ denominator) < 0)
            quotient = unchecked(quotient - 1);
        return quotient;
    }

    private static int FloorMod(int numerator, int denominator) =>
        unchecked(numerator - FloorDiv(numerator, denominator) * denominator);
}
