using System.Text.Json;
using Asteria.Content.Items;
using Asteria.Content.Particles;
using Asteria.Content.Registries;
using Asteria.Serialization;

internal static class ItemParticleChecks
{
    public static void Run()
    {
        CheckItemRegistryAndCodec();
        CheckEmptyItemStackContract();
        CheckGenericItemParticleType();
    }

    private static void CheckItemRegistryAndCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Registry<Item> registry = BuiltInRegistries.Item;

        Require(registry.IsFrozen && registry.Count == 1,
            "The production item registry must contain only the air sentinel.");
        Require(registry.GetId(Items.AIR) == 0 &&
                ReferenceEquals(registry.GetValue(ResourceLocationKey("air")), Items.AIR),
            "The item registry must retain air as its default entry.");
        Require(ReferenceEquals(registry.GetValue(ResourceLocationKey("missing")), Items.AIR),
            "Defaulted item lookup must retain its fallback-to-air semantics.");

        Require(Item.CODEC.Parse(ops, ops.CreateString("asteria:air")).IsError &&
                Item.CODEC.Parse(ops, ops.CreateString("asteria:stick")).IsError &&
                Item.CODEC.Parse(ops, ops.CreateString("asteria:unknown_item")).IsError,
            "The item codec must reject the empty sentinel and all absent item content.");
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
            "The item-stack codec must reject empty and unregistered item identifiers.");

        JsonElement emptyComponents = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("id"), ops.CreateString("asteria:air")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("components"), ops.EmptyMap)
        });
        Require(ItemStackTemplate.CODEC.Parse(ops, emptyComponents).IsError,
            "An explicit empty component patch must not make the air sentinel a valid stack.");
    }

    private static void CheckGenericItemParticleType()
    {
        Registry<ParticleType> registry = BuiltInRegistries.ParticleType;

        Require(registry.GetValue(ResourceLocationKey("item")) == ParticleTypes.ITEM &&
                !ParticleTypes.ITEM.OverrideLimiter &&
                ParticleTypes.ITEM.OptionsCodec is not null,
            "The reusable item particle type and its codec must remain registered independently of concrete item content.");

        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        JsonElement payload = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString("asteria:item")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("item"), ops.CreateString("asteria:air"))
        });
        Require(ParticleTypes.CODEC.Parse(ops, payload).IsError,
            "The generic item particle codec must reject an empty item payload.");
    }

    private static ResourceLocation ResourceLocationKey(string path) =>
        Asteria.Content.Identifiers.ResourceLocation.WithDefaultNamespace(path);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}