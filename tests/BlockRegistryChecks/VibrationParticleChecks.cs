using System.Text.Json;
using Asteria.Content.Identifiers;
using Asteria.Content.Particles;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World.GameEvents;

internal static class VibrationParticleChecks
{
    private sealed class UnsupportedPositionSource : IPositionSource
    {
        public Vec3? GetPosition() => null;
        public PositionSourceType GetSourceType() => PositionSourceTypes.BLOCK;
    }

    public static void Run()
    {
        CheckPositionSourceRegistry();
        CheckBlockPositionSourceCodec();
        CheckVibrationParticleCodec();
    }

    private static void CheckPositionSourceRegistry()
    {
        var registry = BuiltInRegistries.PositionSourceType;
        Require(registry.IsFrozen && registry.Count == 1,
            "The local position-source registry must freeze with the block source registered.");
        Require(registry.GetId(PositionSourceTypes.BLOCK) == 0 &&
                ReferenceEquals(registry.GetValue(ResourceLocation.Parse("asteria:block")), PositionSourceTypes.BLOCK),
            "The block position-source type must occupy registry ID zero and resolve by name.");
    }

    private static void CheckBlockPositionSourceCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var position = new BlockPos(-12, 64, 37);

        DataResult<JsonElement> encodedPositionResult = BlockPos.CODEC.EncodeStart(ops, position);
        Require(ReferenceEquals(encodedPositionResult.Lifecycle, Lifecycle.Stable()),
            "BlockPos codec writes must retain stable lifecycle metadata.");
        JsonElement encodedPosition = encodedPositionResult.GetOrThrow();
        IReadOnlyList<JsonElement> coordinates = ops.GetList(encodedPosition).GetOrThrow();
        Require(coordinates.Count == 3 &&
                coordinates[0].GetInt32() == -12 &&
                coordinates[1].GetInt32() == 64 &&
                coordinates[2].GetInt32() == 37,
            "BlockPos must encode exactly three signed integers in X/Y/Z order.");

        DataResult<BlockPos> decodedPositionResult = BlockPos.CODEC.Parse(ops, encodedPosition);
        Require(ReferenceEquals(decodedPositionResult.Lifecycle, Lifecycle.Stable()),
            "BlockPos codec reads must retain stable lifecycle metadata.");
        BlockPos decodedPosition = decodedPositionResult.GetOrThrow();
        Require(decodedPosition.Equals(position),
            "BlockPos must round-trip all three integer coordinates.");

        JsonElement shortPosition = ops.CreateList(new[]
        {
            ops.CreateInt(1), ops.CreateInt(2)
        });
        JsonElement longPosition = ops.CreateList(new[]
        {
            ops.CreateInt(1), ops.CreateInt(2), ops.CreateInt(3), ops.CreateInt(4)
        });
        Require(BlockPos.CODEC.Parse(ops, shortPosition).IsError,
            "BlockPos must reject coordinate lists shorter than three integers.");
        DataResult<BlockPos> oversized = BlockPos.CODEC.Parse(ops, longPosition);
        Require(oversized.IsError && !oversized.TryGetPartial(out _),
            "BlockPos must reject oversized coordinate lists without a partial position.");

        var source = new BlockPositionSource(position);
        JsonElement encodedSource = PositionSourceCodecs.CODEC.EncodeStart(ops, source).GetOrThrow();
        Require(ops.Get(encodedSource, "type").GetOrThrow().GetString() == "asteria:block",
            "Position-source dispatch must encode the registered block discriminator.");
        JsonElement encodedSourcePosition = ops.Get(encodedSource, "pos").GetOrThrow();
        BlockPositionSource decodedSource =
            (BlockPositionSource)PositionSourceCodecs.CODEC.Parse(ops, encodedSource).GetOrThrow();
        Require(decodedSource.Pos.Equals(position) &&
                ops.GetList(encodedSourcePosition).GetOrThrow().Count == 3,
            "Block position sources must round-trip their required pos field.");

