using System.Text.Json;
using Asteria.Content.Blocks;
using Asteria.Content.Particles;
using Asteria.Content.Registries;
using Asteria.Serialization;

internal static class BlockParticleChecks
{
    public static void Run()
    {
        CheckBlockOptionsRoundTrip();
        CheckFullBlockStateAlternative();
        CheckInvalidPayloads();
    }

    private static void CheckBlockOptionsRoundTrip()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        BlockState air = Blocks.AIR.DefaultState;

        foreach (ParticleType<BlockParticleOption> type in new[]
                 { ParticleTypes.BLOCK, ParticleTypes.BLOCK_MARKER, ParticleTypes.FALLING_DUST })
        {
            var source = new BlockParticleOption(type, air);
            JsonElement encoded = ParticleTypes.CODEC.EncodeStart(ops, source).GetOrThrow();
            Require(
                ops.Get(encoded, "type").GetOrThrow().GetString() ==
                    BuiltInRegistries.ParticleType.GetKey(type)?.ToString(),
                "Block particle options must encode the registered particle discriminator.");
            Require(
                ops.Get(encoded, "block_state").GetOrThrow().GetString() == "minecraft:air",
                "The default block state must use its compact block identifier.");

            BlockParticleOption decoded =
                (BlockParticleOption)ParticleTypes.CODEC.Parse(ops, encoded).GetOrThrow();
            Require(
                ReferenceEquals(decoded.Type, type) && ReferenceEquals(decoded.State, air),
                "Block particle options must round-trip their exact type and immutable block state.");
        }
    }

    private static void CheckFullBlockStateAlternative()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        JsonElement fullState = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("id"), ops.CreateString("minecraft:air"))
        });
        JsonElement input = CreateParticleMap(ops, "asteria:block", fullState);

        BlockParticleOption decoded =
            (BlockParticleOption)ParticleTypes.CODEC.Parse(ops, input).GetOrThrow();
        Require(
            ReferenceEquals(decoded.Type, ParticleTypes.BLOCK) &&
            ReferenceEquals(decoded.State, Blocks.AIR.DefaultState),
            "Block particle options must accept the full block-state object representation.");

        JsonElement reencoded = ParticleTypes.CODEC.EncodeStart(ops, decoded).GetOrThrow();
        Require(
            ops.Get(reencoded, "block_state").GetOrThrow().GetString() == "minecraft:air",
            "A full default block-state object must encode back to the compact block identifier.");
    }

    private static void CheckInvalidPayloads()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        JsonElement missingState = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString("asteria:block"))
        });
        Require(
            ParticleTypes.CODEC.Parse(ops, missingState).IsError,
            "Block particle codec must reject a missing block_state field.");

        JsonElement unknownState = CreateParticleMap(
            ops, "asteria:block_marker", ops.CreateString("minecraft:not_a_registered_block"));
        Require(
            ParticleTypes.CODEC.Parse(ops, unknownState).IsError,
            "Block particle codec must reject an unknown block identifier.");

        JsonElement invalidState = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString("asteria:falling_dust")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("block_state"), ops.CreateInt(42))
        });
        Require(
            ParticleTypes.CODEC.Parse(ops, invalidState).IsError,
            "Block particle codec must reject payloads that are neither a block ID nor a block-state map.");
    }

    private static JsonElement CreateParticleMap(
        DynamicOps<JsonElement> ops,
        string type,
        JsonElement state) =>
        ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString(type)),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("block_state"), state)
        });

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
