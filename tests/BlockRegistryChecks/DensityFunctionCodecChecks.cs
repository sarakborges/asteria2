using System.Text.Json;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.DensityFunctions.Operations;

internal static class DensityFunctionCodecChecks
{
    public static void Run()
    {
        CheckTypeRegistry();
        CheckScalarConstantCodec();
        CheckTypedConstantCodec();
        CheckUnaryCodecs();
        CheckBinaryCodecs();
        CheckRoundCodecs();
        CheckRejectedInputs();
    }

    private static void CheckTypeRegistry()
    {
        Require(
            BuiltInRegistries.DensityFunctionType.Count == 22,
            "The first density-function codec slice must register constant, every unary, every binary and every rounding type.");

        Require(
            BuiltInRegistries.DensityFunctionType.ContainsKey(ResourceLocation.Parse("asteria:constant")) &&
            BuiltInRegistries.DensityFunctionType.ContainsKey(ResourceLocation.Parse("asteria:abs")) &&
            BuiltInRegistries.DensityFunctionType.ContainsKey(ResourceLocation.Parse("asteria:add")) &&
            BuiltInRegistries.DensityFunctionType.ContainsKey(ResourceLocation.Parse("asteria:floor")),
            "Density-function type registration must expose the Asteria-namespaced 26.3 type identifiers.");

        Require(
            !BuiltInRegistries.DensityFunctionType.ContainsKey(ResourceLocation.Parse("minecraft:constant")),
            "Minecraft namespace identifiers must not enter the Asteria runtime registry.");
    }

    private static void CheckScalarConstantCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        DensityFunction expected = new ConstantFunction(37.25f);

