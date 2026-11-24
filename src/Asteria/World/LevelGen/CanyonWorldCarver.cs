using Asteria.Content.Chunks;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World;

namespace Asteria.World.LevelGen;

/// <summary>
/// Canyon carver from Minecraft Java 26.3. It generates the normalized width profile
/// for each world-Y and uses the same seeded tunnel stream and reach pruning as vanilla.
/// </summary>
public sealed class CanyonWorldCarver : WorldCarver
{
    public float Probability { get; }
    public HeightProvider Y { get; }
    public FloatProvider VerticalRotation { get; }
    public Shape CarverShape { get; }

    public CanyonWorldCarver(
        float Probability,
        HeightProvider Y,
        FloatProvider VerticalRotation,
        Shape CarverShape)
    {
        this.Probability = Probability;
        this.Y = Y ?? throw new ArgumentNullException(nameof(Y));
        this.VerticalRotation = VerticalRotation ??
            throw new ArgumentNullException(nameof(VerticalRotation));
        this.CarverShape = CarverShape ??
            throw new ArgumentNullException(nameof(CarverShape));
    }

    public static MapCodec<CanyonWorldCarver> MAP_CODEC { get; } =
        RecordCodecBuilder.MapCodec<CanyonWorldCarver>(instance =>
            instance.Group(
                    Codecs.FloatRange(0.0f, 1.0f)
                        .FieldOf("probability")
                        .ForGetter<CanyonWorldCarver>(static carver => carver.Probability),
                    HeightProvider.CODEC
                        .FieldOf("y")
                        .ForGetter<CanyonWorldCarver>(static carver => carver.Y),
                    FloatProviders.CODEC
                        .FieldOf("vertical_rotation")
                        .ForGetter<CanyonWorldCarver>(static carver => carver.VerticalRotation),
                    Shape.CODEC
                        .FieldOf("shape")
                        .ForGetter<CanyonWorldCarver>(static carver => carver.CarverShape))
                .Apply(
                    instance,
                    static (probability, y, verticalRotation, shape) =>
                        new CanyonWorldCarver(probability, y, verticalRotation, shape)));

    public static MapCodec<WorldCarver> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<WorldCarver>(
            static carver => carver,
            static carver => (CanyonWorldCarver)carver);

    public bool IsStartChunk(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return random.NextFloat() <= Probability;
    }

    public bool Carve(
        WorldGenerationContext context,
        IRandomSource random,
        ChunkPos targetChunk,
        ChunkPos sourceChunk,
        CarverOutput output)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(output);

        int maxDistance = (((WorldCarver)this).GetRange() * 2 - 1) * 16;
        double x = sourceChunk.GetBlockX(random.NextInt(16));
        int y = Y.Sample(random, context);
        double z = sourceChunk.GetBlockZ(random.NextInt(16));
        float horizontalRotation = random.NextFloat() * (float)(Math.PI * 2.0);
        float verticalRotation = VerticalRotation.Sample(random);
        double yScale = CarverShape.YScale.Sample(random);
        float thickness = CarverShape.Thickness.Sample(random);
        int distance = (int)(maxDistance * CarverShape.DistanceFactor.Sample(random));

        DoCarve(
            context,
            targetChunk,
            random.NextLong(),
            x,
            y,
            z,
            thickness,
            horizontalRotation,
            verticalRotation,
            step: 0,
            distance,
            yScale,
            output);

