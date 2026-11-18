using Asteria.Content.Blocks;
using Asteria.Content.Identifiers;

namespace Asteria.Content.Registries;

/// <summary>
/// The statically defined registries and ID maps available to the game.
/// </summary>
public static class BuiltInRegistries
{
    public static DefaultedRegistry<Block> Block { get; } = new(
        ResourceLocation.FromNamespaceAndPath("asteria", "block"),
        ResourceLocation.FromNamespaceAndPath("asteria", "air"),
        allowIntrusiveHolders: true);

    public static BlockStateIdMap BlockStates { get; } = new();

    public static void FreezeAll()
    {
        Block.Freeze();
        BlockStates.Freeze();
    }

    public static bool AreFrozen => Block.IsFrozen && BlockStates.IsFrozen;
}