        JsonElement encoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            encoded.ValueKind == JsonValueKind.Number &&
            encoded.GetSingle() == 37.25f,
            "DIRECT_CODEC must encode ConstantFunction as a scalar noise value.");

        DensityFunction decoded = DensityFunctions.DIRECT_CODEC
            .Parse(ops, encoded)
            .GetOrThrow();

        Require(
            decoded.Equals(expected),
            "DIRECT_CODEC must decode scalar noise values as ConstantFunction.");

        Require(
            DensityFunctions.NOISE_VALUE_CODEC.Parse(ops, ParseJson("1000000")).IsSuccess &&
            DensityFunctions.NOISE_VALUE_CODEC.Parse(ops, ParseJson("-1000000")).IsSuccess,
            "The density scalar codec must include both ±1,000,000 boundary values.");
    }

    private static void CheckTypedConstantCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var expected = new ConstantFunction(-12.5f);

        JsonElement encoded = DensityFunctions.FULL_DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:constant" &&
            ops.Get(encoded, "value").GetOrThrow().GetSingle() == -12.5f,
            "The registered constant map codec must use the 26.3 value field.");

        ConstantFunction decoded = DensityFunctions.FULL_DIRECT_CODEC
            .Parse(ops, encoded)
            .GetOrThrow() as ConstantFunction
            ?? throw new InvalidOperationException("The typed constant codec returned another density-function node.");

        Require(decoded == expected, "The registered constant map codec must round-trip its value.");
    }

    private static void CheckUnaryCodecs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        foreach (UnaryFunction.Type operation in Enum.GetValues<UnaryFunction.Type>())
        {
            DensityFunction expected = new UnaryFunction(
                operation,
                new ConstantFunction(-0.375f));

            JsonElement encoded = DensityFunctions.DIRECT_CODEC
                .EncodeStart(ops, expected)
                .GetOrThrow();

            Require(
                ops.Get(encoded, "type").GetOrThrow().GetString() ==
                    $"asteria:{UnaryFunction.GetId(operation)}",
                $"The unary {operation} codec must dispatch through its registered type ID.");

            Require(
                ops.Get(encoded, "input").GetOrThrow().GetSingle() == -0.375f,
                $"The unary {operation} codec must use the 26.3 input field.");

            DensityFunction decoded = DensityFunctions.DIRECT_CODEC
                .Parse(ops, encoded)
                .GetOrThrow();

            Require(
                decoded.Equals(expected),
                $"The unary {operation} codec must round-trip the operation and its child.");
        }
    }

    private static void CheckBinaryCodecs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        foreach (BinaryFunction.Type operation in Enum.GetValues<BinaryFunction.Type>())
        {
            DensityFunction expected = new BinaryFunction(
                operation,
                new ConstantFunction(2.25f),
                new ConstantFunction(-4.5f));

            JsonElement encoded = DensityFunctions.DIRECT_CODEC
                .EncodeStart(ops, expected)
                .GetOrThrow();

            Require(
                ops.Get(encoded, "type").GetOrThrow().GetString() ==
                    $"asteria:{BinaryFunction.GetId(operation)}",
                $"The binary {operation} codec must dispatch through its registered type ID.");

            Require(
                ops.Get(encoded, "left").GetOrThrow().GetSingle() == 2.25f &&
                ops.Get(encoded, "right").GetOrThrow().GetSingle() == -4.5f,
                $"The binary {operation} codec must use the 26.3 left/right fields.");

            DensityFunction decoded = DensityFunctions.DIRECT_CODEC
                .Parse(ops, encoded)
                .GetOrThrow();

            Require(
                decoded.Equals(expected),
                $"The binary {operation} codec must round-trip both child functions.");
        }
    }

    private static void CheckRoundCodecs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        foreach (RoundFunction.Type operation in Enum.GetValues<RoundFunction.Type>())
        {
            DensityFunction expected = new RoundFunction(
                operation,
                new ConstantFunction(2.75f),
                new ConstantFunction(0.5f));

            JsonElement encoded = DensityFunctions.DIRECT_CODEC
                .EncodeStart(ops, expected)
                .GetOrThrow();

            Require(
                ops.Get(encoded, "type").GetOrThrow().GetString() ==
                    $"asteria:{RoundFunction.GetId(operation)}",
                $"The rounding {operation} codec must dispatch through its registered type ID.");

            Require(
                ops.Get(encoded, "input").GetOrThrow().GetSingle() == 2.75f &&
                ops.Get(encoded, "multiple").GetOrThrow().GetSingle() == 0.5f,
                $"The rounding {operation} codec must preserve input and multiple.");

            DensityFunction decoded = DensityFunctions.DIRECT_CODEC
                .Parse(ops, encoded)
                .GetOrThrow();

            Require(
                decoded.Equals(expected),
                $"The rounding {operation} codec must round-trip its input and multiple.");
        }

        var defaultMultiple = new RoundFunction(
            RoundFunction.Type.FLOOR,
            new ConstantFunction(-1.25f),
            new ConstantFunction(1.0f));
        JsonElement defaultEncoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(ops, defaultMultiple)
            .GetOrThrow();
        DensityFunction defaultDecoded = DensityFunctions.DIRECT_CODEC
            .Parse(ops, defaultEncoded)
            .GetOrThrow();

        Require(
            defaultDecoded.Equals(defaultMultiple),
            "The rounding codec must preserve the default multiple of one when its field is omitted or emitted.");
    }

    private static void CheckRejectedInputs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        Require(
            DensityFunctions.NOISE_VALUE_CODEC.Parse(ops, ParseJson("1000001")).IsError,
            "Density scalar values above the 26.3 maximum must be rejected.");

        Require(
            DensityFunctions.NOISE_VALUE_CODEC.Parse(ops, ParseJson("-1000001")).IsError,
            "Density scalar values below the 26.3 minimum must be rejected.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"asteria:abs","argument":1.0}""")).IsError,
            "The old unary argument field must not be accepted in the 26.3 codec.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"asteria:unknown","input":1.0}""")).IsError,
            "Unknown density-function type IDs must fail dispatch.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:add","left":1.0,"right":2.0}""")).IsError,
            "The density-function codec must reject foreign Minecraft namespace identifiers.");
    }

    private static JsonElement ParseJson(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
