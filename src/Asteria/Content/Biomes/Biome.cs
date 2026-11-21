using Asteria.Content.Registries;

namespace Asteria.Content.Biomes;

/// <summary>Registry-backed biome climate and visual data.</summary>
public sealed class Biome
{
    public static float GetBiomeInfoNoise(double x, double z) => BiomeInfoNoise.Get(x, z);
    public Holder<Biome>.Reference BuiltInRegistryHolder { get; }
    public ClimateSettings Climate { get; }
    public BiomeSpecialEffects SpecialEffects { get; }
    public bool HasPrecipitation => Climate.HasPrecipitation;
    public float GetBaseTemperature() => Climate.Temperature;
    public float GetDownfall() => Climate.Downfall;

    public Biome(ClimateSettings climate, BiomeSpecialEffects specialEffects)
    {
        Climate = climate;
        SpecialEffects = specialEffects ?? throw new ArgumentNullException(nameof(specialEffects));
        BuiltInRegistryHolder = BuiltInRegistries.Biome.CreateIntrusiveHolder(this);
    }

    public readonly record struct ClimateSettings(bool HasPrecipitation, float Temperature, TemperatureModifier TemperatureModifier, float Downfall);
    public enum Precipitation { NONE, RAIN, SNOW }
    public enum TemperatureModifier { NONE, FROZEN }
}

public static class BiomeEnumExtensions
{
    public static string GetSerializedName(this Biome.Precipitation precipitation) => precipitation switch
    {
        Biome.Precipitation.NONE => "none",
        Biome.Precipitation.RAIN => "rain",
        Biome.Precipitation.SNOW => "snow",
        _ => throw new ArgumentOutOfRangeException(nameof(precipitation), precipitation, "Unknown precipitation type.")
    };

    public static string GetSerializedName(this Biome.TemperatureModifier modifier) => modifier switch
    {
        Biome.TemperatureModifier.NONE => "none",
        Biome.TemperatureModifier.FROZEN => "frozen",
        _ => throw new ArgumentOutOfRangeException(nameof(modifier), modifier, "Unknown temperature modifier.")
    };
}