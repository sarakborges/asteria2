using System.Text.Json;
using Asteria.Content.Identifiers;
using Asteria.Content.Items;
using Asteria.Content.Particles;
using Asteria.Content.Registries;
using Asteria.Serialization;

internal static class ItemParticleChecks
{
    public static void Run()
    {
        CheckItemRegistryAndCodec();
        CheckItemStackTemplateCodec();
        CheckItemParticleOptionCodec();
    }

    private static void CheckItemRegistryAndCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var registry = BuiltInRegistries.Item;

        Require(registry.IsFrozen && registry.Count == 2,
            "The partial item registry must freeze with air and stick registered.");
        Require(registry.GetId(Items.AIR) == 0 && registry.GetId(Items.STICK) == 1,
            "The partial item registry must preserve its built-in registration order.");
        Require(ReferenceEquals(registry.GetValue(ResourceLocation.Parse("asteria:air")), Items.AIR) &&
                ReferenceEquals(registry.GetValue(ResourceLocation.Parse("asteria:stick")), Items.STICK),
            "The item registry must resolve its registered identifiers.");
        Require(ReferenceEquals(registry.GetValue(ResourceLocation.Parse("asteria:missing")), Items.AIR),
            "Defaulted item registry lookup must retain its default-value semantics.");

        Holder<Item> stick = Item.CODEC.Parse(ops, ops.CreateString("asteria:stick")).GetOrThrow();
        Require(ReferenceEquals(stick.Value, Items.STICK),
            "The item codec must resolve a registered item holder by name.");
        Require(Item.CODEC.Parse(ops, ops.CreateString("asteria:air")).IsError,
            "The item codec must reject the default air item.");
        Require(Item.CODEC.Parse(ops, ops.CreateString("asteria:not_registered")).IsError,
            "The item codec must reject unknown identifiers instead of defaulting to air.");
    }

    private static void CheckItemStackTemplateCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        ItemStackTemplate defaultStack = new(Items.STICK);

        JsonElement defaultEncoded =
            ItemStackTemplate.CODEC.EncodeStart(ops, defaultStack).GetOrThrow();
        Require(
            ops.Get(defaultEncoded, "id").GetOrThrow().GetString() == "asteria:stick" &&
            !ops.Get(defaultEncoded, "count").TryGetResult(out _) &&
            !ops.Get(defaultEncoded, "components").TryGetResult(out _),
            "A default item stack template must encode id only, omitting default count and empty components.");

        ItemStackTemplate defaultDecoded =
            ItemStackTemplate.CODEC.Parse(ops, defaultEncoded).GetOrThrow();
        Require(
            ReferenceEquals(defaultDecoded.ItemHolder.Value, Items.STICK) &&
            defaultDecoded.Count == 1 &&
            ReferenceEquals(defaultDecoded.Components, DataComponentPatch.EMPTY),
            "The item stack template codec must restore the default count and empty component patch.");

        ItemStackTemplate countedStack = new(Items.STICK, 4);
        JsonElement countedEncoded =
            ItemStackTemplate.CODEC.EncodeStart(ops, countedStack).GetOrThrow();
        Require(ops.Get(countedEncoded, "count").GetOrThrow().GetInt32() == 4,
            "A non-default item stack count must be encoded as an integer.");
        ItemStackTemplate countedDecoded =
            ItemStackTemplate.CODEC.Parse(ops, countedEncoded).GetOrThrow();
        Require(
            ReferenceEquals(countedDecoded.ItemHolder.Value, Items.STICK) &&
            countedDecoded.Count == 4,
            "The item stack template codec must round-trip holder identity and count.");

        ItemStackTemplate compactDecoded =
            ItemStackTemplate.CODEC.Parse(ops, ops.CreateString("asteria:stick")).GetOrThrow();
        Require(
            ReferenceEquals(compactDecoded.ItemHolder.Value, Items.STICK) && compactDecoded.Count == 1,
            "The item stack template codec must accept the compact item-id alternative.");

        foreach (int validCount in new[] { 1, 99 })
        {
            JsonElement input = CreateStackMap(ops, validCount);
            Require(ItemStackTemplate.CODEC.Parse(ops, input).IsSuccess,
                "Item stack template counts one and ninety-nine must be accepted.");
        }

        foreach (int invalidCount in new[] { 0, 100, -1 })
        {
            JsonElement input = CreateStackMap(ops, invalidCount);
            Require(ItemStackTemplate.CODEC.Parse(ops, input).IsError,
                "Item stack template codec must reject counts outside [1, 99].");
        }

        JsonElement emptyComponents = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("id"), ops.CreateString("asteria:stick")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("components"), ops.EmptyMap)
        });
        ItemStackTemplate emptyPatchDecoded =
            ItemStackTemplate.CODEC.Parse(ops, emptyComponents).GetOrThrow();
        Require(ReferenceEquals(emptyPatchDecoded.Components, DataComponentPatch.EMPTY),
            "An explicit empty data-component patch must decode to the empty singleton.");

        JsonElement nonEmptyComponents = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("id"), ops.CreateString("asteria:stick")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("components"),
                ops.CreateMap(new[]
                {
                    new KeyValuePair<JsonElement, JsonElement>(
                        ops.CreateString("asteria:unregistered_component"), ops.CreateInt(1))
                }))
        });
        Require(ItemStackTemplate.CODEC.Parse(ops, nonEmptyComponents).IsError,
            "Non-empty data-component patches must fail until their component type registry and codecs are ported.");
    }

    private static void CheckItemParticleOptionCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var source = new ItemParticleOption(
            ParticleTypes.ITEM,
            new ItemStackTemplate(Items.STICK, 3));

        JsonElement encoded =
            ParticleTypes.CODEC.EncodeStart(ops, source).GetOrThrow();
        Require(ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:item",
            "The particle options codec must encode the item particle discriminator.");
        JsonElement encodedItem = ops.Get(encoded, "item").GetOrThrow();
        Require(
            ops.Get(encodedItem, "id").GetOrThrow().GetString() == "asteria:stick" &&
            ops.Get(encodedItem, "count").GetOrThrow().GetInt32() == 3,
            "The item particle payload must contain the nested item-stack template.");

        ItemParticleOption decoded =
            (ItemParticleOption)ParticleTypes.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(
            ReferenceEquals(decoded.Type, ParticleTypes.ITEM) &&
            ReferenceEquals(decoded.ItemStack.ItemHolder.Value, Items.STICK) &&
            decoded.ItemStack.Count == 3,
            "Item particle options must round-trip their type, item identity and stack count.");

        JsonElement compactPayload = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString("asteria:item")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("item"), ops.CreateString("asteria:stick"))
        });
        ItemParticleOption compactDecoded =
            (ItemParticleOption)ParticleTypes.CODEC.Parse(ops, compactPayload).GetOrThrow();
        Require(
            ReferenceEquals(compactDecoded.ItemStack.ItemHolder.Value, Items.STICK) &&
            compactDecoded.ItemStack.Count == 1,
            "The item particle codec must accept the compact item-id payload alternative.");

        JsonElement missingItem = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString("asteria:item"))
        });
        Require(ParticleTypes.CODEC.Parse(ops, missingItem).IsError,
            "The item particle codec must reject a missing required item field.");

        JsonElement airItem = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString("asteria:item")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("item"), ops.CreateString("asteria:air"))
        });
        Require(ParticleTypes.CODEC.Parse(ops, airItem).IsError,
            "The item particle codec must reject the air item.");

        JsonElement invalidItem = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString("asteria:item")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("item"), ops.CreateString("asteria:unknown_item"))
        });
        Require(ParticleTypes.CODEC.Parse(ops, invalidItem).IsError,
            "The item particle codec must reject an unknown item rather than substituting air.");

        Require(!ParticleTypes.ITEM.OverrideLimiter &&
                BuiltInRegistries.ParticleType.GetId(ParticleTypes.INSTANT_EFFECT) <
                BuiltInRegistries.ParticleType.GetId(ParticleTypes.ITEM) &&
                BuiltInRegistries.ParticleType.GetId(ParticleTypes.ITEM) <
                BuiltInRegistries.ParticleType.GetId(ParticleTypes.VIBRATION) &&
                BuiltInRegistries.ParticleType.GetId(ParticleTypes.VIBRATION) <
                BuiltInRegistries.ParticleType.GetId(ParticleTypes.TRAIL),
            "Item particle must retain overrideLimiter=false and its source declaration position.");
    }

    private static JsonElement CreateStackMap(DynamicOps<JsonElement> ops, int count) =>
        ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("id"), ops.CreateString("asteria:stick")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("count"), ops.CreateInt(count))
        });

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
