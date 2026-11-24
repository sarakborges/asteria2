using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.World.LevelGen.DensityFunctions;

namespace Asteria.World.LevelGen;

/// <summary>
/// Built-in density-function keys from Minecraft Java 26.3, using Asteria's local registry namespace.
/// </summary>
public static class DensityFunctionKeys
{
    public static RegistryKey<DensityFunction> ZERO { get; } = CreateKey("zero");
    public static RegistryKey<DensityFunction> Y { get; } = CreateKey("y");
    public static RegistryKey<DensityFunction> SHIFT_X { get; } = CreateKey("shift_x");
    public static RegistryKey<DensityFunction> ORE_VEIN_TOGGLE { get; } = CreateKey("overworld/ore_vein/toggle");
    public static RegistryKey<DensityFunction> ORE_VEIN_RICHNESS { get; } = CreateKey("overworld/ore_vein/richness");
    public static RegistryKey<DensityFunction> ORE_VEIN_MASK { get; } = CreateKey("overworld/ore_vein/mask");
    public static RegistryKey<DensityFunction> ORE_VEIN_COPPER_DENSITY { get; } = CreateKey("overworld/ore_vein/copper_density");
    public static RegistryKey<DensityFunction> ORE_VEIN_IRON_DENSITY { get; } = CreateKey("overworld/ore_vein/iron_density");
    public static RegistryKey<DensityFunction> ORE_VEIN_GAP { get; } = CreateKey("overworld/ore_vein/gap");
    public static RegistryKey<DensityFunction> SHIFT_Z { get; } = CreateKey("shift_z");
    public static RegistryKey<DensityFunction> BASE_3D_NOISE_OVERWORLD { get; } = CreateKey("overworld/base_3d_noise");
    public static RegistryKey<DensityFunction> BASE_3D_NOISE_NETHER { get; } = CreateKey("nether/base_3d_noise");
    public static RegistryKey<DensityFunction> BASE_3D_NOISE_END { get; } = CreateKey("end/base_3d_noise");
    public static RegistryKey<DensityFunction> OVERWORLD_TEMPERATURE { get; } = CreateKey("overworld/temperature");
    public static RegistryKey<DensityFunction> OVERWORLD_LARGE_TEMPERATURE { get; } = CreateKey("overworld_large_biomes/temperature");
    public static RegistryKey<DensityFunction> OVERWORLD_VEGETATION { get; } = CreateKey("overworld/vegetation");
    public static RegistryKey<DensityFunction> OVERWORLD_LARGE_VEGETATION { get; } = CreateKey("overworld_large_biomes/vegetation");
    public static RegistryKey<DensityFunction> OVERWORLD_CONTINENTS { get; } = CreateKey("overworld/continents");
    public static RegistryKey<DensityFunction> OVERWORLD_EROSION { get; } = CreateKey("overworld/erosion");
    public static RegistryKey<DensityFunction> OVERWORLD_LARGE_CONTINENTS { get; } = CreateKey("overworld_large_biomes/continents");
    public static RegistryKey<DensityFunction> OVERWORLD_LARGE_EROSION { get; } = CreateKey("overworld_large_biomes/erosion");
    public static RegistryKey<DensityFunction> RIDGES { get; } = CreateKey("overworld/ridges");
    public static RegistryKey<DensityFunction> RIDGES_FOLDED { get; } = CreateKey("overworld/ridges_folded");

    public static RegistryKey<DensityFunction> OVERWORLD_OFFSET { get; } = CreateKey("overworld/offset");
    public static RegistryKey<DensityFunction> OVERWORLD_FACTOR { get; } = CreateKey("overworld/factor");
    public static RegistryKey<DensityFunction> OVERWORLD_JAGGEDNESS { get; } = CreateKey("overworld/jaggedness");
    public static RegistryKey<DensityFunction> OVERWORLD_DEPTH { get; } = CreateKey("overworld/depth");
    public static RegistryKey<DensityFunction> OVERWORLD_SLOPED_CHEESE { get; } = CreateKey("overworld/sloped_cheese");
    public static RegistryKey<DensityFunction> OVERWORLD_PRELIMINARY_SURFACE_LEVEL { get; } = CreateKey("overworld/preliminary_surface_level");
    public static RegistryKey<DensityFunction> OVERWORLD_CHUNK_SURFACE_LEVEL { get; } = CreateKey("overworld/chunk_surface_level");
    public static RegistryKey<DensityFunction> OVERWORLD_FINAL_DENSITY { get; } = CreateKey("overworld/final_density");

    public static RegistryKey<DensityFunction> OVERWORLD_AMPLIFIED_OFFSET { get; } = CreateKey("overworld_amplified/offset");
    public static RegistryKey<DensityFunction> OVERWORLD_AMPLIFIED_FACTOR { get; } = CreateKey("overworld_amplified/factor");
    public static RegistryKey<DensityFunction> OVERWORLD_AMPLIFIED_JAGGEDNESS { get; } = CreateKey("overworld_amplified/jaggedness");
    public static RegistryKey<DensityFunction> OVERWORLD_AMPLIFIED_DEPTH { get; } = CreateKey("overworld_amplified/depth");
    public static RegistryKey<DensityFunction> OVERWORLD_AMPLIFIED_SLOPED_CHEESE { get; } = CreateKey("overworld_amplified/sloped_cheese");
    public static RegistryKey<DensityFunction> OVERWORLD_AMPLIFIED_PRELIMINARY_SURFACE_LEVEL { get; } = CreateKey("overworld_amplified/preliminary_surface_level");
    public static RegistryKey<DensityFunction> OVERWORLD_AMPLIFIED_CHUNK_SURFACE_LEVEL { get; } = CreateKey("overworld_amplified/chunk_surface_level");
    public static RegistryKey<DensityFunction> OVERWORLD_AMPLIFIED_FINAL_DENSITY { get; } = CreateKey("overworld_amplified/final_density");

