using Asteria.Content.Identifiers;
using Asteria.Content.Registries;

namespace Asteria.Content.Biomes;

/// <summary>
/// Built-in Minecraft Java 26.3 Overworld biome climate and special-effect definitions.
/// Mob spawn settings, placed features and carvers are intentionally not represented here.
/// </summary>
public static class Biomes
{
    public static Biome PLAINS { get; } =
        Create(true, 0.8F, 0.4F, 0x3F76E4);

    public static Biome DESERT { get; } =
        Create(false, 2.0F, 0.0F, 0x3F76E4);

    public static Biome DEEP_FROZEN_OCEAN { get; } =
        Create(true, 0.5F, 0.5F, 0x3938C9, Biome.TemperatureModifier.FROZEN);
    public static Biome DEEP_COLD_OCEAN { get; } =
        Create(true, 0.5F, 0.5F, 0x3D57D6);
    public static Biome DEEP_OCEAN { get; } =
        Create(true, 0.5F, 0.5F, 0x3F76E4);
    public static Biome DEEP_LUKEWARM_OCEAN { get; } =
        Create(true, 0.5F, 0.5F, 0x45ADF2);
    public static Biome WARM_OCEAN { get; } =
        Create(true, 0.5F, 0.5F, 0x43D5EE);
    public static Biome FROZEN_OCEAN { get; } =
        Create(true, 0.0F, 0.5F, 0x3938C9, Biome.TemperatureModifier.FROZEN);
    public static Biome COLD_OCEAN { get; } =
        Create(true, 0.5F, 0.5F, 0x3D57D6);
    public static Biome OCEAN { get; } =
        Create(true, 0.5F, 0.5F, 0x3F76E4);
    public static Biome LUKEWARM_OCEAN { get; } =
        Create(true, 0.5F, 0.5F, 0x45ADF2);

