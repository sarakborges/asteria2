using Block = Asteria.Content.Blocks.Block;
using Biome = Asteria.Content.Biomes.Biome;
using Item = Asteria.Content.Items.Item;
using Asteria.Content.Blocks;
using ChunkStatusValue = Asteria.Content.Chunks.Status.ChunkStatus;
using Asteria.Content.Particles;
using BuiltInBlocks = Asteria.Content.Blocks.Blocks;
using BuiltInBiomes = Asteria.Content.Biomes.Biomes;
using BuiltInItems = Asteria.Content.Items.Items;
using BuiltInAttributeTypes = Asteria.World.Attributes.AttributeTypes;
using BuiltInEnvironmentAttributes = Asteria.World.Attributes.EnvironmentAttributes;
using Asteria.Content.Fluids;
using BuiltInFluids = Asteria.Content.Fluids.Fluids;
using Asteria.Content.Identifiers;
using TicketTypeValue = Asteria.Server.Level.TicketType;
using Asteria.World.Attributes;
using Asteria.World.GameEvents;
using Asteria.World.Clocks;
using Asteria.World.Timelines;
using Asteria.Serialization;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.Synth;
using BuiltInNoises = Asteria.World.LevelGen.BuiltInNoises;
using DensityFunctionValue = Asteria.World.LevelGen.DensityFunctions.DensityFunction;
using BuiltInDensityFunctions = Asteria.World.LevelGen.BuiltInDensityFunctions;
using Asteria.World.LevelGen.Materials.Rules;
using Asteria.World.LevelGen.Materials.Conditions;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Features;
using Asteria.World.LevelGen.Placement;
using Asteria.World.LevelGen.Structure.Placement;
using Asteria.World.LevelGen.Structure;
using BlockStateProviderValue = Asteria.World.LevelGen.Features.StateProviders.BlockStateProvider;
using Asteria.World.LevelGen.Features.StateProviders;

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
            ResourceLocation.FromNamespaceAndPath("minecraft", "air"),
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

    public static Registry<MapCodec<WorldCarver>> CarverType { get; } =
        RegisterSimple<MapCodec<WorldCarver>>(
            Registries.CARVER_TYPE.Location,
            static () => WorldCarverCodecBootstrap.RegisterAll(CarverType));

    public static Registry<MapCodec<HeightProvider>> HeightProviderType { get; } =
        RegisterSimple<MapCodec<HeightProvider>>(
            Registries.HEIGHT_PROVIDER_TYPE.Location,
            static () => HeightProviderCodecBootstrap.RegisterAll(HeightProviderType));

    public static Registry<MapCodec<IntProvider>> IntProviderType { get; } =
        RegisterSimple<MapCodec<IntProvider>>(
            Registries.INT_PROVIDER_TYPE.Location,
            static () => IntProviderCodecBootstrap.RegisterAll(IntProviderType));

    public static Registry<MapCodec<FloatProvider>> FloatProviderType { get; } =
        RegisterSimple<MapCodec<FloatProvider>>(
            Registries.FLOAT_PROVIDER_TYPE.Location,
            static () => FloatProviderCodecBootstrap.RegisterAll(FloatProviderType));

    public static Registry<WorldCarver> Carver { get; } =
        RegisterSimple<WorldCarver>(
            Registries.CARVER.Location,
            static () => Carvers.RegisterAll());

    public static Registry<MapCodec<BlockStateProviderValue>> BlockStateProviderType { get; } =
        RegisterSimple<MapCodec<BlockStateProviderValue>>(
            Registries.BLOCK_STATE_PROVIDER_TYPE.Location,
            static () => BlockStateProviderCodecBootstrap.RegisterAll(BlockStateProviderType));

    public static Registry<BlockStateProviderValue> BlockStateProvider { get; } =
        RegisterSimple<BlockStateProviderValue>(
            Registries.BLOCK_STATE_PROVIDER.Location,
            static () => { });

    public static Registry<MapCodec<Feature>> FeatureType { get; } =
        RegisterSimple<MapCodec<Feature>>(
            Registries.FEATURE_TYPE.Location,
            static () => FeatureCodecBootstrap.RegisterAll(FeatureType));

    public static Registry<Feature> Feature { get; } =
        RegisterSimple<Feature>(
            Registries.FEATURE.Location,
            static () => { });

    public static Registry<MapCodec<PlacementModifier>> PlacementModifierType { get; } =
        RegisterSimple<MapCodec<PlacementModifier>>(
            Registries.PLACEMENT_MODIFIER_TYPE.Location,
            static () => PlacementModifierCodecBootstrap.RegisterAll(PlacementModifierType));

    public static Registry<MapCodec<StructurePlacement>> StructurePlacementType { get; } =
        RegisterSimple<MapCodec<StructurePlacement>>(
            Registries.STRUCTURE_PLACEMENT_TYPE.Location,
            static () => StructurePlacementCodecBootstrap.RegisterAll(StructurePlacementType));

    public static Registry<StructureSet> StructureSet { get; } =
        RegisterSimple<StructureSet>(
            Registries.STRUCTURE_SET.Location,
            static () => { });

    public static Registry<Structure> Structure { get; } =
        RegisterSimple<Structure>(
            Registries.STRUCTURE.Location,
            static () => { });

    public static Registry<MapCodec<Structure>> StructureType { get; } =
        RegisterSimple<MapCodec<Structure>>(
            Registries.STRUCTURE_TYPE.Location,
            static () => { });

    public static Registry<PlacedFeature> PlacedFeature { get; } =
        RegisterSimple<PlacedFeature>(
            Registries.PLACED_FEATURE.Location,
            static () => { });

    public static DefaultedRegistry<Biome> Biome { get; } =
        RegisterDefaultedWithIntrusiveHolders<Biome>(
            ResourceLocation.FromNamespaceAndPath("asteria", "biome"),
            ResourceLocation.FromNamespaceAndPath("minecraft", "plains"),
            static () => BuiltInBiomes.RegisterAll());

    public static Registry<NormalNoise> Noise { get; } =
        RegisterSimple<NormalNoise>(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/noise"),
            static () => BuiltInNoises.RegisterAll());

    public static Registry<DensityFunctionValue> DensityFunction { get; } =
        RegisterSimple<DensityFunctionValue>(
            Registries.DENSITY_FUNCTION.Location,
            static () => BuiltInDensityFunctions.RegisterAll());

    public static Registry<MapCodec<DensityFunction>> DensityFunctionType { get; } =
        RegisterSimple<MapCodec<DensityFunction>>(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/density_function_type"),
            static () => DensityFunctionCodecBootstrap.RegisterAll(DensityFunctionType));

    public static Registry<MapCodec<MaterialRule>> MaterialRuleType { get; } =
        RegisterSimple<MapCodec<MaterialRule>>(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/material_rule_type"),
            static () => MaterialRuleCodecBootstrap.RegisterAll(MaterialRuleType));

    public static Registry<MaterialCondition> MaterialCondition { get; } =
        RegisterSimple<MaterialCondition>(
            Registries.MATERIAL_CONDITION.Location,
            static () => VanillaMaterialConditions.RegisterAll());

    public static Registry<MapCodec<MaterialCondition>> MaterialConditionType { get; } =
        RegisterSimple<MapCodec<MaterialCondition>>(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/material_condition_type"),
            static () => MaterialConditionCodecBootstrap.RegisterAll(MaterialConditionType));

    public static Registry<MaterialRule> MaterialRule { get; } =
        RegisterSimple<MaterialRule>(
            Registries.MATERIAL_RULE.Location,
            static () => { });

    public static Registry<NoiseGeneratorSettings> NoiseGeneratorSettings { get; } =
        RegisterSimple<NoiseGeneratorSettings>(
            Registries.NOISE_SETTINGS.Location,
            static () => { });

    public static Registry<ChunkStatusValue> ChunkStatus { get; } =
        RegisterSimple<ChunkStatusValue>(
            ResourceLocation.FromNamespaceAndPath("asteria", "chunk_status"),
            static () => { _ = ChunkStatusValue.EMPTY; });

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

    public static Registry<TicketTypeValue> TicketType { get; } =
        RegisterSimple<TicketTypeValue>(
            Registries.TICKET_TYPE.Location,
            static () => TicketTypeValue.RegisterAll());

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

        SolidDebugger.RunAndDump(InitializeBlockStateCaches);
        Freeze();
        BuiltInBlockTags.BindSupportedValues();
    }

    private static void InitializeBlockStateCaches()
    {
        foreach (Block block in BuiltInRegistries.Block)
            block.InitializeStateCaches();
    }

    private static void Freeze()
    {
        // Bind the root's registry values first. Child registries can then be staged
        // independently while their registry objects remain the canonical root values.
        Root.Freeze();

        foreach (Holder<IRegistry>.Reference registryHolder in Root.ListElements())
        {
            IRegistry registry = registryHolder.Value;
            if (ReferenceEquals(registry, MaterialRule) ||
                ReferenceEquals(registry, NoiseGeneratorSettings))
            {
                continue;
            }

            if (!registry.IsFrozen)
                registry.BindAllTagsToEmpty();
            registry.Freeze();
        }

        // MaterialRule JSON uses map codecs from MaterialRuleType and
        // MaterialConditionType, whose standalone holders only bind during Freeze.
        // Decode the graph after those dependencies are frozen, but before its own
        // registry is frozen so all pre-reserved rule holders can be populated.
        BuiltInMaterialRules.RegisterAll();

        if (!MaterialRule.IsFrozen)
            MaterialRule.BindAllTagsToEmpty();
        MaterialRule.Freeze();

        // Noise settings depend on the finalized material-rule tree and the
        // frozen density-function, noise and block registries.
        BuiltInNoiseGeneratorSettings.RegisterAll();
        if (!NoiseGeneratorSettings.IsFrozen)
            NoiseGeneratorSettings.BindAllTagsToEmpty();
        NoiseGeneratorSettings.Freeze();

        foreach (Holder<IRegistry>.Reference registryHolder in Root.ListElements())
            registryHolder.Value.Validate();
    }

    public static bool AreFrozen =>
        Root.IsFrozen &&
        Root.ListElements().All(registryHolder => registryHolder.Value.IsFrozen);
}