        return true;
    }

    public MapCodec<WorldCarver> GetCodec() => TYPE_CODEC;

    private void DoCarve(
        WorldGenerationContext context,
        ChunkPos targetChunk,
        long tunnelSeed,
        double x,
        double y,
        double z,
        float thickness,
        float horizontalRotation,
        float verticalRotation,
        int step,
        int distance,
        double yScale,
        CarverOutput output)
    {
        IRandomSource random = new LegacyRandomSource(tunnelSeed);
        float[] widthFactorPerHeight = InitWidthFactors(context, random);
        float yRota = 0.0f;
        float xRota = 0.0f;

        for (int currentStep = step; currentStep < distance; currentStep++)
        {
            double horizontalRadius =
                1.5 + Mth.Sin(currentStep * (float)Math.PI / distance) * thickness;
            double verticalRadius = horizontalRadius * yScale;
            horizontalRadius *= CarverShape.HorizontalRadiusFactor.Sample(random);
            verticalRadius = UpdateVerticalRadius(
                random,
                verticalRadius,
                distance,
                currentStep);

            float xc = Mth.Cos(verticalRotation);
            float xs = Mth.Sin(verticalRotation);
            x += Mth.Cos(horizontalRotation) * xc;
            y += xs;
            z += Mth.Sin(horizontalRotation) * xc;

            verticalRotation *= 0.7f;
            verticalRotation += xRota * 0.05f;
            horizontalRotation += yRota * 0.05f;
            xRota *= 0.8f;
            yRota *= 0.5f;
            xRota += (random.NextFloat() - random.NextFloat()) * random.NextFloat() * 2.0f;
            yRota += (random.NextFloat() - random.NextFloat()) * random.NextFloat() * 4.0f;

            if (random.NextInt(4) != 0)
            {
                if (!WorldCarver.CanReach(
                        targetChunk,
                        x,
                        z,
                        currentStep,
                        distance,
                        thickness))
                {
                    return;
                }

                WorldCarver.CarveEllipsoid(
                    targetChunk,
                    x,
                    y,
                    z,
                    horizontalRadius,
                    verticalRadius,
                    output,
                    (xd, yd, zd, worldY) =>
                        ShouldSkip(context, widthFactorPerHeight, xd, yd, zd, worldY));
            }
        }
    }

    private float[] InitWidthFactors(
        WorldGenerationContext context,
        IRandomSource random)
    {
        int depth = context.GetGenDepth();
        var widthFactorPerHeight = new float[depth];
        float widthFactor = 1.0f;

        for (int yIndex = 0; yIndex < depth; yIndex++)
        {
            if (yIndex == 0 || random.NextInt(CarverShape.WidthSmoothness) == 0)
                widthFactor = 1.0f + random.NextFloat() * random.NextFloat();

            widthFactorPerHeight[yIndex] = widthFactor * widthFactor;
        }

        return widthFactorPerHeight;
    }

    private double UpdateVerticalRadius(
        IRandomSource random,
        double verticalRadius,
        float distance,
        float currentStep)
    {
        float verticalMultiplier =
            1.0f - MathF.Abs(0.5f - currentStep / distance) * 2.0f;
        float factor =
            CarverShape.VerticalRadiusDefaultFactor +
            CarverShape.VerticalRadiusCenterFactor * verticalMultiplier;
        float randomScale = RandomBetween(random, 0.75f, 1.0f);
        return factor * verticalRadius * randomScale;
    }

    private static bool ShouldSkip(
        WorldGenerationContext context,
        float[] widthFactorPerHeight,
        double xd,
        double yd,
        double zd,
        int worldY)
    {
        int yIndex = worldY - context.GetMinGenY();
        return (xd * xd + zd * zd) * widthFactorPerHeight[yIndex - 1] +
            yd * yd / 6.0 >= 1.0;
    }

    private static float RandomBetween(
        IRandomSource random,
        float minimum,
        float maximum) =>
        minimum + random.NextFloat() * (maximum - minimum);

    public sealed record Shape(
        FloatProvider DistanceFactor,
        FloatProvider Thickness,
        int WidthSmoothness,
        FloatProvider HorizontalRadiusFactor,
        float VerticalRadiusDefaultFactor,
        float VerticalRadiusCenterFactor,
        FloatProvider YScale)
    {
        private static readonly MapCodec<(
            FloatProvider DistanceFactor,
            FloatProvider Thickness,
            int WidthSmoothness,
            FloatProvider HorizontalRadiusFactor)> FirstFields =
            RecordCodecBuilder.MapCodec<(
                FloatProvider DistanceFactor,
                FloatProvider Thickness,
                int WidthSmoothness,
                FloatProvider HorizontalRadiusFactor)>(instance =>
                instance.Group(
                        FloatProviders.CODEC.FieldOf("distance_factor")
                            .ForGetter<(FloatProvider, FloatProvider, int, FloatProvider)>(static shape => shape.Item1),
                        FloatProviders.CODEC.FieldOf("thickness")
                            .ForGetter<(FloatProvider, FloatProvider, int, FloatProvider)>(static shape => shape.Item2),
                        Codecs.IntRange(1, int.MaxValue).FieldOf("width_smoothness")
                            .ForGetter<(FloatProvider, FloatProvider, int, FloatProvider)>(static shape => shape.Item3),
                        FloatProviders.CODEC.FieldOf("horizontal_radius_factor")
                            .ForGetter<(FloatProvider, FloatProvider, int, FloatProvider)>(static shape => shape.Item4))
                    .Apply(instance, static (distanceFactor, thickness, widthSmoothness, horizontalRadiusFactor) =>
                        (distanceFactor, thickness, widthSmoothness, horizontalRadiusFactor)));

        private static readonly MapCodec<(
            float VerticalRadiusDefaultFactor,
            float VerticalRadiusCenterFactor,
            FloatProvider YScale)> LastFields =
            RecordCodecBuilder.MapCodec<(
                float VerticalRadiusDefaultFactor,
                float VerticalRadiusCenterFactor,
                FloatProvider YScale)>(instance =>
                instance.Group(
                        Codecs.FLOAT.FieldOf("vertical_radius_default_factor")
                            .ForGetter<(float, float, FloatProvider)>(static shape => shape.Item1),
                        Codecs.FLOAT.FieldOf("vertical_radius_center_factor")
                            .ForGetter<(float, float, FloatProvider)>(static shape => shape.Item2),
                        FloatProviders.CODEC.FieldOf("y_scale")
                            .ForGetter<(float, float, FloatProvider)>(static shape => shape.Item3))
                    .Apply(instance, static (defaultFactor, centerFactor, yScale) =>
                        (defaultFactor, centerFactor, yScale)));

        public static Codec<Shape> CODEC { get; } =
            RecordCodecBuilder.MapCodec<Shape>(instance =>
                instance.Group(
                        FirstFields.ForGetter<Shape>(static shape =>
                            (shape.DistanceFactor, shape.Thickness, shape.WidthSmoothness, shape.HorizontalRadiusFactor)),
                        LastFields.ForGetter<Shape>(static shape =>
                            (shape.VerticalRadiusDefaultFactor, shape.VerticalRadiusCenterFactor, shape.YScale)))
                    .Apply(instance, static (first, last) =>
                        new Shape(
                            first.DistanceFactor,
                            first.Thickness,
                            first.WidthSmoothness,
                            first.HorizontalRadiusFactor,
                            last.VerticalRadiusDefaultFactor,
                            last.VerticalRadiusCenterFactor,
                            last.YScale)))
            .Codec();
    }
}