        Vec3 center = decodedSource.GetPosition()!;
        Require(center.Equals(new Vec3(-11.5, 64.5, 37.5)),
            "A block position source must resolve to the geometric center of its block.");
    }

    private static void CheckVibrationParticleCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var destination = new BlockPositionSource(new BlockPos(4, -8, 15));
        var source = new VibrationParticleOption(destination, 17);

        JsonElement encoded = ParticleTypes.CODEC.EncodeStart(ops, source).GetOrThrow();
        Require(ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:vibration",
            "The vibration particle must encode its registered discriminator.");
        Require(ops.Get(encoded, "arrival_in_ticks").GetOrThrow().GetInt32() == 17,
            "The vibration particle must encode arrival_in_ticks as an integer.");
        JsonElement encodedDestination = ops.Get(encoded, "destination").GetOrThrow();
        Require(ops.Get(encodedDestination, "type").GetOrThrow().GetString() == "asteria:block",
            "The vibration destination must use position-source dispatch.");
        IReadOnlyList<JsonElement> encodedCoordinates =
            ops.GetList(ops.Get(encodedDestination, "pos").GetOrThrow()).GetOrThrow();
        Require(encodedCoordinates.Count == 3 &&
                encodedCoordinates[0].GetInt32() == 4 &&
                encodedCoordinates[1].GetInt32() == -8 &&
                encodedCoordinates[2].GetInt32() == 15,
            "The vibration destination must preserve its block coordinates.");

        VibrationParticleOption decoded =
            (VibrationParticleOption)ParticleTypes.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(decoded.Destination is BlockPositionSource decodedBlock &&
                decodedBlock.Pos.Equals(destination.Pos) &&
                decoded.ArrivalInTicks == 17 &&
                ReferenceEquals(decoded.GetParticleType(), ParticleTypes.VIBRATION),
            "Vibration options must round-trip destination, signed delay and particle type.");

        VibrationParticleOption negativeDelay = new(destination, -23);
        VibrationParticleOption negativeDecoded =
            (VibrationParticleOption)ParticleTypes.CODEC.Parse(
                ops, ParticleTypes.CODEC.EncodeStart(ops, negativeDelay).GetOrThrow()).GetOrThrow();
        Require(negativeDecoded.ArrivalInTicks == -23,
            "Vibration arrival delay must preserve signed Int32 values without extra range checks.");

        JsonElement missingDestination = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString("asteria:vibration")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("arrival_in_ticks"), ops.CreateInt(5))
        });
        JsonElement missingArrival = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString("asteria:vibration")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("destination"),
                ParticlePositionSource(ops, 1, 2, 3))
        });
        Require(ParticleTypes.CODEC.Parse(ops, missingDestination).IsError &&
                ParticleTypes.CODEC.Parse(ops, missingArrival).IsError,
            "Vibration options must reject missing required destination or arrival_in_ticks fields.");

        JsonElement invalidPosition = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString("asteria:vibration")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("destination"),
                ops.CreateMap(new[]
                {
                    new KeyValuePair<JsonElement, JsonElement>(
                        ops.CreateString("type"), ops.CreateString("asteria:block")),
                    new KeyValuePair<JsonElement, JsonElement>(
                        ops.CreateString("pos"),
                        ops.CreateList(new[] { ops.CreateInt(1), ops.CreateInt(2) }))
                })),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("arrival_in_ticks"), ops.CreateInt(5))
        });
        Require(ParticleTypes.CODEC.Parse(ops, invalidPosition).IsError,
            "Vibration options must reject malformed block-position coordinates.");

        JsonElement unknownSourceType = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString("asteria:vibration")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("destination"),
                ops.CreateMap(new[]
                {
                    new KeyValuePair<JsonElement, JsonElement>(
                        ops.CreateString("type"), ops.CreateString("asteria:entity"))
                })),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("arrival_in_ticks"), ops.CreateInt(5))
        });
        Require(ParticleTypes.CODEC.Parse(ops, unknownSourceType).IsError,
            "The local position-source registry must reject the unsupported entity discriminator.");

        var unsupported = new VibrationParticleOption(new UnsupportedPositionSource(), 5);
        DataResult<JsonElement> unsupportedEncoded = ParticleTypes.CODEC.EncodeStart(ops, unsupported);
        Require(unsupportedEncoded.IsError &&
                unsupportedEncoded.ErrorMessage == "Entity position sources are not allowed",
            "The vibration safe codec must preserve the reference error for non-block position sources.");

        Require(!ParticleTypes.VIBRATION.OverrideLimiter &&
                BuiltInRegistries.ParticleType.GetId(ParticleTypes.ITEM) <
                BuiltInRegistries.ParticleType.GetId(ParticleTypes.VIBRATION) &&
                BuiltInRegistries.ParticleType.GetId(ParticleTypes.VIBRATION) <
                BuiltInRegistries.ParticleType.GetId(ParticleTypes.TRAIL),
            "Vibration must retain overrideLimiter=false and its source declaration position.");
    }

    private static JsonElement ParticlePositionSource(
        DynamicOps<JsonElement> ops,
        int x,
        int y,
        int z) =>
        ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString("asteria:block")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("pos"),
                ops.CreateList(new[] { ops.CreateInt(x), ops.CreateInt(y), ops.CreateInt(z) }))
        });

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
