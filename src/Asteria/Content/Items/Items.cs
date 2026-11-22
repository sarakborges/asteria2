using Asteria.Content.Identifiers;
using Asteria.Content.Registries;

namespace Asteria.Content.Items;

/// <summary>
/// First built-in item identities needed by the item-stack and particle domains.
/// This is an intentionally partial content registry, not a synthetic test catalog.
/// </summary>
public static class Items
{
    public static Item AIR { get; } = new();
    public static Item STICK { get; } = new();

    internal static void RegisterAll()
    {
        Register("air", AIR);
        Register("stick", STICK);
    }

    private static void Register(string name, Item item)
    {
        ResourceLocation location =
            ResourceLocation.FromNamespaceAndPath("asteria", name);
        RegistryKey<Item> key =
            RegistryKey<Item>.Create(BuiltInRegistries.Item.Key, location);
        BuiltInRegistries.Item.Register(key, item, RegistrationInfo.BuiltIn);
    }
}
