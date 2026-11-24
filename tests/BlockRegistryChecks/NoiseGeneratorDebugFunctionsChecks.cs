using System.Text.Json;
using Asteria.Serialization;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions.Generator;

internal static class NoiseGeneratorDebugFunctionsChecks
{
    public static void Run()
    {
        CheckEntryCodec();
        CheckFunctionsCodec();
        CheckEmptyDefault();
        CheckInvalidEntries();
    }

    private static void CheckEntryCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var expected = new NoiseGeneratorDebugFunctionEntry(
            "N",
            new ConstantFunction(-0.375f));

        JsonElement encoded = NoiseGeneratorDebugFunctionEntry.CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            encoded.ValueKind == JsonValueKind.Object &&
            ops.Get(encoded, "label").GetOrThrow().GetString() == "N" &&
            ops.Get(encoded, "function").GetOrThrow().GetSingle() == -0.375f,
            "Noise generator debug entries must encode the exact label/function fields.");

        NoiseGeneratorDebugFunctionEntry decoded =
            NoiseGeneratorDebugFunctionEntry.CODEC.Parse(ops, encoded).GetOrThrow();

        Require(
            decoded.Label == expected.Label &&
            decoded.Function.Equals(expected.Function),
            "Noise generator debug entries must round-trip both their label and density function.");
    }

    private static void CheckFunctionsCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var expected = new NoiseGeneratorDebugFunctions(
            new[]
            {
                new NoiseGeneratorDebugFunctionEntry("N", new ConstantFunction(0.25f)),
                new NoiseGeneratorDebugFunctionEntry("T", new ConstantFunction(-0.5f)),
                new NoiseGeneratorDebugFunctionEntry("PS", new ConstantFunction(0.75f))
            });

        JsonElement encoded = NoiseGeneratorDebugFunctions.CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            encoded.ValueKind == JsonValueKind.Array &&
            encoded.GetArrayLength() == 3 &&
            ops.Get(encoded[0], "label").GetOrThrow().GetString() == "N" &&
            ops.Get(encoded[1], "label").GetOrThrow().GetString() == "T" &&
            ops.Get(encoded[2], "label").GetOrThrow().GetString() == "PS",
            "Noise generator debug functions must preserve list order and each entry's label.");

        NoiseGeneratorDebugFunctions decoded = NoiseGeneratorDebugFunctions.CODEC
            .Parse(ops, encoded)
            .GetOrThrow();

        Require(
            decoded.Functions.SequenceEqual(expected.Functions),
            "Noise generator debug functions must round-trip the ordered entry list.");
    }

    private static void CheckEmptyDefault()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        Require(
            NoiseGeneratorDebugFunctions.EMPTY.Functions.Count == 0,
            "Noise generator debug functions must expose the canonical empty default.");

        JsonElement encoded = NoiseGeneratorDebugFunctions.CODEC
            .EncodeStart(ops, NoiseGeneratorDebugFunctions.EMPTY)
            .GetOrThrow();

        Require(
            encoded.ValueKind == JsonValueKind.Array &&
            encoded.GetArrayLength() == 0,
            "The empty noise generator debug-functions value must encode as an empty list.");
    }

    private static void CheckInvalidEntries()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        Require(
            NoiseGeneratorDebugFunctionEntry.CODEC.Parse(
                ops,
                ParseJson("""{"label":"N"}"""))
                .IsError,
            "Noise generator debug entries must require a function field.");

        Require(
            NoiseGeneratorDebugFunctions.CODEC.Parse(
                ops,
                ParseJson("""[{"label":"N","function":0.25},{"function":0.5}]"""))
                .IsError,
            "Noise generator debug-functions lists must reject entries missing their label.");
    }

    private static JsonElement ParseJson(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
