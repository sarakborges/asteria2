using Asteria.Content.Identifiers;
using Asteria.Content.Registries;

namespace Asteria.Content.Blocks;

/// <summary>
/// Built-in Asteria block definitions.
/// </summary>
public static class Blocks
{
    public static Block AIR { get; } = new(
        BlockProperties.Of().Air());

    public static Block STONE { get; } = new(
        BlockProperties.Of().Strength(1.5f, 6.0f));

    public static Block DIRT { get; } = new(
        BlockProperties.Of().Strength(0.5f));

    public static Block GRASS_BLOCK { get; } = new(
        BlockProperties.Of().Strength(0.6f));

    internal static void RegisterAll()
    {
        Register("air", AIR);
        Register("stone", STONE);
        Register("dirt", DIRT);
        Register("grass_block", GRASS_BLOCK);
    }

    private static void Register(string path, Block block) =>
        Registry.Register(
            BuiltInRegistries.Block,
            ResourceLocation.FromNamespaceAndPath("asteria", path),
            block);
}
