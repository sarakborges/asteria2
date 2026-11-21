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

    internal static void RegisterAll()
    {
        Registry.Register(BuiltInRegistries.Block,
            ResourceLocation.FromNamespaceAndPath("asteria", "air"),
            AIR);
    }
}
