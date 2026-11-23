using System.Text.Json;
using Asteria.Content.Blocks;
using Asteria.Content.Blocks.StateProperties;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

internal static class BlockStateCodecChecks
{
    public static void Run(
        Block block,
        BooleanProperty powered,
        IntegerProperty stage)
    {
        var registry = new Registry<Block>(
            ResourceLocation.Parse("asteria:block_state_codec_test"));
        ResourceLocation blockId = ResourceLocation.Parse("asteria:codec_test_block");
        RegistryKey<Block> key = RegistryKey<Block>.Create(registry.Key, blockId);
        registry.Register(key, block, RegistrationInfo.BuiltIn);
        registry.Freeze();

        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Codec<BlockState> codec = BlockState.CreateCodec(registry.ByNameCodec());
        BlockState defaultState = block.DefaultState;

        DataResult<JsonElement> compactEncoding = codec.EncodeStart(ops, defaultState);
        Require(
            ReferenceEquals(compactEncoding.Lifecycle, Lifecycle.Stable()),
            "Compact block-state encoding must preserve the stable lifecycle of the registry name codec.");
        JsonElement compact = compactEncoding.GetOrThrow();
        DataResult<BlockState> compactDecoding = codec.Parse(ops, compact);
        Require(
            ReferenceEquals(compactDecoding.Lifecycle, Lifecycle.Stable()),
            "Compact block-state decoding must preserve the stable lifecycle of the registry name codec.");
        Require(
            compact.ValueKind == JsonValueKind.String &&
            compact.GetString() == blockId.ToString(),
            "A block's default state must encode as the compact block identifier.");
        Require(
            ReferenceEquals(codec.Parse(ops, compact).GetOrThrow(), defaultState),
            "A compact block identifier must resolve to the exact default state instance.");

        BlockState nonDefault = defaultState.SetValue(powered, true).SetValue(stage, 2);
        DataResult<JsonElement> fullEncoding = codec.EncodeStart(ops, nonDefault);
        Require(
            ReferenceEquals(fullEncoding.Lifecycle, Lifecycle.Stable()),
            "Full block-state encoding must use the stable lifecycle declared by Minecraft 26.3 FULL_CODEC.");
        JsonElement full = fullEncoding.GetOrThrow();
        Require(
            full.ValueKind == JsonValueKind.Object &&
            ops.Get(full, "id").GetOrThrow().GetString() == blockId.ToString(),
            "A non-default block state must encode as an object with an id field.");
        JsonElement properties = ops.Get(full, "properties").GetOrThrow();
        Require(
            ops.Get(properties, "powered").GetOrThrow().GetString() == "true" &&
            ops.Get(properties, "stage").GetOrThrow().GetString() == "2",
            "A full block state must serialize each non-default property using its declared value name.");

        DataResult<BlockState> fullDecoding = codec.Parse(ops, full);
        Require(
            ReferenceEquals(fullDecoding.Lifecycle, Lifecycle.Stable()),
            "Full block-state decoding must use the stable lifecycle declared by Minecraft 26.3 FULL_CODEC.");
        BlockState decoded = fullDecoding.GetOrThrow();
        Require(
            ReferenceEquals(decoded, nonDefault),
            "A full block state must resolve through the block's precomputed immutable state table.");

        JsonElement omittedProperties = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("id"), ops.CreateString(blockId.ToString()))
        });
        Require(
            ReferenceEquals(codec.Parse(ops, omittedProperties).GetOrThrow(), defaultState),
            "A full block-state map without properties must decode to the block default state.");

        JsonElement unknownProperty = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("id"), ops.CreateString(blockId.ToString())),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("properties"),
                ops.CreateMap(new[]
                {
                    new KeyValuePair<JsonElement, JsonElement>(
                        ops.CreateString("unknown_property"), ops.CreateString("ignored"))
                }))
        });
        Require(
            ReferenceEquals(codec.Parse(ops, unknownProperty).GetOrThrow(), defaultState),
            "The typed properties map must ignore unknown keys that are not part of the block definition.");

        JsonElement invalidProperty = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("id"), ops.CreateString(blockId.ToString())),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("properties"),
                ops.CreateMap(new[]
                {
                    new KeyValuePair<JsonElement, JsonElement>(
                        ops.CreateString("powered"), ops.CreateString("yes"))
                }))
        });
        Require(
            codec.Parse(ops, invalidProperty).IsError,
            "A known block-state property must reject serialized values outside its allowed set.");

        Codec<BlockState> fullCodec = BlockState.CreateFullCodec(registry.ByNameCodec());
        Require(
            fullCodec.Parse(ops, compact).IsError,
            "The full block-state codec must require an object and must not accept compact block IDs.");
        Require(
            ReferenceEquals(
                fullCodec.EncodeStart(ops, nonDefault).Lifecycle,
                Lifecycle.Stable()) &&
            ReferenceEquals(
                fullCodec.Parse(ops, full).Lifecycle,
                Lifecycle.Stable()),
            "The full block-state codec must mark both encode and decode results stable.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
