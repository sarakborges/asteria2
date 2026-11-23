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
        CheckPowCodec();
        CheckLerpCodec();
        CheckCacheCodec();
        CheckInterpolatedCodec();
        CheckClampCodec();
        CheckRangeChoiceCodec();
        CheckIntervalSelectCodec();
        CheckRejectedInputs();
    }

    private static void CheckTypeRegistry()
    {
        Require(
            BuiltInRegistries.DensityFunctionType.Count == 29,
            "The density-function codec slice must register constant, unary/binary/rounding types, pow, lerp, clamp, range choice, interval select, cache and interpolated.");

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

    private static void CheckPowCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        DensityFunction expected = new PowFunction(
            new ConstantFunction(2.5f),
            new ConstantFunction(3.0f));

        JsonElement encoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:pow" &&
            ops.Get(encoded, "base").GetOrThrow().GetSingle() == 2.5f &&
            ops.Get(encoded, "exponent").GetOrThrow().GetSingle() == 3.0f,
            "PowFunction must serialize its base and exponent fields.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(ops, encoded).GetOrThrow().Equals(expected),
            "PowFunction must round-trip through the direct density codec.");
    }

    private static void CheckLerpCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        DensityFunction expected = new LerpFunction(
            new ConstantFunction(0.25f),
            new ConstantFunction(-2.0f),
            new ConstantFunction(6.0f));

        JsonElement encoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:lerp" &&
            ops.Get(encoded, "alpha").GetOrThrow().GetSingle() == 0.25f &&
            ops.Get(encoded, "first").GetOrThrow().GetSingle() == -2.0f &&
            ops.Get(encoded, "second").GetOrThrow().GetSingle() == 6.0f,
            "LerpFunction must use the 26.3 alpha/first/second fields.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(ops, encoded).GetOrThrow().Equals(expected),
            "LerpFunction must round-trip all three child density functions.");
    }

    private static void CheckCacheCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        DensityFunction expected = new CacheFunction(
            new UnaryFunction(
                UnaryFunction.Type.ABS,
                new ConstantFunction(-0.75f)));

        JsonElement encoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:cache" &&
            ops.Get(encoded, "input").GetOrThrow().ValueKind == JsonValueKind.Object,
            "CacheFunction must serialize its child under the 26.3 input field.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(ops, encoded).GetOrThrow().Equals(expected),
            "CacheFunction must round-trip without evaluating its compiler-only cache node.");
    }

    private static void CheckInterpolatedCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        DensityFunction expected = new InterpolatedFunction(
            new UnaryFunction(
                UnaryFunction.Type.SQUARE,
                new ConstantFunction(0.5f)),
            4,
            8);

        JsonElement encoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:interpolated" &&
            ops.Get(encoded, "input").GetOrThrow().ValueKind == JsonValueKind.Object &&
            ops.Get(encoded, "cell_size_xz").GetOrThrow().GetInt32() == 4 &&
            ops.Get(encoded, "cell_size_y").GetOrThrow().GetInt32() == 8,
            "InterpolatedFunction must serialize the 26.3 input and cell-size fields.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(ops, encoded).GetOrThrow().Equals(expected),
            "InterpolatedFunction must round-trip its input and both cell sizes.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"asteria:interpolated","input":1.0,"cell_size_xz":0,"cell_size_y":4}"""))
                .IsError,
            "InterpolatedFunction must reject a non-positive XZ cell size.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"asteria:interpolated","input":1.0,"cell_size_xz":4,"cell_size_y":-1}"""))
                .IsError,
            "InterpolatedFunction must reject a non-positive Y cell size.");
    }

    private static void CheckClampCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        DensityFunction expected = new ClampFunction(
            new ConstantFunction(-4.0f),
            -1.5f,
            2.25f);

        JsonElement encoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:clamp" &&
            ops.Get(encoded, "input").GetOrThrow().GetSingle() == -4.0f &&
            ops.Get(encoded, "min").GetOrThrow().GetSingle() == -1.5f &&
            ops.Get(encoded, "max").GetOrThrow().GetSingle() == 2.25f,
            "ClampFunction must serialize input/min/max using the 26.3 fields.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(ops, encoded).GetOrThrow().Equals(expected),
            "ClampFunction must round-trip its child and bounds.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"asteria:clamp","input":1.0,"min":2.0,"max":1.0}"""))
                .IsError,
            "ClampFunction codec must reject a maximum lower than its minimum.");

        Require(
            DensityFunctions.DIRECT_CODEC.EncodeStart(
                ops,
                new ClampFunction(new ConstantFunction(0.0f), 2.0f, 1.0f))
                .IsError,
            "ClampFunction codec validation must also run during encoding.");
    }

    private static void CheckRangeChoiceCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        DensityFunction expected = new RangeChoiceFunction(
            new ConstantFunction(0.5f),
            -0.25f,
            0.75f,
            new UnaryFunction(UnaryFunction.Type.SQUARE, new ConstantFunction(2.0f)),
            new ConstantFunction(-3.0f));

        JsonElement encoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:range_choice" &&
            ops.Get(encoded, "input").GetOrThrow().GetSingle() == 0.5f &&
            ops.Get(encoded, "min_inclusive").GetOrThrow().GetSingle() == -0.25f &&
            ops.Get(encoded, "max_exclusive").GetOrThrow().GetSingle() == 0.75f &&
            ops.Get(encoded, "when_in_range").GetOrThrow().ValueKind == JsonValueKind.Object &&
            ops.Get(encoded, "when_out_of_range").GetOrThrow().GetSingle() == -3.0f,
            "RangeChoiceFunction must serialize all five vanilla fields.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(ops, encoded).GetOrThrow().Equals(expected),
            "RangeChoiceFunction must round-trip its condition and both result branches.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"asteria:range_choice","input":0.0,"min_inclusive":3.0,"max_exclusive":-3.0,"when_in_range":1.0,"when_out_of_range":2.0}"""))
                .IsSuccess,
            "RangeChoiceFunction must not add validation absent from the 26.3 codec.");
    }

    private static void CheckIntervalSelectCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        IntervalSelectFunction expected = new IntervalSelectFunction(
            new ConstantFunction(0.25f),
            new[] { -0.5f, 0.75f },
            new DensityFunction[]
            {
                new ConstantFunction(-2.0f),
                new UnaryFunction(UnaryFunction.Type.ABS, new ConstantFunction(-3.0f)),
                new ConstantFunction(4.0f)
            });

        JsonElement encoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:interval_select" &&
            ops.Get(encoded, "input").GetOrThrow().GetSingle() == 0.25f &&
            ops.Get(encoded, "thresholds").GetOrThrow().GetArrayLength() == 2 &&
            ops.Get(encoded, "functions").GetOrThrow().GetArrayLength() == 3,
            "IntervalSelectFunction must serialize input, thresholds and the function list.");

        IntervalSelectFunction decoded = DensityFunctions.DIRECT_CODEC
            .Parse(ops, encoded)
            .GetOrThrow() as IntervalSelectFunction
            ?? throw new InvalidOperationException("IntervalSelectFunction codec returned another density-function node.");

        Require(
            decoded.Input.Equals(expected.Input) &&
            decoded.Thresholds.SequenceEqual(expected.Thresholds) &&
            decoded.Functions.SequenceEqual(expected.Functions),
            "IntervalSelectFunction must round-trip its input, threshold sequence and branches.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"asteria:interval_select","input":0.0,"thresholds":[0.0],"functions":[1.0,2.0,3.0]}"""))
                .IsError,
            "IntervalSelectFunction must require exactly one fewer thresholds than functions.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"asteria:interval_select","input":0.0,"thresholds":[1.0,0.0],"functions":[1.0,2.0,3.0]}"""))
                .IsError,
            "IntervalSelectFunction must reject thresholds not ordered by Java Float.compare.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"asteria:interval_select","input":0.0,"thresholds":[0.0,0.0],"functions":[1.0,2.0,3.0]}"""))
                .IsSuccess,
            "IntervalSelectFunction must allow equal, nondecreasing threshold values.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"asteria:interval_select","input":0.0,"thresholds":[],"functions":[1.0]}"""))
                .IsError,
            "IntervalSelectFunction must require at least two branches.");
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
