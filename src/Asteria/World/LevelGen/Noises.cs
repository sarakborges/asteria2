using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.World.LevelGen.Synth;

namespace Asteria.World.LevelGen;

/// <summary>
/// Canonical noise registry keys from Minecraft Java 26.3.
/// </summary>
public static class Noises
{
    public static RegistryKey<NormalNoise> TEMPERATURE { get; } = CreateKey("temperature");
    public static RegistryKey<NormalNoise> VEGETATION { get; } = CreateKey("vegetation");
    public static RegistryKey<NormalNoise> CONTINENTALNESS { get; } = CreateKey("continentalness");
    public static RegistryKey<NormalNoise> EROSION { get; } = CreateKey("erosion");
    public static RegistryKey<NormalNoise> TEMPERATURE_LARGE { get; } = CreateKey("temperature_large");
    public static RegistryKey<NormalNoise> VEGETATION_LARGE { get; } = CreateKey("vegetation_large");
    public static RegistryKey<NormalNoise> CONTINENTALNESS_LARGE { get; } = CreateKey("continentalness_large");
    public static RegistryKey<NormalNoise> EROSION_LARGE { get; } = CreateKey("erosion_large");
    public static RegistryKey<NormalNoise> RIDGE { get; } = CreateKey("ridge");
    public static RegistryKey<NormalNoise> SHIFT { get; } = CreateKey("offset");
    public static RegistryKey<NormalNoise> TEMPERATURE_NETHER { get; } = CreateKey("nether/temperature");
    public static RegistryKey<NormalNoise> VEGETATION_NETHER { get; } = CreateKey("nether/vegetation");
    public static RegistryKey<NormalNoise> AQUIFER_BARRIER { get; } = CreateKey("aquifer_barrier");
    public static RegistryKey<NormalNoise> AQUIFER_FLUID_LEVEL_FLOODEDNESS { get; } = CreateKey("aquifer_fluid_level_floodedness");
    public static RegistryKey<NormalNoise> AQUIFER_LAVA { get; } = CreateKey("aquifer_lava");
    public static RegistryKey<NormalNoise> AQUIFER_FLUID_LEVEL_SPREAD { get; } = CreateKey("aquifer_fluid_level_spread");
    public static RegistryKey<NormalNoise> PILLAR { get; } = CreateKey("pillar");
    public static RegistryKey<NormalNoise> PILLAR_RARENESS { get; } = CreateKey("pillar_rareness");
    public static RegistryKey<NormalNoise> PILLAR_THICKNESS { get; } = CreateKey("pillar_thickness");
    public static RegistryKey<NormalNoise> SPAGHETTI_2D { get; } = CreateKey("spaghetti_2d");
    public static RegistryKey<NormalNoise> SPAGHETTI_2D_ELEVATION { get; } = CreateKey("spaghetti_2d_elevation");
    public static RegistryKey<NormalNoise> SPAGHETTI_2D_MODULATOR { get; } = CreateKey("spaghetti_2d_modulator");
    public static RegistryKey<NormalNoise> SPAGHETTI_2D_THICKNESS { get; } = CreateKey("spaghetti_2d_thickness");
    public static RegistryKey<NormalNoise> SPAGHETTI_3D_1 { get; } = CreateKey("spaghetti_3d_1");
    public static RegistryKey<NormalNoise> SPAGHETTI_3D_2 { get; } = CreateKey("spaghetti_3d_2");
    public static RegistryKey<NormalNoise> SPAGHETTI_3D_RARITY { get; } = CreateKey("spaghetti_3d_rarity");
    public static RegistryKey<NormalNoise> SPAGHETTI_3D_THICKNESS { get; } = CreateKey("spaghetti_3d_thickness");
    public static RegistryKey<NormalNoise> SPAGHETTI_ROUGHNESS { get; } = CreateKey("spaghetti_roughness");
    public static RegistryKey<NormalNoise> SPAGHETTI_ROUGHNESS_MODULATOR { get; } = CreateKey("spaghetti_roughness_modulator");
    public static RegistryKey<NormalNoise> CAVE_ENTRANCE { get; } = CreateKey("cave_entrance");
    public static RegistryKey<NormalNoise> CAVE_LAYER { get; } = CreateKey("cave_layer");
    public static RegistryKey<NormalNoise> CAVE_CHEESE { get; } = CreateKey("cave_cheese");
    public static RegistryKey<NormalNoise> ORE_VEININESS { get; } = CreateKey("ore_veininess");
    public static RegistryKey<NormalNoise> ORE_VEIN_A { get; } = CreateKey("ore_vein_a");
    public static RegistryKey<NormalNoise> ORE_VEIN_B { get; } = CreateKey("ore_vein_b");
    public static RegistryKey<NormalNoise> ORE_GAP { get; } = CreateKey("ore_gap");
    public static RegistryKey<NormalNoise> NOODLE { get; } = CreateKey("noodle");
    public static RegistryKey<NormalNoise> NOODLE_THICKNESS { get; } = CreateKey("noodle_thickness");
    public static RegistryKey<NormalNoise> NOODLE_RIDGE_A { get; } = CreateKey("noodle_ridge_a");
    public static RegistryKey<NormalNoise> NOODLE_RIDGE_B { get; } = CreateKey("noodle_ridge_b");
    public static RegistryKey<NormalNoise> JAGGED { get; } = CreateKey("jagged");
    public static RegistryKey<NormalNoise> SURFACE { get; } = CreateKey("surface");
    public static RegistryKey<NormalNoise> SURFACE_SECONDARY { get; } = CreateKey("surface_secondary");
    public static RegistryKey<NormalNoise> CLAY_BANDS_OFFSET { get; } = CreateKey("clay_bands_offset");
    public static RegistryKey<NormalNoise> BADLANDS_PILLAR { get; } = CreateKey("badlands_pillar");
    public static RegistryKey<NormalNoise> BADLANDS_PILLAR_ROOF { get; } = CreateKey("badlands_pillar_roof");
    public static RegistryKey<NormalNoise> BADLANDS_SURFACE { get; } = CreateKey("badlands_surface");
    public static RegistryKey<NormalNoise> ICEBERG_PILLAR { get; } = CreateKey("iceberg_pillar");
    public static RegistryKey<NormalNoise> ICEBERG_PILLAR_ROOF { get; } = CreateKey("iceberg_pillar_roof");
    public static RegistryKey<NormalNoise> ICEBERG_SURFACE { get; } = CreateKey("iceberg_surface");
    public static RegistryKey<NormalNoise> SULFUR_CAVE_GRADIENT { get; } = CreateKey("sulfur_cave_gradient");
    public static RegistryKey<NormalNoise> SWAMP { get; } = CreateKey("surface_swamp");
    public static RegistryKey<NormalNoise> CALCITE { get; } = CreateKey("calcite");
    public static RegistryKey<NormalNoise> GRAVEL { get; } = CreateKey("gravel");
    public static RegistryKey<NormalNoise> POWDER_SNOW { get; } = CreateKey("powder_snow");
    public static RegistryKey<NormalNoise> PACKED_ICE { get; } = CreateKey("packed_ice");
    public static RegistryKey<NormalNoise> ICE { get; } = CreateKey("ice");
    public static RegistryKey<NormalNoise> SOUL_SAND_LAYER { get; } = CreateKey("soul_sand_layer");
    public static RegistryKey<NormalNoise> GRAVEL_LAYER { get; } = CreateKey("gravel_layer");
    public static RegistryKey<NormalNoise> PATCH { get; } = CreateKey("patch");
    public static RegistryKey<NormalNoise> SMALL_PATCH { get; } = CreateKey("small_patch");
    public static RegistryKey<NormalNoise> NETHERRACK { get; } = CreateKey("netherrack");
    public static RegistryKey<NormalNoise> NETHER_WART { get; } = CreateKey("nether_wart");
    public static RegistryKey<NormalNoise> NETHER_STATE_SELECTOR { get; } = CreateKey("nether_state_selector");

    private static RegistryKey<NormalNoise> CreateKey(string name) =>
        RegistryKey<NormalNoise>.Create(
            Registries.NOISE,
            ResourceLocation.FromNamespaceAndPath("minecraft", name));
}
