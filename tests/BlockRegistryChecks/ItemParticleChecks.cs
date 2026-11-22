using System.Text.Json;
using Asteria.Content.Identifiers;
using Asteria.Content.Items;
using Asteria.Content.Particles;
using Asteria.Content.Registries;
using Asteria.Serialization;

internal static class ItemParticleChecks
{
    private static class TestItems
    {
        public static Registry<Item> Registry { get; }
        public static Item ITEM { get; }
        public static Codec<Holder<Item>> ItemCodec { get; }
        public static Codec<ItemStackTemplate> StackCodec { get; }

        static TestItems()
        {
            Registry = new Registry<Item>(
                ResourceLocationKey("test_item_registry"),
                allowIntrusiveHolders: true);

            ITEM = new Item(Registry);
            Registry.Register(
                RegistryKey<Item>.Create(Registry.Key, ResourceLocationKey("test_item")),
                ITEM,
                RegistrationInfo.BuiltIn);
            Registry.BindAllTagsToEmpty();
            Registry.Freeze();

            ItemCodec = Item.CreateCodec(Registry);
            StackCodec = ItemStackTemplate.CreateCodec(ItemCodec);
        }
    }

    public static void Run()
    {
        CheckProductionItemRegistry();
        CheckEmptyItemStackContract();
        CheckGenericItemStackTemplateCodec();
        CheckGenericItemParticleOptionCodec();
    }

    private static void CheckProductionItemRegistry()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Registry<Item> registry = BuiltInRegistries.Item;

        Require(registry.IsFrozen && registry.Count == 1,
            "The production item registry must contain only the air sentinel.");
        Require(registry.GetId(Items.AIR) == 0 &&
                ReferenceEquals(registry.GetValue(ResourceLocationKey("air")), Items.AIR),
            "The production item registry must retain air as its default entry.");
        Require(ReferenceEquals(registry.GetValue(ResourceLocationKey("missing")), Items.AIR),
            "Defaulted item lookup must retain its fallback-to-air semantics.");

