using Asteria.Content.Chunks;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World;

namespace Asteria.World.LevelGen;

/// <summary>
/// Cave carver from Minecraft Java 26.3, including room creation, branching tunnels,
/// optional weird-thickness bias, the steep-tunnel branch and floor-level clipping.
/// </summary>
public sealed record CaveWorldCarver(
    float Probability,
    HeightProvider Y,
    IntProvider Count,
    FloatProvider Thickness,
    bool WeirdThicknessBias,
    FloatProvider RoomVerticalRadiusMultiplier,
    FloatProvider HorizontalRadiusMultiplier,
    FloatProvider VerticalRadiusMultiplier,
    FloatProvider StartVerticalRadiusMultiplier,
    FloatProvider FloorLevel) : WorldCarver
{
    private static readonly MapCodec<(
        float Probability,
        HeightProvider Y,
        IntProvider Count,
        FloatProvider Thickness)> FirstFields =
        RecordCodecBuilder.MapCodec<(
            float Probability,
            HeightProvider Y,
            IntProvider Count,
            FloatProvider Thickness)>(instance =>
            instance.Group(
                    Codecs.FloatRange(0.0f, 1.0f).FieldOf("probability")
                        .ForGetter<(float, HeightProvider, IntProvider, FloatProvider)>(static carver => carver.Item1),
                    HeightProvider.CODEC.FieldOf("y")
                        .ForGetter<(float, HeightProvider, IntProvider, FloatProvider)>(static carver => carver.Item2),
                    IntProviders.NON_NEGATIVE_CODEC.FieldOf("count")
                        .ForGetter<(float, HeightProvider, IntProvider, FloatProvider)>(static carver => carver.Item3),
                    FloatProviders.Codec(0.0f).FieldOf("thickness")
                        .ForGetter<(float, HeightProvider, IntProvider, FloatProvider)>(static carver => carver.Item4))
                .Apply(instance, static (probability, y, count, thickness) =>
                    (probability, y, count, thickness)));

    private static readonly MapCodec<(
        bool WeirdThicknessBias,
        FloatProvider RoomVerticalRadiusMultiplier,
        FloatProvider HorizontalRadiusMultiplier,
        FloatProvider VerticalRadiusMultiplier)> MiddleFields =
        RecordCodecBuilder.MapCodec<(
            bool WeirdThicknessBias,
            FloatProvider RoomVerticalRadiusMultiplier,
            FloatProvider HorizontalRadiusMultiplier,
            FloatProvider VerticalRadiusMultiplier)>(instance =>
            instance.Group(
                    Codecs.BOOL.OptionalFieldOf("weird_thickness_bias", false)
                        .ForGetter<(bool, FloatProvider, FloatProvider, FloatProvider)>(static carver => carver.Item1),
                    FloatProviders.CODEC.FieldOf("room_vertical_radius_multiplier")
                        .ForGetter<(bool, FloatProvider, FloatProvider, FloatProvider)>(static carver => carver.Item2),
                    FloatProviders.CODEC.FieldOf("horizontal_radius_multiplier")
                        .ForGetter<(bool, FloatProvider, FloatProvider, FloatProvider)>(static carver => carver.Item3),
                    FloatProviders.CODEC.FieldOf("vertical_radius_multiplier")
                        .ForGetter<(bool, FloatProvider, FloatProvider, FloatProvider)>(static carver => carver.Item4))
                .Apply(instance, static (weirdThicknessBias, roomVerticalRadiusMultiplier, horizontalRadiusMultiplier, verticalRadiusMultiplier) =>
                    (weirdThicknessBias, roomVerticalRadiusMultiplier, horizontalRadiusMultiplier, verticalRadiusMultiplier)));

    private static readonly MapCodec<(
        FloatProvider StartVerticalRadiusMultiplier,
        FloatProvider FloorLevel)> LastFields =
        RecordCodecBuilder.MapCodec<(
            FloatProvider StartVerticalRadiusMultiplier,
            FloatProvider FloorLevel)>(instance =>
            instance.Group(
                    FloatProviders.CODEC.OptionalFieldOf(
                            "start_vertical_radius_multiplier",
                            ConstantFloat.Of(1.0f))
                        .ForGetter<(FloatProvider, FloatProvider)>(static carver => carver.Item1),
                    FloatProviders.Codec(-1.0f, 1.0f).FieldOf("floor_level")
                        .ForGetter<(FloatProvider, FloatProvider)>(static carver => carver.Item2))
                .Apply(instance, static (startVerticalRadiusMultiplier, floorLevel) =>
                    (startVerticalRadiusMultiplier, floorLevel)));

    public static MapCodec<CaveWorldCarver> MAP_CODEC { get; } =
        RecordCodecBuilder.MapCodec<CaveWorldCarver>(instance =>
            instance.Group(
                    FirstFields.ForGetter<CaveWorldCarver>(static carver =>
                        (carver.Probability, carver.Y, carver.Count, carver.Thickness)),
                    MiddleFields.ForGetter<CaveWorldCarver>(static carver =>
                        (carver.WeirdThicknessBias, carver.RoomVerticalRadiusMultiplier, carver.HorizontalRadiusMultiplier, carver.VerticalRadiusMultiplier)),
                    LastFields.ForGetter<CaveWorldCarver>(static carver =>
                        (carver.StartVerticalRadiusMultiplier, carver.FloorLevel)))
                .Apply(instance, static (first, middle, last) =>
                    new CaveWorldCarver(
                        first.Probability,
                        first.Y,
                        first.Count,
                        first.Thickness,
                        middle.WeirdThicknessBias,
                        middle.RoomVerticalRadiusMultiplier,
                        middle.HorizontalRadiusMultiplier,
                        middle.VerticalRadiusMultiplier,
                        last.StartVerticalRadiusMultiplier,
                        last.FloorLevel)));
    public static MapCodec<WorldCarver> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<WorldCarver>(
            static carver => carver,
            static carver => (CaveWorldCarver)carver);

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
        int caveCount = Count.Sample(random);

        for (int cave = 0; cave < caveCount; cave++)
        {
            double x = sourceChunk.GetBlockX(random.NextInt(16));
            double y = Y.Sample(random, context);
            double z = sourceChunk.GetBlockZ(random.NextInt(16));
            double horizontalRadiusMultiplier = HorizontalRadiusMultiplier.Sample(random);
            double verticalRadiusMultiplier = VerticalRadiusMultiplier.Sample(random);
            double startVerticalRadiusMultiplier = StartVerticalRadiusMultiplier.Sample(random);
            double floorLevel = FloorLevel.Sample(random);

            WorldCarver.CarveSkipChecker skipChecker =
                (xd, yd, zd, _) => ShouldSkip(xd, yd, zd, floorLevel);

            int tunnels = 1;
            if (random.NextInt(4) == 0)
            {
                double roomYScale = RoomVerticalRadiusMultiplier.Sample(random);
                float roomThickness = 1.0f + random.NextFloat() * 6.0f;
                CreateRoom(
                    targetChunk,
                    x,
                    y,
                    z,
                    roomThickness,
                    roomYScale,
                    output,
                    skipChecker);
                tunnels += random.NextInt(4);
            }

            for (int tunnel = 0; tunnel < tunnels; tunnel++)
            {
                float horizontalRotation = random.NextFloat() * (float)(Math.PI * 2.0);
                float verticalRotation = (random.NextFloat() - 0.5f) / 4.0f;
                float thickness = GetThickness(random);
                int distance = maxDistance - random.NextInt(maxDistance / 4);

                CreateTunnel(
                    targetChunk,
                    random.NextLong(),
                    x,
                    y,
                    z,
                    horizontalRadiusMultiplier,
                    verticalRadiusMultiplier,
                    thickness,
                    horizontalRotation,
                    verticalRotation,
                    step: 0,
                    distance,
                    startVerticalRadiusMultiplier,
                    output,
                    skipChecker);
            }
        }

        return true;
    }

    public MapCodec<WorldCarver> GetCodec() => TYPE_CODEC;

    private float GetThickness(IRandomSource random)
    {
        float thickness = Thickness.Sample(random);
        if (WeirdThicknessBias && random.NextInt(10) == 0)
            thickness *= random.NextFloat() * random.NextFloat() * 3.0f + 1.0f;

        return thickness;
    }

    private static void CreateRoom(
        ChunkPos targetChunk,
        double x,
        double y,
        double z,
        float thickness,
        double yScale,
        CarverOutput output,
        WorldCarver.CarveSkipChecker skipChecker)
    {
        double horizontalRadius = 1.5 + Mth.Sin((float)(Math.PI / 2.0)) * thickness;
        double verticalRadius = horizontalRadius * yScale;
        WorldCarver.CarveEllipsoid(
            targetChunk,
            x + 1.0,
            y,
            z,
            horizontalRadius,
            verticalRadius,
            output,
            skipChecker);
    }

    private void CreateTunnel(
        ChunkPos targetChunk,
        long tunnelSeed,
        double x,
        double y,
        double z,
        double horizontalRadiusMultiplier,
        double verticalRadiusMultiplier,
        float thickness,
        float horizontalRotation,
        float verticalRotation,
        int step,
        int distance,
        double yScale,
        CarverOutput output,
        WorldCarver.CarveSkipChecker skipChecker)
    {
        IRandomSource random = new LegacyRandomSource(tunnelSeed);
        int splitPoint = random.NextInt(distance / 2) + distance / 4;
        bool steep = random.NextInt(6) == 0;
        float yRota = 0.0f;
        float xRota = 0.0f;

        for (int currentStep = step; currentStep < distance; currentStep++)
        {
            double horizontalRadius =
                1.5 + Mth.Sin((float)Math.PI * currentStep / distance) * thickness;
            double verticalRadius =
                horizontalRadius * (currentStep == step ? yScale : 1.0);

            float cosX = Mth.Cos(verticalRotation);
            x += Mth.Cos(horizontalRotation) * cosX;
            y += Mth.Sin(verticalRotation);
            z += Mth.Sin(horizontalRotation) * cosX;

            verticalRotation *= steep ? 0.92f : 0.7f;
            verticalRotation += xRota * 0.1f;
            horizontalRotation += yRota * 0.1f;
            xRota *= 0.9f;
            yRota *= 0.75f;
            xRota += (random.NextFloat() - random.NextFloat()) * random.NextFloat() * 2.0f;
            yRota += (random.NextFloat() - random.NextFloat()) * random.NextFloat() * 4.0f;

            if (currentStep == splitPoint && thickness > 1.0f)
            {
                CreateTunnel(
                    targetChunk,
                    random.NextLong(),
                    x,
                    y,
                    z,
                    horizontalRadiusMultiplier,
                    verticalRadiusMultiplier,
                    random.NextFloat() * 0.5f + 0.5f,
                    horizontalRotation - (float)(Math.PI / 2.0),
                    verticalRotation / 3.0f,
                    currentStep,
                    distance,
                    1.0,
                    output,
                    skipChecker);
                CreateTunnel(
                    targetChunk,
                    random.NextLong(),
                    x,
                    y,
                    z,
                    horizontalRadiusMultiplier,
                    verticalRadiusMultiplier,
                    random.NextFloat() * 0.5f + 0.5f,
                    horizontalRotation + (float)(Math.PI / 2.0),
                    verticalRotation / 3.0f,
                    currentStep,
                    distance,
                    1.0,
                    output,
                    skipChecker);
                return;
            }

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
                    horizontalRadius * horizontalRadiusMultiplier,
                    verticalRadius * verticalRadiusMultiplier,
                    output,
                    skipChecker);
            }
        }
    }

    private static bool ShouldSkip(
        double xd,
        double yd,
        double zd,
        double floorLevel) =>
        yd <= floorLevel || xd * xd + yd * yd + zd * zd >= 1.0;
}
