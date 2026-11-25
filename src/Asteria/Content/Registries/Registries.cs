using Asteria.Content.Identifiers;
using Asteria.Serialization;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.Synth;
using Asteria.World.LevelGen.Materials.Rules;
using Asteria.World.LevelGen.Materials.Conditions;
using Asteria.World.LevelGen;
using Asteria.Content.Biomes;
using Asteria.World.LevelGen.Features;
using Asteria.World.LevelGen.Placement;
using Asteria.World.LevelGen.Features.StateProviders;
using Asteria.World.LevelGen.Structure.Placement;
using Asteria.World.LevelGen.Structure;

namespace Asteria.Content.Registries;

/// <summary>
/// Canonical registry keys referenced by registry-aware serialization and world generation.
/// </summary>
public static class Registries
{
    public static RegistryKey<Registry<BlockStateProvider>> BLOCK_STATE_PROVIDER { get; } =
        RegistryKey<BlockStateProvider>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/block_state_provider"));

    public static RegistryKey<Registry<MapCodec<BlockStateProvider>>> BLOCK_STATE_PROVIDER_TYPE { get; } =
        RegistryKey<MapCodec<BlockStateProvider>>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/block_state_provider_type"));

    public static RegistryKey<Registry<Feature>> FEATURE { get; } =
        RegistryKey<Feature>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/feature"));

    public static RegistryKey<Registry<PlacedFeature>> PLACED_FEATURE { get; } =
        RegistryKey<PlacedFeature>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/placed_feature"));

    public static RegistryKey<Registry<MapCodec<Feature>>> FEATURE_TYPE { get; } =
        RegistryKey<MapCodec<Feature>>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/feature_type"));

    public static RegistryKey<Registry<MapCodec<PlacementModifier>>> PLACEMENT_MODIFIER_TYPE { get; } =
        RegistryKey<MapCodec<PlacementModifier>>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/placement_modifier_type"));

    public static RegistryKey<Registry<MapCodec<StructurePlacement>>> STRUCTURE_PLACEMENT_TYPE { get; } =
        RegistryKey<MapCodec<StructurePlacement>>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/structure_placement_type"));

    public static RegistryKey<Registry<StructureSet>> STRUCTURE_SET { get; } =
        RegistryKey<StructureSet>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/structure_set"));

    public static RegistryKey<Registry<Biome>> BIOME { get; } =
        RegistryKey<Biome>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "biome"));

    public static RegistryKey<Registry<WorldCarver>> CARVER { get; } =
        RegistryKey<WorldCarver>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/carver"));

    public static RegistryKey<Registry<DensityFunction>> DENSITY_FUNCTION { get; } =
        RegistryKey<DensityFunction>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/density_function"));

    public static RegistryKey<Registry<MapCodec<WorldCarver>>> CARVER_TYPE { get; } =
        RegistryKey<MapCodec<WorldCarver>>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/carver_type"));

    public static RegistryKey<Registry<MapCodec<HeightProvider>>> HEIGHT_PROVIDER_TYPE { get; } =
        RegistryKey<MapCodec<HeightProvider>>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/height_provider_type"));

    public static RegistryKey<Registry<MapCodec<IntProvider>>> INT_PROVIDER_TYPE { get; } =
        RegistryKey<MapCodec<IntProvider>>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/int_provider_type"));

    public static RegistryKey<Registry<MapCodec<FloatProvider>>> FLOAT_PROVIDER_TYPE { get; } =
        RegistryKey<MapCodec<FloatProvider>>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/float_provider_type"));

    public static RegistryKey<Registry<NormalNoise>> NOISE { get; } =
        RegistryKey<NormalNoise>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/noise"));

    public static RegistryKey<Registry<MaterialRule>> MATERIAL_RULE { get; } =
        RegistryKey<MaterialRule>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/material_rule"));

    public static RegistryKey<Registry<MaterialCondition>> MATERIAL_CONDITION { get; } =
        RegistryKey<MaterialCondition>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/material_condition"));

    public static RegistryKey<Registry<NoiseGeneratorSettings>> NOISE_SETTINGS { get; } =
        RegistryKey<NoiseGeneratorSettings>.CreateRegistryKey(
            ResourceLocation.FromNamespaceAndPath("asteria", "worldgen/noise_settings"));
}
