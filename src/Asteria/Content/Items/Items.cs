using Asteria.Content.Identifiers;
using Asteria.Content.Registries;

namespace Asteria.Content.Items;

/// <summary>
/// Built-in item sentinels required by the generic item registry.
/// </summary>
public static class Items
{
    public static Item AIR { get; } = new();

    internal static void RegisterAll()
    {
        Register("air", AIR);
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
