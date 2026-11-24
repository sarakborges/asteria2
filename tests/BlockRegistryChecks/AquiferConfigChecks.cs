using System.Text.Json;
using Asteria.Serialization;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions.Generator;

internal static class AquiferConfigChecks
{
    public static void Run()
    {
        CheckCodecFieldContract();
        CheckCodecRequiresAllInputs();
    }

    private static void CheckCodecFieldContract()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Aquifer.Config expected = new(
            new ConstantFunction(0.125f),
            new ConstantFunction(0.25f),
            new ConstantFunction(0.375f),
            new ConstantFunction(0.5f),
            new ConstantFunction(0.625f),
            new ConstantFunction(0.75f));

        JsonElement encoded = Aquifer.Config.CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            encoded.ValueKind == JsonValueKind.Object &&
            ops.GetMapValues(encoded).GetOrThrow().Count == 6,
            "Aquifer.Config must serialize exactly its six 26.3 density-function fields.");

        Require(
            ops.Get(encoded, "barrier").GetOrThrow().GetSingle() == 0.125f &&
            ops.Get(encoded, "fluid_level_floodedness").GetOrThrow().GetSingle() == 0.25f &&
            ops.Get(encoded, "fluid_level_spread").GetOrThrow().GetSingle() == 0.375f &&
            ops.Get(encoded, "lava").GetOrThrow().GetSingle() == 0.5f &&
            ops.Get(encoded, "exclusion").GetOrThrow().GetSingle() == 0.625f &&
            ops.Get(encoded, "surface_level").GetOrThrow().GetSingle() == 0.75f,
            "Aquifer.Config must preserve the exact 26.3 aquifer field names and their density inputs.");

        Aquifer.Config decoded = Aquifer.Config.CODEC
            .Parse(ops, encoded)
            .GetOrThrow();

        Require(
            decoded == expected,
            "Aquifer.Config must round-trip all six density-function inputs.");
    }

    private static void CheckCodecRequiresAllInputs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        Require(
            Aquifer.Config.CODEC.Parse(
                ops,
                ParseJson(
                    """
                    {
                      "barrier": 0.0,
                      "fluid_level_floodedness": 0.0,
                      "fluid_level_spread": 0.0,
                      "lava": 0.0,
                      "exclusion": 0.0
                    }
                    """))
                .IsError,
            "Aquifer.Config must reject an aquifer definition missing surface_level.");

        Require(
            Aquifer.Config.CODEC.Parse(
                ops,
                ParseJson(
                    """
                    {
                      "barrier": 0.0,
                      "fluid_level_floodedness": 0.0,
                      "fluid_level_spread": 0.0,
                      "lava": 0.0,
                      "exclusion": 0.0,
                      "surface_level": 0.0,
                      "preliminary_surface_level": 1.0
                    }
                    """))
                .IsSuccess,
            "Aquifer.Config must recognize surface_level while ignoring unrelated extra map keys.");
    }

    private static JsonElement ParseJson(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
