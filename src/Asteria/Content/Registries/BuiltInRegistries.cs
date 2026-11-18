using Asteria.Content.Blocks;
using Asteria.Content.Identifiers;

namespace Asteria.Content.Registries;

/// <summary>
/// The statically defined registries available to the game.
/// Additional registry types can be introduced without changing the registry implementation.
/// </summary>
public static class BuiltInRegistries
{
    public static DefaultedRegistry<Block> Block { get; } = new(
        ResourceLocation.FromNamespaceAndPath("asteria", "block"),
        ResourceLocation.FromNamespaceAndPath("asteria", "air"));

    public static void FreezeAll()
    {
        Block.Freeze();
    }

    public static bool AreFrozen => Block.IsFrozen;
}
