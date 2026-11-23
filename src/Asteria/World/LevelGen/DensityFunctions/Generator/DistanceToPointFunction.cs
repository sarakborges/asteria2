using Asteria.Core;
using Asteria.Serialization;
using Asteria.Util;

namespace Asteria.World.LevelGen.DensityFunctions.Generator;

/// <summary>
/// Samples the configured distance from a fixed integer point, matching Minecraft Java 26.3.
/// </summary>
public sealed record DistanceToPointFunction(
    Vec3i Point,
    DistanceMetric Metric) : DensityFunction
{
    public static MapCodec<DistanceToPointFunction> CODEC { get; } =
        RecordCodecBuilder.MapCodec<DistanceToPointFunction>(instance =>
            instance.Group(
                    Vec3i.CODEC.FieldOf("point")
                        .ForGetter<DistanceToPointFunction>(static function => function.Point),
                    DistanceMetricExtensions.CODEC.FieldOf("metric")
                        .ForGetter<DistanceToPointFunction>(static function => function.Metric))
                .Apply(
                    instance,
                    static (point, metric) =>
                        new DistanceToPointFunction(point, metric)));

    public DensitySampler CompileSampler(DensityFunction.CompileContext context) =>
        new Sampler(Point.GetX(), Point.GetY(), Point.GetZ(), Metric);

    public Interval Range() =>
        Interval.Of(0.0f, float.PositiveInfinity);

    public int DomainAxes() =>
        DensityFunction.ALL_AXES;

    public DensityFunction RewriteChildren(DfRewriteRule rule) =>
        this;

    private sealed record Sampler(
        int X,
        int Y,
        int Z,
        DistanceMetric Metric) : DensitySampler
    {
        public void SampleVolume(
            SamplerContext context,
            DensityBuffer outputBuffer,
            DensityVolume volume)
        {
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
                        outputBuffer.Set(
                            index++,
                            SampleValue(context, blockX, blockY, blockZ));
                    }
                }
            }
        }

        public float SampleValue(
            SamplerContext context,
            int blockX,
            int blockY,
            int blockZ)
        {
            // Vanilla performs the integer subtraction before passing the deltas as floats.
            int deltaX = unchecked(X - blockX);
            int deltaY = unchecked(Y - blockY);
            int deltaZ = unchecked(Z - blockZ);
            return Metric.Compute(deltaX, deltaY, deltaZ);
        }
    }
}