    public static RegistryKey<DensityFunction> OVERWORLD_LARGE_OFFSET { get; } = CreateKey("overworld_large_biomes/offset");
    public static RegistryKey<DensityFunction> OVERWORLD_LARGE_FACTOR { get; } = CreateKey("overworld_large_biomes/factor");
    public static RegistryKey<DensityFunction> OVERWORLD_LARGE_JAGGEDNESS { get; } = CreateKey("overworld_large_biomes/jaggedness");
    public static RegistryKey<DensityFunction> OVERWORLD_LARGE_DEPTH { get; } = CreateKey("overworld_large_biomes/depth");
    public static RegistryKey<DensityFunction> OVERWORLD_LARGE_SLOPED_CHEESE { get; } = CreateKey("overworld_large_biomes/sloped_cheese");
    public static RegistryKey<DensityFunction> OVERWORLD_LARGE_PRELIMINARY_SURFACE_LEVEL { get; } = CreateKey("overworld_large_biomes/preliminary_surface_level");
    public static RegistryKey<DensityFunction> OVERWORLD_LARGE_CHUNK_SURFACE_LEVEL { get; } = CreateKey("overworld_large_biomes/chunk_surface_level");
    public static RegistryKey<DensityFunction> OVERWORLD_LARGE_FINAL_DENSITY { get; } = CreateKey("overworld_large_biomes/final_density");

    public static RegistryKey<DensityFunction> SPAGHETTI_ROUGHNESS_FUNCTION { get; } = CreateKey("overworld/caves/spaghetti_roughness_function");
    public static RegistryKey<DensityFunction> ENTRANCES { get; } = CreateKey("overworld/caves/entrances");
    public static RegistryKey<DensityFunction> NOODLE { get; } = CreateKey("overworld/caves/noodle");
    public static RegistryKey<DensityFunction> PILLARS { get; } = CreateKey("overworld/caves/pillars");
    public static RegistryKey<DensityFunction> SPAGHETTI_2D_THICKNESS_MODULATOR { get; } = CreateKey("overworld/caves/spaghetti_2d_thickness_modulator");
    public static RegistryKey<DensityFunction> SPAGHETTI_2D { get; } = CreateKey("overworld/caves/spaghetti_2d");

    /// <summary>
    /// Vanilla's twelve density-function keys for normal Overworld generation.
    /// </summary>
    public static OverworldFunctionSet<RegistryKey<DensityFunction>> OVERWORLD_FUNCTIONS { get; } = new(
        OVERWORLD_TEMPERATURE,
        OVERWORLD_VEGETATION,
        OVERWORLD_CONTINENTS,
        OVERWORLD_EROSION,
        OVERWORLD_OFFSET,
        OVERWORLD_FACTOR,
        OVERWORLD_JAGGEDNESS,
        OVERWORLD_DEPTH,
        OVERWORLD_SLOPED_CHEESE,
        OVERWORLD_PRELIMINARY_SURFACE_LEVEL,
        OVERWORLD_CHUNK_SURFACE_LEVEL,
        OVERWORLD_FINAL_DENSITY);

    /// <summary>
    /// Vanilla's amplified profile shares the four climate keys and replaces its eight terrain keys.
    /// </summary>
    public static OverworldFunctionSet<RegistryKey<DensityFunction>> AMPLIFIED_OVERWORLD_FUNCTIONS { get; } = new(
        OVERWORLD_FUNCTIONS.Temperature,
        OVERWORLD_FUNCTIONS.Vegetation,
        OVERWORLD_FUNCTIONS.Continents,
        OVERWORLD_FUNCTIONS.Erosion,
        OVERWORLD_AMPLIFIED_OFFSET,
        OVERWORLD_AMPLIFIED_FACTOR,
        OVERWORLD_AMPLIFIED_JAGGEDNESS,
        OVERWORLD_AMPLIFIED_DEPTH,
        OVERWORLD_AMPLIFIED_SLOPED_CHEESE,
        OVERWORLD_AMPLIFIED_PRELIMINARY_SURFACE_LEVEL,
        OVERWORLD_AMPLIFIED_CHUNK_SURFACE_LEVEL,
        OVERWORLD_AMPLIFIED_FINAL_DENSITY);

    /// <summary>
    /// Vanilla's large-biome profile uses its own twelve keys, including climate noise entries.
    /// </summary>
    public static OverworldFunctionSet<RegistryKey<DensityFunction>> LARGE_OVERWORLD_FUNCTIONS { get; } = new(
        OVERWORLD_LARGE_TEMPERATURE,
        OVERWORLD_LARGE_VEGETATION,
        OVERWORLD_LARGE_CONTINENTS,
        OVERWORLD_LARGE_EROSION,
        OVERWORLD_LARGE_OFFSET,
        OVERWORLD_LARGE_FACTOR,
        OVERWORLD_LARGE_JAGGEDNESS,
        OVERWORLD_LARGE_DEPTH,
        OVERWORLD_LARGE_SLOPED_CHEESE,
        OVERWORLD_LARGE_PRELIMINARY_SURFACE_LEVEL,
        OVERWORLD_LARGE_CHUNK_SURFACE_LEVEL,
        OVERWORLD_LARGE_FINAL_DENSITY);

    private static RegistryKey<DensityFunction> CreateKey(string name) =>
        RegistryKey<DensityFunction>.Create(
            Registries.DENSITY_FUNCTION,
            ResourceLocation.WithDefaultNamespace(name));
}
