using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.World.LevelGen;

internal static class BiomeGenerationSettingsChecks
{
    public static void Run()
    {
        CheckEmptySettings();
        CheckPlainBuilder();
        CheckBuiltInBiomeCarvers();
    }

    private static void CheckEmptySettings()
    {
        Require(
            !BiomeGenerationSettings.EMPTY.GetCarvers().Any(),
            "BiomeGenerationSettings.EMPTY must expose an empty carver set.");

        var biome = new Biome(
            new Biome.ClimateSettings(
                HasPrecipitation: true,
                Temperature: 0.8f,
                TemperatureModifier: Biome.TemperatureModifier.NONE,
                Downfall: 0.4f),
            new BiomeSpecialEffects.Builder().WaterColor(0x3F76E4).Build());

        Require(
            !biome.GenerationSettings.GetCarvers().Any(),
            "Biomes created without explicit generation settings must use BiomeGenerationSettings.EMPTY.");
    }

    private static void CheckPlainBuilder()
    {
        Holder<WorldCarver> cave = BuiltInRegistries.Carver.GetHolder(Carvers.CAVE) ??
            throw new InvalidOperationException("The built-in cave carver holder must exist.");
        Holder<WorldCarver> canyon = BuiltInRegistries.Carver.GetHolder(Carvers.CANYON) ??
            throw new InvalidOperationException("The built-in canyon carver holder must exist.");

        var builder = new BiomeGenerationSettings.PlainBuilder();
        BiomeGenerationSettings settings = builder
            .AddCarver(cave)
            .AddCarver(canyon)
            .Build();

        Holder<WorldCarver>[] actual = settings.GetCarvers().ToArray();
        Require(
            actual.Length == 2 &&
            ReferenceEquals(actual[0], cave) &&
            ReferenceEquals(actual[1], canyon),
            "The carver-only PlainBuilder must preserve holder identity and insertion order.");
    }

    private static void CheckBuiltInBiomeCarvers()
    {
        Biome[] biomes = BuiltInRegistries.Biome.ToArray();
        Require(
            biomes.Length == BuiltInRegistries.Biome.Count &&
            biomes.Length > 0,
            "The built-in biome registry must be fully enumerable.");

        foreach (Biome biome in biomes)
        {
            Holder<WorldCarver>[] carvers = biome.GenerationSettings.GetCarvers().ToArray();
            Require(
                carvers.Length == 3,
                $"Built-in Overworld biome {BuiltInRegistries.Biome.GetKey(biome)} must have the three default carvers.");

            Require(
                ReferenceEquals(carvers[0].Value, Carvers.CAVE_VALUE) &&
                ReferenceEquals(carvers[1].Value, Carvers.CAVE_EXTRA_UNDERGROUND_VALUE) &&
                ReferenceEquals(carvers[2].Value, Carvers.CANYON_VALUE),
                $"Built-in Overworld biome {BuiltInRegistries.Biome.GetKey(biome)} must preserve default carver order and identity.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
