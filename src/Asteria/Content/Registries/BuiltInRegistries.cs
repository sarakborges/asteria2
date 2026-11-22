using Block = Asteria.Content.Blocks.Block;
using Biome = Asteria.Content.Biomes.Biome;
using Item = Asteria.Content.Items.Item;
using Asteria.Content.Blocks;
using Asteria.Content.Particles;
using BuiltInBlocks = Asteria.Content.Blocks.Blocks;
using BuiltInBiomes = Asteria.Content.Biomes.Biomes;
using BuiltInItems = Asteria.Content.Items.Items;
using BuiltInAttributeTypes = Asteria.World.Attributes.AttributeTypes;
using BuiltInEnvironmentAttributes = Asteria.World.Attributes.EnvironmentAttributes;
using Asteria.Content.Fluids;
using BuiltInFluids = Asteria.Content.Fluids.Fluids;
using Asteria.Content.Identifiers;
using Asteria.World.Attributes;
using Asteria.World.GameEvents;
using Asteria.World.Clocks;
using Asteria.World.Timelines;

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

    public static DefaultedRegistry<Fluid> Fluid { get; } =
        RegisterDefaultedWithIntrusiveHolders<Fluid>(
            ResourceLocation.FromNamespaceAndPath("asteria", "fluid"),
            FluidIds.EMPTY,
            static () => BuiltInFluids.RegisterAll());

    public static DefaultedRegistry<Item> Item { get; } =
        RegisterDefaultedWithIntrusiveHolders<Item>(
            ResourceLocation.FromNamespaceAndPath("asteria", "item"),
            ResourceLocation.FromNamespaceAndPath("asteria", "air"),
            static () => BuiltInItems.RegisterAll());

    public static DefaultedRegistry<Biome> Biome { get; } =
        RegisterDefaultedWithIntrusiveHolders<Biome>(
            ResourceLocation.FromNamespaceAndPath("asteria", "biome"),
            ResourceLocation.FromNamespaceAndPath("asteria", "plains"),
            static () => BuiltInBiomes.RegisterAll());

    public static Registry<ParticleType> ParticleType { get; } =
        RegisterSimple<ParticleType>(
            ResourceLocation.FromNamespaceAndPath("asteria", "particle_type"),
            static () => ParticleTypes.RegisterAll());

    public static Registry<IEnvironmentAttribute> EnvironmentAttribute { get; } =
        RegisterSimple<IEnvironmentAttribute>(
            ResourceLocation.FromNamespaceAndPath("asteria", "environment_attribute"),
            static () => BuiltInEnvironmentAttributes.RegisterAll());

    public static Registry<IAttributeType> AttributeType { get; } =
        RegisterSimple<IAttributeType>(
            ResourceLocation.FromNamespaceAndPath("asteria", "attribute_type"),
            static () => BuiltInAttributeTypes.RegisterAll());

    public static Registry<PositionSourceType> PositionSourceType { get; } =
        RegisterSimple<PositionSourceType>(
            ResourceLocation.FromNamespaceAndPath("asteria", "position_source_type"),
            static () => PositionSourceTypes.RegisterAll());

    public static Registry<WorldClock> WorldClock { get; } =
        RegisterSimple<WorldClock>(
            ResourceLocation.FromNamespaceAndPath("asteria", "world_clock"),
            static () => WorldClocks.RegisterAll());

    public static Registry<Timeline> Timeline { get; } =
        RegisterSimple<Timeline>(
            ResourceLocation.FromNamespaceAndPath("asteria", "timeline"),
            static () => { });

    private static Registry<T> RegisterSimple<T>(
        ResourceLocation location,
        Action bootstrap)
        where T : class =>
        InternalRegister(location, new Registry<T>(location), bootstrap);

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

        InitializeBlockStateCaches();
        Freeze();
    }

    private static void InitializeBlockStateCaches()
    {
        foreach (Block block in BuiltInRegistries.Block)
            block.InitializeStateCaches();
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
