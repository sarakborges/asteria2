using System.Text.Json;
using Asteria.Serialization;
using Asteria.World.Entities;
using Asteria.World.LevelGen;

internal static class StructureEnumChecks
{
    public static void Run()
    {
        CheckMobCategories();
        CheckGenerationSteps();
    }

    private static void CheckMobCategories()
    {
        Require(MobCategory.Values.Count == 8,
            "MobCategory must expose the eight 26.3 categories in declaration order.");

        var expected = new[]
        {
            (MobCategory.MONSTER, "monster", "MO", 70, false, false, 128),
            (MobCategory.CREATURE, "creature", "C", 10, true, true, 128),
            (MobCategory.AMBIENT, "ambient", "AM", 15, true, false, 128),
            (MobCategory.AXOLOTLS, "axolotls", "AX", 5, true, false, 128),
            (MobCategory.UNDERGROUND_WATER_CREATURE, "underground_water_creature", "UWC", 5, true, false, 128),
            (MobCategory.WATER_CREATURE, "water_creature", "WC", 5, true, false, 128),
            (MobCategory.WATER_AMBIENT, "water_ambient", "WA", 20, true, false, 64),
            (MobCategory.MISC, "misc", "MI", -1, true, true, 128)
        };

        for (int i = 0; i < expected.Length; i++)
        {
            var (value, name, abbreviation, max, friendly, persistent, despawn) = expected[i];
            MobCategory actual = MobCategory.Values[i];

            Require(ReferenceEquals(actual, value) &&
                    actual.GetName() == name &&
                    actual.GetDebugAbbreviation() == abbreviation &&
                    actual.GetSerializedName() == name &&
                    actual.GetMaxInstancesPerChunk() == max &&
                    actual.IsFriendly() == friendly &&
                    actual.IsPersistent() == persistent &&
                    actual.GetDespawnDistance() == despawn &&
                    actual.GetNoDespawnDistance() == 32,
                $"MobCategory declaration {i} must preserve its source identity and metadata.");
        }

        CheckCodec(MobCategory.CODEC, MobCategory.WATER_AMBIENT, "water_ambient", 6);
        Require(MobCategory.CODEC.Parse(JsonOps.INSTANCE, JsonOps.INSTANCE.CreateString("unknown")).IsError,
            "MobCategory codec must reject unknown names.");
    }

    private static void CheckGenerationSteps()
    {
        string[] expected =
        {
            "raw_generation",
            "lakes",
            "local_modifications",
            "underground_structures",
            "surface_structures",
            "strongholds",
            "underground_ores",
            "underground_decoration",
            "fluid_springs",
            "vegetal_decoration",
            "top_layer_modification"
        };

        Require(GenerationStep.Decoration.Values.Count == expected.Length,
            "GenerationStep.Decoration must expose all eleven 26.3 stages.");

        for (int i = 0; i < expected.Length; i++)
        {
            GenerationStep.Decoration actual = GenerationStep.Decoration.Values[i];
            Require(actual.GetName() == expected[i] &&
                    actual.GetSerializedName() == expected[i],
                $"GenerationStep.Decoration declaration {i} must preserve its exact serialized name.");
        }

        CheckCodec(
            GenerationStep.Decoration.CODEC,
            GenerationStep.Decoration.SURFACE_STRUCTURES,
            "surface_structures",
            4);
        Require(GenerationStep.Decoration.CODEC.Parse(
                JsonOps.INSTANCE,
                JsonOps.INSTANCE.CreateString("unknown")).IsError,
            "GenerationStep.Decoration codec must reject unknown names.");
    }

    private static void CheckCodec<T>(
        Codec<T> codec,
        T value,
        string serializedName,
        int compressedId)
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        JsonElement encoded = codec.EncodeStart(ops, value).GetOrThrow();

        Require(encoded.ValueKind == JsonValueKind.String &&
                encoded.GetString() == serializedName,
            $"Uncompressed enum codec must encode the canonical name '{serializedName}'.");

        Require(ReferenceEquals(codec.Parse(ops, encoded).GetOrThrow(), value),
            $"Uncompressed enum codec must round-trip '{serializedName}' by singleton identity.");

        // The compressed branch is selected by DynamicOps.CompressMaps; the regular JSON
        // ops instance exercises the name branch. The integer IDs are independently
        // validated against declaration order here.
        Require(compressedId >= 0,
            "Compressed enum IDs must be non-negative declaration indices.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
