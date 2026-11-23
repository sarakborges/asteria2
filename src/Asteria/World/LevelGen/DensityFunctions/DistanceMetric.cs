using Asteria.Serialization;

namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Three-dimensional and horizontal distance metrics used by density functions in Minecraft Java 26.3.
/// </summary>
public enum DistanceMetric
{
    EUCLIDEAN,
    EUCLIDEAN_SQUARED,
    MANHATTAN,
    CHEBYSHEV
}

public static class DistanceMetricExtensions
{
    public static Codec<DistanceMetric> CODEC { get; } =
        Codecs.STRING.FlatXMap(
            static name => name switch
            {
                "euclidean" => DataResult<DistanceMetric>.Success(DistanceMetric.EUCLIDEAN),
                "euclidean_squared" => DataResult<DistanceMetric>.Success(DistanceMetric.EUCLIDEAN_SQUARED),
                "manhattan" => DataResult<DistanceMetric>.Success(DistanceMetric.MANHATTAN),
                "chebyshev" => DataResult<DistanceMetric>.Success(DistanceMetric.CHEBYSHEV),
                _ => DataResult<DistanceMetric>.Error(() => $"Unknown distance metric: {name}")
            },
            static metric => DataResult<string>.Success(metric.GetSerializedName()));

    public static string GetSerializedName(this DistanceMetric metric) =>
        metric switch
        {
            DistanceMetric.EUCLIDEAN => "euclidean",
            DistanceMetric.EUCLIDEAN_SQUARED => "euclidean_squared",
            DistanceMetric.MANHATTAN => "manhattan",
            DistanceMetric.CHEBYSHEV => "chebyshev",
            _ => throw new ArgumentOutOfRangeException(nameof(metric))
        };

    public static float Compute(
        this DistanceMetric metric,
        float deltaX,
        float deltaY,
        float deltaZ)
    {
        float absX = MathF.Abs(deltaX);
        float absY = MathF.Abs(deltaY);
        float absZ = MathF.Abs(deltaZ);

        return metric switch
        {
            DistanceMetric.EUCLIDEAN =>
                MathF.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ),
            DistanceMetric.EUCLIDEAN_SQUARED =>
                deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ,
            DistanceMetric.MANHATTAN =>
                absX + absY + absZ,
            DistanceMetric.CHEBYSHEV =>
                MathF.Max(MathF.Max(absX, absY), absZ),
            _ => throw new ArgumentOutOfRangeException(nameof(metric))
        };
    }

    public static float Compute(
        this DistanceMetric metric,
        float deltaX,
        float deltaZ)
    {
        float absX = MathF.Abs(deltaX);
        float absZ = MathF.Abs(deltaZ);

        return metric switch
        {
            DistanceMetric.EUCLIDEAN =>
                MathF.Sqrt(deltaX * deltaX + deltaZ * deltaZ),
            DistanceMetric.EUCLIDEAN_SQUARED =>
                deltaX * deltaX + deltaZ * deltaZ,
            DistanceMetric.MANHATTAN =>
                absX + absZ,
            DistanceMetric.CHEBYSHEV =>
                MathF.Max(absX, absZ),
            _ => throw new ArgumentOutOfRangeException(nameof(metric))
        };
    }
}
