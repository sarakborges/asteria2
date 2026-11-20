using Block = Asteria.Content.Blocks.Block;
using Asteria.Content.Blocks;
using BuiltInBlocks = Asteria.Content.Blocks.Blocks;
using Asteria.Content.Identifiers;

namespace Asteria.Content.Registries;

/// <summary>
/// The root registry and statically defined registries available to the game.
/// </summary>
public static class BuiltInRegistries
{
    private static readonly List<(ResourceLocation Location, Action Bootstrap)> Loaders = new();

    public static ResourceLocation RootLocation { get; } =
        ResourceLocation.FromNamespaceAndPath("asteria", "root");

    public static Registry<IRegistry> Root { get; } = new(RootLocation);

    public static DefaultedRegistry<Block> Block { get; } =
        RegisterDefaultedWithIntrusiveHolders<Block>(
            ResourceLocation.FromNamespaceAndPath("asteria", "block"),
            ResourceLocation.FromNamespaceAndPath("asteria", "air"),
            static () => BuiltInBlocks.RegisterAll());

    private static DefaultedRegistry<T> RegisterDefaultedWithIntrusiveHolders<T>(
        ResourceLocation location,
        ResourceLocation defaultKey,
        Action bootstrap)
        where T : class =>
        InternalRegister(
            location,
            new DefaultedRegistry<T>(
                location,
                defaultKey,
                allowIntrusiveHolders: true),
            bootstrap);

    private static TRegistry InternalRegister<TRegistry>(
        ResourceLocation location,
        TRegistry registry,
        Action bootstrap)
        where TRegistry : class, IRegistry
    {
        ArgumentNullException.ThrowIfNull(bootstrap);

        Loaders.Add((location, bootstrap));
        Root.Register(
            RegistryKey<IRegistry>.Create(Root.Key, location),
            registry,
            RegistrationInfo.BuiltIn);

        return registry;
    }

    internal static void Bootstrap()
    {
        foreach (var loader in Loaders)
            loader.Bootstrap();

        Freeze();
    }

    private static void Freeze()
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