    public static Biome SNOWY_PLAINS { get; } =
        Create(true, 0.0F, 0.5F, 0x3F76E4);
    public static Biome SNOWY_TAIGA { get; } =
        Create(true, -0.5F, 0.4F, 0x3D57D6);
    public static Biome TAIGA { get; } =
        Create(true, 0.25F, 0.8F, 0x3F76E4);
    public static Biome FOREST { get; } =
        Create(true, 0.7F, 0.8F, 0x3F76E4);
    public static Biome OLD_GROWTH_SPRUCE_TAIGA { get; } =
        Create(true, 0.25F, 0.8F, 0x3F76E4);
    public static Biome FLOWER_FOREST { get; } =
        Create(true, 0.7F, 0.8F, 0x3F76E4);
    public static Biome BIRCH_FOREST { get; } =
        Create(true, 0.6F, 0.6F, 0x3F76E4);
    public static Biome DARK_FOREST { get; } =
        Create(true, 0.7F, 0.8F, 0x3F76E4,
            dryFoliageColor: 0x7B5334,
            grassColorModifier: GrassColorModifier.DARK_FOREST);
    public static Biome SAVANNA { get; } =
        Create(false, 2.0F, 0.0F, 0x3F76E4);
    public static Biome JUNGLE { get; } =
        Create(true, 0.95F, 0.9F, 0x3F76E4);
    public static Biome ICE_SPIKES { get; } =
        Create(true, 0.0F, 0.5F, 0x3F76E4);
    public static Biome DAPPLED_FOREST { get; } =
        Create(true, 0.6F, 0.6F, 0x375154,
            foliageColor: 0xE68E30,
            dryFoliageColor: 0x8C3A04,
            grassColor: 0xDF6827);
    public static Biome OLD_GROWTH_PINE_TAIGA { get; } =
        Create(true, 0.3F, 0.8F, 0x3F76E4);
    public static Biome SUNFLOWER_PLAINS { get; } =
        Create(true, 0.8F, 0.4F, 0x3F76E4);
    public static Biome OLD_GROWTH_BIRCH_FOREST { get; } =
        Create(true, 0.6F, 0.6F, 0x3F76E4);
    public static Biome SPARSE_JUNGLE { get; } =
        Create(true, 0.95F, 0.8F, 0x3F76E4);
    public static Biome BAMBOO_JUNGLE { get; } =
        Create(true, 0.95F, 0.9F, 0x3F76E4);
    public static Biome MEADOW { get; } =
        Create(true, 0.5F, 0.8F, 0x0E4ECF);
    public static Biome PALE_GARDEN { get; } =
        Create(true, 0.7F, 0.8F, 0x76889D,
            foliageColor: 0x878D76,
            dryFoliageColor: 0xA0A69C,
            grassColor: 0x778272);
    public static Biome SAVANNA_PLATEAU { get; } =
        Create(false, 2.0F, 0.0F, 0x3F76E4);
    public static Biome BADLANDS { get; } =
        Create(false, 2.0F, 0.0F, 0x3F76E4,
            foliageColor: 0x9E814D,
            grassColor: 0x90814D);
    public static Biome WOODED_BADLANDS { get; } =
        Create(false, 2.0F, 0.0F, 0x3F76E4,
            foliageColor: 0x9E814D,
            grassColor: 0x90814D);
    public static Biome CHERRY_GROVE { get; } =
        Create(true, 0.5F, 0.8F, 0x5DB7EF,
            foliageColor: 0xB6DB61,
            grassColor: 0xB6DB61);
    public static Biome ERODED_BADLANDS { get; } =
        Create(false, 2.0F, 0.0F, 0x3F76E4,
            foliageColor: 0x9E814D,
            grassColor: 0x90814D);
    public static Biome WINDSWEPT_GRAVELLY_HILLS { get; } =
        Create(true, 0.2F, 0.3F, 0x3F76E4);
    public static Biome WINDSWEPT_HILLS { get; } =
        Create(true, 0.2F, 0.3F, 0x3F76E4);
    public static Biome WINDSWEPT_FOREST { get; } =
        Create(true, 0.2F, 0.3F, 0x3F76E4);
    public static Biome MUSHROOM_FIELDS { get; } =
        Create(true, 0.9F, 1.0F, 0x3F76E4);
    public static Biome STONY_SHORE { get; } =
        Create(true, 0.2F, 0.3F, 0x3F76E4);
    public static Biome SWAMP { get; } =
        Create(true, 0.8F, 0.9F, 0x617B64,
            foliageColor: 0x6A7039,
            dryFoliageColor: 0x7B5334,
            grassColorModifier: GrassColorModifier.SWAMP);
    public static Biome MANGROVE_SWAMP { get; } =
        Create(true, 0.8F, 0.9F, 0x3A7A6A,
            foliageColor: 0x8DB127,
            dryFoliageColor: 0x7B5334,
            grassColorModifier: GrassColorModifier.SWAMP);
    public static Biome FROZEN_RIVER { get; } =
        Create(true, 0.0F, 0.5F, 0x3938C9);
    public static Biome RIVER { get; } =
        Create(true, 0.5F, 0.5F, 0x3F76E4);
    public static Biome DRIPSTONE_CAVES { get; } =
        Create(true, 0.8F, 0.4F, 0x3F76E4);
    public static Biome LUSH_CAVES { get; } =
        Create(true, 0.5F, 0.5F, 0x3F76E4);
    public static Biome SULFUR_CAVES { get; } =
        Create(true, 0.8F, 0.4F, 0x34BF89, grassColor: 0xABA64F);
    public static Biome DEEP_DARK { get; } =
        Create(true, 0.8F, 0.4F, 0x3F76E4);
    public static Biome WINDSWEPT_SAVANNA { get; } =
        Create(false, 2.0F, 0.0F, 0x3F76E4);
    public static Biome SNOWY_BEACH { get; } =
        Create(true, 0.05F, 0.3F, 0x3D57D6);
    public static Biome BEACH { get; } =
        Create(true, 0.8F, 0.4F, 0x3F76E4);
    public static Biome JAGGED_PEAKS { get; } =
        Create(true, -0.7F, 0.9F, 0x3F76E4);
    public static Biome FROZEN_PEAKS { get; } =
        Create(true, -0.7F, 0.9F, 0x3F76E4);
    public static Biome STONY_PEAKS { get; } =
        Create(true, 1.0F, 0.3F, 0x3F76E4);
    public static Biome SNOWY_SLOPES { get; } =
        Create(true, -0.3F, 0.9F, 0x3F76E4);
    public static Biome GROVE { get; } =
        Create(true, -0.2F, 0.8F, 0x3F76E4);