        Require(Item.CODEC.Parse(ops, ops.CreateString("asteria:air")).IsError &&
                Item.CODEC.Parse(ops, ops.CreateString("asteria:test_item")).IsError &&
                Item.CODEC.Parse(ops, ops.CreateString("asteria:unknown_item")).IsError,
            "The production item codec must reject the empty sentinel and identifiers absent from production content.");
    }

    private static void CheckEmptyItemStackContract()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        bool constructorRejectedAir = false;
        try
        {
            _ = new ItemStackTemplate(Items.AIR);
        }
        catch (InvalidOperationException exception) when (exception.Message == "Item must be non-empty")
        {
            constructorRejectedAir = true;
        }

        Require(constructorRejectedAir,
            "An item-stack template must reject the air sentinel as an empty stack.");
        Require(ItemStackTemplate.CODEC.Parse(ops, ops.CreateString("asteria:air")).IsError &&
                ItemStackTemplate.CODEC.Parse(ops, ops.CreateString("asteria:unknown_item")).IsError,
            "The production item-stack codec must reject empty and unregistered item identifiers.");
    }

    private static void CheckGenericItemStackTemplateCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Item item = TestItems.ITEM;
        Codec<ItemStackTemplate> codec = TestItems.StackCodec;

        ItemStackTemplate defaultStack = new(item);
        JsonElement defaultEncoded = codec.EncodeStart(ops, defaultStack).GetOrThrow();
        Require(
            ops.Get(defaultEncoded, "id").GetOrThrow().GetString() == "asteria:test_item" &&
            !ops.Get(defaultEncoded, "count").TryGetResult(out _) &&
            !ops.Get(defaultEncoded, "components").TryGetResult(out _),
            "A default item stack template must encode id only, omitting default count and empty components.");

        ItemStackTemplate defaultDecoded = codec.Parse(ops, defaultEncoded).GetOrThrow();
        Require(
            ReferenceEquals(defaultDecoded.ItemHolder.Value, item) &&
            defaultDecoded.Count == 1 &&
            ReferenceEquals(defaultDecoded.Components, DataComponentPatch.EMPTY),
            "The generic item-stack codec must restore holder identity, default count and empty patch.");

        ItemStackTemplate countedStack = new(item, 4);
        JsonElement countedEncoded = codec.EncodeStart(ops, countedStack).GetOrThrow();
        Require(ops.Get(countedEncoded, "count").GetOrThrow().GetInt32() == 4,
            "A non-default item stack count must be encoded as an integer.");

        ItemStackTemplate countedDecoded = codec.Parse(ops, countedEncoded).GetOrThrow();
        Require(ReferenceEquals(countedDecoded.ItemHolder.Value, item) && countedDecoded.Count == 4,
            "The generic item-stack codec must round-trip holder identity and count.");

        ItemStackTemplate compactDecoded =
            codec.Parse(ops, ops.CreateString("asteria:test_item")).GetOrThrow();
        Require(ReferenceEquals(compactDecoded.ItemHolder.Value, item) && compactDecoded.Count == 1,
            "The generic item-stack codec must accept its compact item-id alternative.");

        foreach (int validCount in new[] { 1, 99 })
        {
            JsonElement input = CreateStackMap(ops, validCount);
            Require(codec.Parse(ops, input).IsSuccess,
                "Item stack template counts one and ninety-nine must be accepted.");
        }

        foreach (int invalidCount in new[] { 0, 100, -1 })
        {
            JsonElement input = CreateStackMap(ops, invalidCount);
            Require(codec.Parse(ops, input).IsError,
                "Item stack template codec must reject counts outside [1, 99].");
        }

        JsonElement emptyComponents = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("id"), ops.CreateString("asteria:test_item")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("components"), ops.EmptyMap)
        });
        ItemStackTemplate emptyPatchDecoded = codec.Parse(ops, emptyComponents).GetOrThrow();
        Require(ReferenceEquals(emptyPatchDecoded.Components, DataComponentPatch.EMPTY),
            "An explicit empty data-component patch must decode to the empty singleton.");

        JsonElement nonEmptyComponents = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("id"), ops.CreateString("asteria:test_item")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("components"),
                ops.CreateMap(new[]
                {
                    new KeyValuePair<JsonElement, JsonElement>(
                        ops.CreateString("asteria:unregistered_component"), ops.CreateInt(1))
                }))
        });
        Require(codec.Parse(ops, nonEmptyComponents).IsError,
            "Non-empty data-component patches must fail until their component type registry and codecs are ported.");

        Require(BuiltInRegistries.Item.Count == 1,
            "Creating the test-only item fixture must not change the production item registry.");
    }

    private static void CheckGenericItemParticleOptionCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        MapCodec<ItemParticleOption> mapCodec =
            ItemParticleOption.CreateCodec(ParticleTypes.ITEM, TestItems.StackCodec);
        Codec<ItemParticleOption> codec = mapCodec.Codec();

        ItemParticleOption source = new(ParticleTypes.ITEM, new ItemStackTemplate(TestItems.ITEM, 3));
        JsonElement encoded = codec.EncodeStart(ops, source).GetOrThrow();
        Require(
            ops.Get(ops.Get(encoded, "item").GetOrThrow(), "id").GetOrThrow().GetString() == "asteria:test_item" &&
            ops.Get(ops.Get(encoded, "item").GetOrThrow(), "count").GetOrThrow().GetInt32() == 3,
            "The generic item-particle option must encode the nested item-stack template.");

        ItemParticleOption decoded = codec.Parse(ops, encoded).GetOrThrow();
        Require(
            ReferenceEquals(decoded.Type, ParticleTypes.ITEM) &&
            ReferenceEquals(decoded.ItemStack.ItemHolder.Value, TestItems.ITEM) &&
            decoded.ItemStack.Count == 3,
            "The generic item-particle option must round-trip particle type, item identity and stack count.");

        JsonElement compactPayload = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("item"), ops.CreateString("asteria:test_item"))
        });
        ItemParticleOption compactDecoded = codec.Parse(ops, compactPayload).GetOrThrow();
        Require(
            ReferenceEquals(compactDecoded.ItemStack.ItemHolder.Value, TestItems.ITEM) &&
            compactDecoded.ItemStack.Count == 1,
            "The generic item-particle option must accept the compact item-id payload alternative.");
    }

    private static JsonElement CreateStackMap(DynamicOps<JsonElement> ops, int count) =>
        ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("id"), ops.CreateString("asteria:test_item")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("count"), ops.CreateInt(count))
        });

    private static ResourceLocation ResourceLocationKey(string path) =>
        ResourceLocation.WithDefaultNamespace(path);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
