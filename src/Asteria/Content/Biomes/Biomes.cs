using Asteria.Content.Identifiers;
using Asteria.Content.Registries;

namespace Asteria.Content.Biomes;

/// <summary>Built-in climate and visual definitions for the local world.</summary>
public static class Biomes
{
    public static Biome PLAINS { get; } = new(
        new Biome.ClimateSettings(true, 0.8f, Biome.TemperatureModifier.NONE, 0.4f),
        new BiomeSpecialEffects.Builder().WaterColor(4159204).Build());

    public static Biome DESERT { get; } = new(
        new Biome.ClimateSettings(false, 2.0f, Biome.TemperatureModifier.NONE, 0.0f),
        new BiomeSpecialEffects.Builder().WaterColor(4159204).Build());

    internal static void RegisterAll()
    {
        Registry.Register(BuiltInRegistries.Biome, ResourceLocation.FromNamespaceAndPath("asteria", "plains"), PLAINS);
        Registry.Register(BuiltInRegistries.Biome, ResourceLocation.FromNamespaceAndPath("asteria", "desert"), DESERT);
    }
}