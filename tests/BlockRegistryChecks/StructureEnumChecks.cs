using System.Text.Json;
using Asteria.Serialization;
using Asteria.World.LevelGen;

internal static class StructureEnumChecks
{
    public static void Run()
    {
        CheckGenerationSteps();
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

        DynamicOps<JsonElement> compressedOps = JsonOps.COMPRESSED;
        JsonElement compressedEncoded = codec.EncodeStart(compressedOps, value).GetOrThrow();

        Require(compressedEncoded.ValueKind == JsonValueKind.Number &&
                compressedEncoded.GetInt32() == compressedId,
            $"Compressed enum codec must encode '{serializedName}' with its declaration ID.");

        Require(ReferenceEquals(
                codec.Parse(compressedOps, compressedEncoded).GetOrThrow(),
                value),
            $"Compressed enum codec must round-trip '{serializedName}' by declaration ID.");

        Require(codec.Parse(compressedOps, compressedOps.CreateInt(-1)).IsError &&
                codec.Parse(compressedOps, compressedOps.CreateInt(compressedId + 100)).IsError,
            "Compressed enum codec must reject negative and out-of-range declaration IDs.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
