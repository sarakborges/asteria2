using Asteria.Content.Blocks;
using Asteria.Content.Identifiers;

namespace Asteria.Content.Registries;

/// <summary>
/// The root registry and statically defined registries available to the game.
/// </summary>
public static class BuiltInRegistries
{
    public static ResourceLocation RootLocation { get; } =
        ResourceLocation.FromNamespaceAndPath("asteria", "root");

    public static Registry<IRegistry> Root { get; } = new(RootLocation);

    public static DefaultedRegistry<Block> Block { get; } = new(
        ResourceLocation.FromNamespaceAndPath("asteria", "block"),
        ResourceLocation.FromNamespaceAndPath("asteria", "air"),
        allowIntrusiveHolders: true);

    static BuiltInRegistries()
    {
        Root.Register(
            new RegistryKey<IRegistry>(Root.Location, Block.Location),
            Block,
            RegistrationInfo.BuiltIn);
    }

    public static void FreezeAll()
    {
        Root.Freeze();

        foreach (Holder<IRegistry>.Reference registryHolder in Root.ListElements())
        {
            IRegistry registry = registryHolder.Value;
            if (!registry.IsFrozen)
                registry.BindAllTagsToEmpty();
            registry.Freeze();
        }

        foreach (Holder<IRegistry>.Reference registryHolder in Root.ListElements())
            registryHolder.Value.Validate();
    }

    public static bool AreFrozen =>
        Root.IsFrozen &&
        Root.ListElements().All(registryHolder => registryHolder.Value.IsFrozen);
}