    private static readonly (string Name, Biome Value)[] RegisteredBiomes =
    [
        ("plains", PLAINS),
        ("desert", DESERT),
        ("deep_frozen_ocean", DEEP_FROZEN_OCEAN),
        ("deep_cold_ocean", DEEP_COLD_OCEAN),
        ("deep_ocean", DEEP_OCEAN),
        ("deep_lukewarm_ocean", DEEP_LUKEWARM_OCEAN),
        ("warm_ocean", WARM_OCEAN),
        ("frozen_ocean", FROZEN_OCEAN),
        ("cold_ocean", COLD_OCEAN),
        ("ocean", OCEAN),
        ("lukewarm_ocean", LUKEWARM_OCEAN),
        ("snowy_plains", SNOWY_PLAINS),
        ("snowy_taiga", SNOWY_TAIGA),
        ("taiga", TAIGA),
        ("forest", FOREST),
        ("old_growth_spruce_taiga", OLD_GROWTH_SPRUCE_TAIGA),
        ("flower_forest", FLOWER_FOREST),
        ("birch_forest", BIRCH_FOREST),
        ("dark_forest", DARK_FOREST),
        ("savanna", SAVANNA),
        ("jungle", JUNGLE),
        ("ice_spikes", ICE_SPIKES),
        ("dappled_forest", DAPPLED_FOREST),
        ("old_growth_pine_taiga", OLD_GROWTH_PINE_TAIGA),
        ("sunflower_plains", SUNFLOWER_PLAINS),
        ("old_growth_birch_forest", OLD_GROWTH_BIRCH_FOREST),
        ("sparse_jungle", SPARSE_JUNGLE),
        ("bamboo_jungle", BAMBOO_JUNGLE),
        ("meadow", MEADOW),
        ("pale_garden", PALE_GARDEN),
        ("savanna_plateau", SAVANNA_PLATEAU),
        ("badlands", BADLANDS),
        ("wooded_badlands", WOODED_BADLANDS),
        ("cherry_grove", CHERRY_GROVE),
        ("eroded_badlands", ERODED_BADLANDS),
        ("windswept_gravelly_hills", WINDSWEPT_GRAVELLY_HILLS),
        ("windswept_hills", WINDSWEPT_HILLS),
        ("windswept_forest", WINDSWEPT_FOREST),
        ("mushroom_fields", MUSHROOM_FIELDS),
        ("stony_shore", STONY_SHORE),
        ("swamp", SWAMP),
        ("mangrove_swamp", MANGROVE_SWAMP),
        ("frozen_river", FROZEN_RIVER),
        ("river", RIVER),
        ("dripstone_caves", DRIPSTONE_CAVES),
        ("lush_caves", LUSH_CAVES),
        ("sulfur_caves", SULFUR_CAVES),
        ("deep_dark", DEEP_DARK),
        ("windswept_savanna", WINDSWEPT_SAVANNA),
        ("snowy_beach", SNOWY_BEACH),
        ("beach", BEACH),
        ("jagged_peaks", JAGGED_PEAKS),
        ("frozen_peaks", FROZEN_PEAKS),
        ("stony_peaks", STONY_PEAKS),
        ("snowy_slopes", SNOWY_SLOPES),
        ("grove", GROVE)
    ];

    private static Biome Create(
        bool hasPrecipitation,
        float temperature,
        float downfall,
        int waterColor,
        Biome.TemperatureModifier temperatureModifier = Biome.TemperatureModifier.NONE,
        int? foliageColor = null,
        int? dryFoliageColor = null,
        int? grassColor = null,
        GrassColorModifier grassColorModifier = GrassColorModifier.NONE)
    {
        var effects = new BiomeSpecialEffects.Builder().WaterColor(waterColor);
        if (foliageColor.HasValue)
            effects.FoliageColorOverride(foliageColor.Value);
        if (dryFoliageColor.HasValue)
            effects.DryFoliageColorOverride(dryFoliageColor.Value);
        if (grassColor.HasValue)
            effects.GrassColorOverride(grassColor.Value);
        if (grassColorModifier != GrassColorModifier.NONE)
            effects.GrassColorModifier(grassColorModifier);

        return new Biome(
            new Biome.ClimateSettings(hasPrecipitation, temperature, temperatureModifier, downfall),
            effects.Build());
    }

    internal static void RegisterAll()
    {
        foreach ((string name, Biome biome) in RegisteredBiomes)
        {
            Registry.Register(
                BuiltInRegistries.Biome,
                ResourceLocation.FromNamespaceAndPath("minecraft", name),
                biome);
        }
    }
}
