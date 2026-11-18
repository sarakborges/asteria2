using Asteria.Content.Identifiers;
using Asteria.Content.Registries;

namespace Asteria.Content.Blocks;

/// <summary>
/// Built-in Asteria block definitions.
/// TEST_BLOCK exists only to exercise the registry bootstrap; it is not Minecraft content.
/// </summary>
public static class Blocks
{
    public static Block TEST_BLOCK { get; } = new(new BlockProperties(
        destroyTime: 1.5f,
        explosionResistance: 6.0f));

    internal static void RegisterAll()
    {
        BuiltInRegistries.Block.Register(
            ResourceLocation.FromNamespaceAndPath("asteria", "registry_test"),
            TEST_BLOCK);
    }
}
