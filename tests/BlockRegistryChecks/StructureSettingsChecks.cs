using System.Text.Json;
using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Structure;

internal static class StructureSettingsChecks
{
    public static void Run()
    {
        CheckDefaultsAndBuilder();
        CheckCodecRoundTripAndOptionalTerrainAdaptation();
        CheckRequiredFields();
    }

    private static void CheckDefaultsAndBuilder()
    {
        HolderSet<Biome> biomes = HolderSet<Biome>.Direct(Biomes.PLAINS.BuiltInRegistryHolder);

        var defaults = new StructureSettings(biomes);
        Require(ReferenceEquals(defaults.Biomes, biomes) &&
                ReferenceEquals(defaults.Step, GenerationStep.Decoration.SURFACE_STRUCTURES) &&
                ReferenceEquals(defaults.TerrainAdaptation, TerrainAdjustment.NONE),
            "The single-argument settings constructor must use vanilla surface-structure and no-adjustment defaults.");

        StructureSettings built = new StructureSettings.Builder(biomes)
            .GenerationStep(GenerationStep.Decoration.UNDERGROUND_STRUCTURES)
            .TerrainAdapation(TerrainAdjustment.BEARD_THIN)
            .Build();

        Require(ReferenceEquals(built.Biomes, biomes) &&
                ReferenceEquals(built.Step, GenerationStep.Decoration.UNDERGROUND_STRUCTURES) &&
                ReferenceEquals(built.TerrainAdaptation, TerrainAdjustment.BEARD_THIN),
            "The structure-settings builder must preserve biome set, generation step and terrain adjustment.");
    }

    private static void CheckCodecRoundTripAndOptionalTerrainAdaptation()
    {
        DynamicOps<JsonElement> jsonOps = JsonOps.INSTANCE;
        HolderLookupProvider lookup = HolderLookupProvider.Create(
            new IRegistryLookup[] { BuiltInRegistries.Biome });
        RegistryOps<JsonElement> ops = RegistryOps<JsonElement>.Create(jsonOps, lookup);

        HolderSet<Biome> biomes = HolderSet<Biome>.Direct(Biomes.PLAINS.BuiltInRegistryHolder);
        var expected = new StructureSettings(
            biomes,
            GenerationStep.Decoration.VEGETAL_DECORATION,
            TerrainAdjustment.ENCAPSULATE);

        JsonElement encoded = StructureSettings.CODEC.EncodeStart(ops, expected).GetOrThrow();
        Require(encoded.ValueKind == JsonValueKind.Object &&
                ops.Get(encoded, "biomes").GetOrThrow().ValueKind == JsonValueKind.String &&
                ops.Get(encoded, "step").GetOrThrow().GetString() == "vegetal_decoration" &&
                ops.Get(encoded, "terrain_adaptation").GetOrThrow().GetString() == "encapsulate" &&
                !ops.Get(encoded, "spawn_overrides").TryGetResult(out _),
            "StructureSettings must encode biome, step and terrain adaptation without mob spawn overrides.");

        StructureSettings decoded = StructureSettings.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(decoded.Biomes.Count == 1 &&
                ReferenceEquals(decoded.Biomes[0], Biomes.PLAINS.BuiltInRegistryHolder) &&
                ReferenceEquals(decoded.Step, expected.Step) &&
                ReferenceEquals(decoded.TerrainAdaptation, expected.TerrainAdaptation),
            "StructureSettings codec must resolve biome references and preserve enum singleton identities.");

        JsonElement encodedWithoutTerrain = ops.Remove(encoded, "terrain_adaptation");
        StructureSettings decodedDefaulted =
            StructureSettings.CODEC.Parse(ops, encodedWithoutTerrain).GetOrThrow();
        Require(ReferenceEquals(decodedDefaulted.TerrainAdaptation, TerrainAdjustment.NONE),
            "Missing terrain_adaptation must use the vanilla NONE default.");
    }

    private static void CheckRequiredFields()
    {
        DynamicOps<JsonElement> jsonOps = JsonOps.INSTANCE;
        HolderLookupProvider lookup = HolderLookupProvider.Create(
            new IRegistryLookup[] { BuiltInRegistries.Biome });
        RegistryOps<JsonElement> ops = RegistryOps<JsonElement>.Create(jsonOps, lookup);

        JsonElement missingBiome = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("step"),
                ops.CreateString("surface_structures"))
        });
        Require(StructureSettings.CODEC.Parse(ops, missingBiome).IsError,
            "The biome holder set is required by StructureSettings.CODEC.");

        JsonElement missingStep = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("biomes"),
                ops.CreateString("asteria:plains"))
        });
        Require(StructureSettings.CODEC.Parse(ops, missingStep).IsError,
            "The generation step is required by StructureSettings.CODEC.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
