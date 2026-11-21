using Asteria.Content.Identifiers;
using Asteria.Content.Registries;

namespace Asteria.Content.Biomes;

/// <summary>
/// Built-in climate definitions used by the local world. Registry keys, not object names,
/// identify biome values, matching the holder-backed registry model.
/// </summary>
public static class Biomes
{
    public static Biome PLAINS { get; } = new(
        new Biome.ClimateSettings(
            HasPrecipitation: true,
            Temperature: 0.8f,
            TemperatureModifier: Biome.TemperatureModifier.NONE,
            Downfall: 0.4f));

    public static Biome DESERT { get; } = new(
        new Biome.ClimateSettings(
            HasPrecipitation: false,
            Temperature: 2.0f,
            TemperatureModifier: Biome.TemperatureModifier.NONE,
            Downfall: 0.0f));

    internal static void RegisterAll()
    {
        Registry.Register(
            BuiltInRegistries.Biome,
            ResourceLocation.FromNamespaceAndPath("asteria", "plains"),
            PLAINS);
        Registry.Register(
            BuiltInRegistries.Biome,
            ResourceLocation.FromNamespaceAndPath("asteria", "desert"),
            DESERT);
    }
}
