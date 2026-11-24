using System.Text.Json;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.Util;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.DensityFunctions.Operations;
using Asteria.World.LevelGen.Synth;

internal static class DensityFunctionCodecChecks
{
    public static void Run()
    {
        CheckTypeRegistry();
        CheckScalarConstantCodec();
        CheckSimpleDensityFunctionCodecs();
        CheckTypedConstantCodec();
        CheckUnaryCodecs();
        CheckBinaryCodecs();
        CheckRoundCodecs();
        CheckPowCodec();
        CheckLerpCodec();
        CheckCacheCodec();
        CheckBlendDensityCodec();
        CheckInterpolatedCodec();
        CheckClampCodec();
        CheckRangeChoiceCodec();
        CheckIntervalSelectCodec();
        CheckSliceCodec();
        CheckGradientCodec();
        CheckSplineCodec();
        CheckDistanceToPointCodec();
        CheckEndIslandCodec();
        CheckNormalNoiseCodec();
        CheckNoiseFunctionCodec();
        CheckShiftNoiseCodecs();
        CheckFindTopSurfaceCodec();
        CheckOldBlendedNoiseCodec();
        CheckHolderCodecs();
        CheckRejectedInputs();
    }

    private static void CheckTypeRegistry()
    {
        Require(
            BuiltInRegistries.DensityFunctionType.Count == 44,
            "The density-function codec slice must register constant, noise, shift variants, find_top_surface, old_blended_noise, unary/binary/rounding types, pow, lerp, clamp, range choice, interval select, cache, interpolated, slice, gradient, spline, distance_to_point and end_outer_islands.");

        Require(
            BuiltInRegistries.DensityFunctionType.ContainsKey(ResourceLocation.Parse("minecraft:constant")) &&
            BuiltInRegistries.DensityFunctionType.ContainsKey(ResourceLocation.Parse("minecraft:abs")) &&
            BuiltInRegistries.DensityFunctionType.ContainsKey(ResourceLocation.Parse("minecraft:add")) &&
            BuiltInRegistries.DensityFunctionType.ContainsKey(ResourceLocation.Parse("minecraft:floor")) &&
            BuiltInRegistries.DensityFunctionType.ContainsKey(ResourceLocation.Parse("minecraft:blend_density")),
            "Density-function type registration must expose the Asteria-namespaced 26.3 type identifiers.");

        Require(
            !BuiltInRegistries.DensityFunctionType.ContainsKey(ResourceLocation.Parse("asteria:constant")),
            "Minecraft namespace identifiers must not enter the Asteria runtime registry.");
    }

    private static void CheckSimpleDensityFunctionCodecs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        foreach (SimpleDensityFunction expected in SimpleDensityFunction.Values)
        {
            JsonElement encoded = DensityFunctions.DIRECT_CODEC
                .EncodeStart(ops, expected)
                .GetOrThrow();

            Require(
                encoded.ValueKind == JsonValueKind.Object &&
                ops.Get(encoded, "type").GetOrThrow().GetString() == $"minecraft:{expected.Id}" &&
                ops.GetMapValues(encoded).GetOrThrow().Count == 1,
                $"The {expected.Id} simple density function must encode as a type-only density-function object.");

            DensityFunction decoded = DensityFunctions.DIRECT_CODEC
                .Parse(ops, encoded)
                .GetOrThrow();

            Require(
                ReferenceEquals(decoded, expected),
                $"The {expected.Id} simple density function codec must decode to its canonical singleton.");
        }
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
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:constant" &&
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
                    $"minecraft:{UnaryFunction.GetId(operation)}",
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
                    $"minecraft:{BinaryFunction.GetId(operation)}",
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
                    $"minecraft:{RoundFunction.GetId(operation)}",
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
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:pow" &&
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
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:lerp" &&
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
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:cache" &&
            ops.Get(encoded, "input").GetOrThrow().ValueKind == JsonValueKind.Object,
            "CacheFunction must serialize its child under the 26.3 input field.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(ops, encoded).GetOrThrow().Equals(expected),
            "CacheFunction must round-trip without evaluating its compiler-only cache node.");
    }

    private static void CheckBlendDensityCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        DensityFunction expected = new BlendDensityFunction(
            new UnaryFunction(
                UnaryFunction.Type.ABS,
                new ConstantFunction(-0.75f)));

        JsonElement encoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:blend_density" &&
            ops.Get(encoded, "input").GetOrThrow().ValueKind == JsonValueKind.Object,
            "BlendDensityFunction must serialize its child under the 26.3 input field.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(ops, encoded).GetOrThrow().Equals(expected),
            "BlendDensityFunction must round-trip its wrapped density function.");
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
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:interpolated" &&
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
                ParseJson("""{"type":"minecraft:interpolated","input":1.0,"cell_size_xz":0,"cell_size_y":4}"""))
                .IsError,
            "InterpolatedFunction must reject a non-positive XZ cell size.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:interpolated","input":1.0,"cell_size_xz":4,"cell_size_y":-1}"""))
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
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:clamp" &&
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
                ParseJson("""{"type":"minecraft:clamp","input":1.0,"min":2.0,"max":1.0}"""))
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
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:range_choice" &&
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
                ParseJson("""{"type":"minecraft:range_choice","input":0.0,"min_inclusive":3.0,"max_exclusive":-3.0,"when_in_range":1.0,"when_out_of_range":2.0}"""))
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
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:interval_select" &&
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
                ParseJson("""{"type":"minecraft:interval_select","input":0.0,"thresholds":[0.0],"functions":[1.0,2.0,3.0]}"""))
                .IsError,
            "IntervalSelectFunction must require exactly one fewer thresholds than functions.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:interval_select","input":0.0,"thresholds":[1.0,0.0],"functions":[1.0,2.0,3.0]}"""))
                .IsError,
            "IntervalSelectFunction must reject thresholds not ordered by Java Float.compare.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:interval_select","input":0.0,"thresholds":[0.0,0.0],"functions":[1.0,2.0,3.0]}"""))
                .IsSuccess,
            "IntervalSelectFunction must allow equal, nondecreasing threshold values.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:interval_select","input":0.0,"thresholds":[],"functions":[1.0]}"""))
                .IsError,
            "IntervalSelectFunction must require at least two branches.");
    }

    private static void CheckSliceCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        DensityFunction expected = new SliceFunction(
            Direction.Axis.Z,
            -128,
            new UnaryFunction(
                UnaryFunction.Type.ABS,
                new ConstantFunction(-0.5f)));

        JsonElement encoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:slice" &&
            ops.Get(encoded, "axis").GetOrThrow().GetString() == "z" &&
            ops.Get(encoded, "coordinate").GetOrThrow().GetInt32() == -128 &&
            ops.Get(encoded, "input").GetOrThrow().ValueKind == JsonValueKind.Object,
            "SliceFunction must serialize axis, coordinate and input with the 26.3 field names.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(ops, encoded).GetOrThrow().Equals(expected),
            "SliceFunction must round-trip its axis, coordinate and input.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:slice","axis":"w","coordinate":0,"input":1.0}"""))
                .IsError,
            "SliceFunction must reject axes outside x/y/z.");
    }

    private static void CheckGradientCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        DensityFunction expected = new GradientFunction(
            Direction.Axis.Y,
            TilingMode.MIRRORED_REPEAT,
            -64,
            320,
            -1.25f,
            2.5f);

        JsonElement encoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:gradient" &&
            ops.Get(encoded, "axis").GetOrThrow().GetString() == "y" &&
            ops.Get(encoded, "tiling").GetOrThrow().GetString() == "mirrored_repeat" &&
            ops.Get(encoded, "from_coordinate").GetOrThrow().GetInt32() == -64 &&
            ops.Get(encoded, "to_coordinate").GetOrThrow().GetInt32() == 320 &&
            ops.Get(encoded, "from_value").GetOrThrow().GetSingle() == -1.25f &&
            ops.Get(encoded, "to_value").GetOrThrow().GetSingle() == 2.5f,
            "GradientFunction must serialize all 26.3 fields and the tiling name.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(ops, encoded).GetOrThrow().Equals(expected),
            "GradientFunction must round-trip axis, tiling, coordinates and values.");

        DensityFunction defaultTiling = DensityFunctions.DIRECT_CODEC
            .Parse(
                ops,
                ParseJson("""{"type":"minecraft:gradient","axis":"x","from_coordinate":1,"to_coordinate":4,"from_value":0.0,"to_value":1.0}"""))
            .GetOrThrow();

        Require(
            defaultTiling is GradientFunction { Tiling: TilingMode.CLAMP_TO_EDGE },
            "GradientFunction must default omitted tiling to CLAMP_TO_EDGE.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:gradient","axis":"x","from_coordinate":2,"to_coordinate":2,"from_value":0.0,"to_value":1.0}"""))
                .IsError,
            "GradientFunction must reject equal from/to coordinates.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:gradient","axis":"x","tiling":"wrap","from_coordinate":0,"to_coordinate":1,"from_value":0.0,"to_value":1.0}"""))
                .IsError,
            "GradientFunction must reject unknown tiling modes.");
    }

    private static void CheckSplineCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        var coordinate = new SplineFunction.Coordinate(
            new UnaryFunction(
                UnaryFunction.Type.ABS,
                new ConstantFunction(-0.375f)));

        CubicSpline<SplineFunction.Unit> nested =
            CubicSpline<SplineFunction.Unit>.CreateBuilder(coordinate)
                .AddPoint(-1.0f, -0.5f)
                .AddPoint(1.0f, 0.5f)
                .Build();

        CubicSpline<SplineFunction.Unit> spline =
            CubicSpline<SplineFunction.Unit>.CreateBuilder(coordinate)
                .AddPoint(-2.0f, -1.0f, 0.25f)
                .AddPoint(0.0f, nested)
                .AddPoint(2.0f, 1.0f, -0.25f)
                .Build();

        var expected = new SplineFunction(spline);

        JsonElement encoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        JsonElement splineData = ops.Get(encoded, "spline").GetOrThrow();
        JsonElement points = ops.Get(splineData, "points").GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:spline" &&
            ops.Get(splineData, "coordinate").GetOrThrow().ValueKind == JsonValueKind.Object &&
            points.GetArrayLength() == 3,
            "SplineFunction must serialize its coordinate and non-empty point list using the 26.3 shape.");

        JsonElement middlePoint = points[1];
        Require(
            ops.Get(middlePoint, "location").GetOrThrow().GetSingle() == 0.0f &&
            ops.Get(middlePoint, "value").GetOrThrow().ValueKind == JsonValueKind.Object,
            "Spline point values must recursively encode nested multipoint splines.");

        SplineFunction decoded = DensityFunctions.DIRECT_CODEC
            .Parse(ops, encoded)
            .GetOrThrow() as SplineFunction
            ?? throw new InvalidOperationException("Spline codec returned another density-function node.");

        Require(
            decoded.Spline.ParityString() == expected.Spline.ParityString() &&
            decoded.Spline.Range().Equals(expected.Spline.Range()),
            "SplineFunction must round-trip the recursive spline structure, coordinates and derivatives.");

        var constantExpected = new SplineFunction(
            CubicSpline<SplineFunction.Unit>.CreateConstant(0.625f));
        JsonElement constantEncoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(ops, constantExpected)
            .GetOrThrow();

        Require(
            ops.Get(constantEncoded, "spline").GetOrThrow().GetSingle() == 0.625f,
            "A constant cubic spline must use the scalar branch of the recursive codec.");

        SplineFunction constantDecoded = DensityFunctions.DIRECT_CODEC
            .Parse(ops, constantEncoded)
            .GetOrThrow() as SplineFunction
            ?? throw new InvalidOperationException("Constant spline codec returned another density-function node.");

        Require(
            constantDecoded.Spline.ParityString() == constantExpected.Spline.ParityString(),
            "Constant cubic splines must round-trip through the scalar branch.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:spline","spline":{"coordinate":0.0,"points":[]}}"""))
                .IsError,
            "Multipoint spline codecs must reject empty point lists.");
    }

    private static void CheckDistanceToPointCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var expected = new DistanceToPointFunction(
            new Vec3i(10, -20, 30),
            DistanceMetric.MANHATTAN);

        JsonElement encoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        JsonElement point = ops.Get(encoded, "point").GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:distance_to_point" &&
            point.GetArrayLength() == 3 &&
            point[0].GetInt32() == 10 &&
            point[1].GetInt32() == -20 &&
            point[2].GetInt32() == 30 &&
            ops.Get(encoded, "metric").GetOrThrow().GetString() == "manhattan",
            "DistanceToPointFunction must use the Vec3i int-stream and named metric fields.");

        DistanceToPointFunction decoded = DensityFunctions.DIRECT_CODEC
            .Parse(ops, encoded)
            .GetOrThrow() as DistanceToPointFunction
            ?? throw new InvalidOperationException("Distance-to-point codec returned another density-function node.");

        Require(
            decoded.Point.Equals(expected.Point) &&
            decoded.Metric == expected.Metric,
            "DistanceToPointFunction must round-trip its fixed point and metric.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:distance_to_point","point":[1,2],"metric":"euclidean"}"""))
                .IsError,
            "Vec3i codec must reject input lists that do not contain exactly three integers.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:distance_to_point","point":[1,2,3],"metric":"taxicab"}"""))
                .IsError,
            "DistanceMetric codec must reject unknown serialized names.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:distance_to_point","point":[1,2,3],"metric":"euclidean_squared"}"""))
                .IsSuccess,
            "DistanceMetric codec must accept the full canonical enum name.");

        Require(
            expected.Range().Min() == 0.0f &&
            float.IsPositiveInfinity(expected.Range().Max()),
            "DistanceToPointFunction range must be [0, positive infinity].");

        Require(
            DistanceMetric.EUCLIDEAN.Compute(3.0f, -4.0f, 12.0f) == 13.0f &&
            DistanceMetric.EUCLIDEAN_SQUARED.Compute(3.0f, -4.0f, 12.0f) == 169.0f &&
            DistanceMetric.MANHATTAN.Compute(3.0f, -4.0f, 12.0f) == 19.0f &&
            DistanceMetric.CHEBYSHEV.Compute(3.0f, -4.0f, 12.0f) == 12.0f,
            "DistanceMetric must preserve all four vanilla three-axis distance calculations.");

        Require(
            DistanceMetric.EUCLIDEAN.Compute(3.0f, 4.0f) == 5.0f &&
            DistanceMetric.EUCLIDEAN_SQUARED.Compute(3.0f, 4.0f) == 25.0f &&
            DistanceMetric.MANHATTAN.Compute(3.0f, 4.0f) == 7.0f &&
            DistanceMetric.CHEBYSHEV.Compute(3.0f, 4.0f) == 4.0f,
            "DistanceMetric must preserve all four horizontal distance calculations.");
    }

    private static void CheckEndIslandCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var expected = new EndIslandFunction();

        JsonElement encoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:end_outer_islands" &&
            encoded.EnumerateObject().Count() == 1,
            "EndIslandFunction must use the vanilla unit map codec without extra fields.");

        DensityFunction decoded = DensityFunctions.DIRECT_CODEC
            .Parse(ops, encoded)
            .GetOrThrow();

        Require(
            decoded is EndIslandFunction &&
            decoded.Range().Min() == -0.84375f &&
            decoded.Range().Max() == 0.5625f &&
            decoded.DomainAxes() == (DensityFunction.AXIS_X | DensityFunction.AXIS_Z) &&
            ReferenceEquals(decoded.RewriteChildren(static function => function), decoded),
            "EndIslandFunction must preserve its vanilla range, horizontal axes and leaf rewrite behavior.");

        var compileContext = new EndIslandCompileContext();
        DensitySampler sampler = expected.CompileSampler(compileContext);
        SamplerContext samplerContext = SamplerContext.EmptyUncached;

        foreach ((int x, int z) in new[] { (-1024, -512), (0, 0), (512, 1024), (4096, -2048) })
        {
            float value = sampler.SampleValue(samplerContext, x, 0, z);
            Require(
                value >= expected.Range().Min() && value <= expected.Range().Max(),
                "End-island samples must remain inside the exact vanilla density range.");
        }

        var volume = new DensityVolume(
            sizeX: 2,
            sizeY: 3,
            sizeZ: 2,
            minBlockX: -64,
            minBlockY: -2,
            minBlockZ: 96);
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        sampler.SampleVolume(samplerContext, output, volume);

        for (int z = 0; z < volume.SizeZ; z++)
        {
            for (int x = 0; x < volume.SizeX; x++)
            {
                float expectedColumn = sampler.SampleValue(
                    samplerContext,
                    volume.BlockX(x),
                    0,
                    volume.BlockZ(z));

                for (int y = 0; y < volume.SizeY; y++)
                {
                    Require(
                        output.Get(volume.IndexUnchecked(x, y, z)) == expectedColumn,
                        "End-island volume sampling must write one identical value through every Y cell.");
                }
            }
        }
    }

    private static void CheckNormalNoiseCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        NormalNoise expected = NormalNoise.CreateBuilder()
            .SetBaseAmplitude(0.75)
            .SetBaseOctave(-4)
            .SetOctaveCount(3)
            .SetLegacyNormalization()
            .SetAmplitudeModifier(0, 1.0)
            .SetAmplitudeModifier(1, 0.5)
            .SetAmplitudeModifier(2, 0.0)
            .Build();

        JsonElement encoded = NormalNoise.DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "base_amplitude").GetOrThrow().GetDouble() == 0.75 &&
            ops.Get(encoded, "base_octave").GetOrThrow().GetInt32() == -4 &&
            ops.Get(encoded, "octave_count").GetOrThrow().GetInt32() == 3 &&
            ops.Get(encoded, "normalize").GetOrThrow().GetString() == "legacy" &&
            ops.Get(encoded, "amplitude_modifiers").GetOrThrow().GetArrayLength() == 3,
            "NormalNoise must encode the 26.3 parameter fields and legacy normalization spelling.");

        NormalNoise decoded = NormalNoise.DIRECT_CODEC
            .Parse(ops, encoded)
            .GetOrThrow();

        Require(
            decoded.Equals(expected),
            "NormalNoise parameters must round-trip through DIRECT_CODEC.");

        NormalNoise defaults = NormalNoise.DIRECT_CODEC
            .Parse(ops, ParseJson("""{"base_octave":0}"""))
            .GetOrThrow();

        Require(
            defaults.Equals(NormalNoise.CreateBuilder().Build()),
            "NormalNoise must apply base amplitude, octave count, normalization and amplitude modifier defaults.");

        Require(
            NormalNoise.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"base_octave":0,"normalize":false}"""))
                .GetOrThrow()
                .Equals(NormalNoise.CreateBuilder().SetNormalize(false).Build()),
            "NormalNoise normalization false must map to DISABLED.");

        Require(
            NormalNoise.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"base_octave":0,"normalize":true}"""))
                .GetOrThrow()
                .Equals(NormalNoise.CreateBuilder().Build()),
            "NormalNoise normalization true must map to ENABLED.");

        Require(
            NormalNoise.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"base_octave":0,"normalize":"enabled"}"""))
                .IsError,
            "NormalNoise normalization strings other than legacy must be rejected.");

        Require(
            NormalNoise.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"base_octave":0,"octave_count":2,"amplitude_modifiers":[1.0]}"""))
                .IsError,
            "NormalNoise must reject a non-empty amplitude-modifier list whose size differs from octave_count.");

        Require(
            NormalNoise.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"base_octave":0,"base_amplitude":0.000001}"""))
                .IsError &&
            NormalNoise.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"base_octave":0,"base_amplitude":1000001.0}"""))
                .IsError,
            "NormalNoise must enforce the vanilla base-amplitude range.");

        Require(
            NormalNoise.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"base_octave":33}"""))
                .IsError &&
            NormalNoise.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"base_octave":0,"octave_count":0}"""))
                .IsError,
            "NormalNoise must enforce the base-octave and octave-count ranges.");

        Require(
            NormalNoise.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"base_octave":0,"amplitude_modifiers":[-0.1]}"""))
                .IsError &&
            NormalNoise.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"base_octave":0,"amplitude_modifiers":[1000001.0]}"""))
                .IsError,
            "NormalNoise must enforce the amplitude-modifier range.");
    }

    private static void CheckNoiseFunctionCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        NormalNoise noise = NormalNoise.CreateBuilder()
            .SetBaseOctave(-2)
            .SetOctaveCount(3)
            .Build();

        NoiseFunction expected = new(
            Holder<NormalNoise>.Direct(noise),
            2.0,
            0.5,
            DensityFunctions.Zero(),
            new ConstantFunction(0.25f),
            DensityFunctions.Zero());

        JsonElement encoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:noise" &&
            ops.Get(encoded, "noise").GetOrThrow().ValueKind == JsonValueKind.Object &&
            ops.Get(encoded, "xz_scale").GetOrThrow().GetDouble() == 2.0 &&
            ops.Get(encoded, "y_scale").GetOrThrow().GetDouble() == 0.5,
            "NoiseFunction must encode its holder and coordinate scales with the 26.3 field names.");

        NoiseFunction decoded = DensityFunctions.DIRECT_CODEC
            .Parse(ops, encoded)
            .GetOrThrow() as NoiseFunction
            ?? throw new InvalidOperationException("NoiseFunction codec returned another density-function node.");

        Require(
            decoded.NoiseData.Kind == HolderKind.Direct &&
            decoded.NoiseData.Value.Equals(noise) &&
            decoded.XzScale == expected.XzScale &&
            decoded.YScale == expected.YScale &&
            decoded.ShiftX.Equals(expected.ShiftX) &&
            decoded.ShiftY.Equals(expected.ShiftY) &&
            decoded.ShiftZ.Equals(expected.ShiftZ),
            "NoiseFunction must round-trip its NormalNoise holder, scales and all three shifts.");

        NoiseFunction defaults = DensityFunctions.DIRECT_CODEC
            .Parse(
                ops,
                ParseJson("""{"type":"minecraft:noise","noise":{"base_octave":0},"xz_scale":1.25,"y_scale":0.75}"""))
            .GetOrThrow() as NoiseFunction
            ?? throw new InvalidOperationException("NoiseFunction defaults decoded into another node type.");

        Require(
            defaults.ShiftX.Equals(DensityFunctions.Zero()) &&
            defaults.ShiftY.Equals(DensityFunctions.Zero()) &&
            defaults.ShiftZ.Equals(DensityFunctions.Zero()),
            "NoiseFunction must default omitted shift_x/shift_y/shift_z to DensityFunctions.zero().");
    }

    private static void CheckShiftNoiseCodecs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Holder<NormalNoise> noise = Holder<NormalNoise>.Direct(
            NormalNoise.CreateBuilder().SetBaseOctave(-3).Build());

        DensityFunction[] values =
        {
            new ShiftNoiseFunction.ShiftA(noise),
            new ShiftNoiseFunction.ShiftB(noise),
            new ShiftNoiseFunction.Shift(noise)
        };
        string[] identifiers = { "minecraft:shift_a", "minecraft:shift_b", "minecraft:shift" };

        for (int index = 0; index < values.Length; index++)
        {
            JsonElement encoded = DensityFunctions.DIRECT_CODEC
                .EncodeStart(ops, values[index])
                .GetOrThrow();

            Require(
                ops.Get(encoded, "type").GetOrThrow().GetString() == identifiers[index] &&
                ops.Get(encoded, "noise").GetOrThrow().ValueKind == JsonValueKind.Object,
                $"Shift-noise codec {identifiers[index]} must encode the holder under noise.");

            DensityFunction decoded = DensityFunctions.DIRECT_CODEC
                .Parse(ops, encoded)
                .GetOrThrow();

            Require(
                decoded.Equals(values[index]),
                $"Shift-noise codec {identifiers[index]} must round-trip its holder.");
        }
    }

    private static void CheckFindTopSurfaceCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        FindTopSurfaceFunction expected = new(
            new ConstantFunction(0.25f),
            new ConstantFunction(11.0f),
            -8,
            4);

        JsonElement encoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:find_top_surface" &&
            ops.Get(encoded, "density").GetOrThrow().ValueKind == JsonValueKind.Number &&
            ops.Get(encoded, "upper_bound").GetOrThrow().GetSingle() == 11.0f &&
            ops.Get(encoded, "lower_bound").GetOrThrow().GetInt32() == -8 &&
            ops.Get(encoded, "cell_height").GetOrThrow().GetInt32() == 4,
            "FindTopSurfaceFunction must serialize density, upper_bound, lower_bound and cell_height.");

        FindTopSurfaceFunction decoded = DensityFunctions.DIRECT_CODEC
            .Parse(ops, encoded)
            .GetOrThrow() as FindTopSurfaceFunction
            ?? throw new InvalidOperationException("FindTopSurfaceFunction codec returned another density-function node.");

        Require(
            decoded.UpperBound.Equals(expected.UpperBound) &&
            decoded.LowerBound == expected.LowerBound &&
            decoded.CellHeight == expected.CellHeight,
            "FindTopSurfaceFunction must preserve its upper bound and vertical search settings.");
        Require(
            decoded.Density.Equals(expected.Density),
            "FindTopSurfaceFunction must preserve its density child.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:find_top_surface","density":1.0,"upper_bound":1.0,"lower_bound":-4065,"cell_height":1}"""))
                .IsError &&
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:find_top_surface","density":1.0,"upper_bound":1.0,"lower_bound":4063,"cell_height":1}"""))
                .IsError,
            "FindTopSurfaceFunction must reject lower bounds outside twice the DimensionType Y limits.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:find_top_surface","density":1.0,"upper_bound":1.0,"lower_bound":0,"cell_height":0}"""))
                .IsError &&
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:find_top_surface","density":1.0,"upper_bound":1.0,"lower_bound":0,"cell_height":-4}"""))
                .IsError,
            "FindTopSurfaceFunction must reject non-positive cell heights.");

        Require(
            expected.Range().Equals(Interval.Of(-8.0f, 11.0f)) &&
            expected.DomainAxes() == DensityFunction.NO_AXES,
            "FindTopSurfaceFunction range and domain must use the upper-bound range and clear Y axes.");

        DensityFunction unchanged = expected.RewriteChildren(static child => child);
        Require(
            ReferenceEquals(unchanged, expected),
            "FindTopSurfaceFunction rewrite must preserve identity when neither child changes.");

        DensityFunction rewritten = expected.RewriteChildren(
            child => ReferenceEquals(child, expected.Density)
                ? new SurfaceProbeDensityFunction(9.0f)
                : child);
        Require(
            rewritten is FindTopSurfaceFunction rewrittenNode &&
            rewrittenNode.CellHeight == 4 &&
            rewrittenNode.LowerBound == -8 &&
            rewrittenNode.Density is SurfaceProbeDensityFunction { Threshold: 9.0f },
            "FindTopSurfaceFunction rewrite must replace the density child and preserve search settings.");

        var context = new TestCompileContext();
        DensityFunction density = new SurfaceProbeDensityFunction(5.0f);
        DensityFunction runtimeFunction = new FindTopSurfaceFunction(
            density,
            new ConstantFunction(11.0f),
            -8,
            4);
        DensitySampler sampler = runtimeFunction.CompileSampler(context);
        Require(
            sampler.SampleValue(SamplerContext.EmptyUncached, 2, 100, -3) == 8.0f,
            "FindTopSurfaceFunction must begin at floor(upper_bound/cell_height)*cell_height and return the first positive probe.");

        DensityFunction belowSurface = new FindTopSurfaceFunction(
            density,
            new ConstantFunction(3.0f),
            -8,
            4);
        Require(
            belowSurface.CompileSampler(context)
                .SampleValue(SamplerContext.EmptyUncached, 0, 200, 0) == -8.0f,
            "FindTopSurfaceFunction must return lower_bound when no probe is positive.");

        var volume = new DensityVolume(2, 3, 2, -2, 30, 5);
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        sampler.SampleVolume(SamplerContext.EmptyUncached, output, volume);
        for (int z = 0; z < volume.SizeZ; z++)
        for (int x = 0; x < volume.SizeX; x++)
        for (int y = 0; y < volume.SizeY; y++)
        {
            Require(
                output.Get(volume.IndexUnchecked(x, y, z)) == 8.0f,
                "FindTopSurfaceFunction volume sampling must replicate the sampled Y=0 surface across the input Y axis.");
        }

    }

    private sealed record SurfaceProbeDensityFunction(float Threshold) : DensityFunction
    {
        public DensitySampler CompileSampler(DensityFunction.CompileContext context) =>
            new SurfaceProbeSampler(Threshold);

        public DensityFunction RewriteChildren(DfRewriteRule rule) => this;

        public Interval Range() =>
            Interval.Of(-10_000.0f, 10_000.0f);

        public int DomainAxes() =>
            DensityFunction.AXIS_Y;
    }

    private sealed record SurfaceProbeSampler(float Threshold) : DensitySampler
    {
        public float SampleValue(
            SamplerContext context,
            int blockX,
            int blockY,
            int blockZ) =>
            blockY - Threshold;

        public void SampleVolume(
            SamplerContext context,
            DensityBuffer outputBuffer,
            DensityVolume volume)
        {
            int index = 0;
            for (int z = 0; z < volume.SizeZ; z++)
            for (int x = 0; x < volume.SizeX; x++)
            for (int y = 0; y < volume.SizeY; y++)
                outputBuffer.Set(
                    index++,
                    volume.BlockY(y) - Threshold);
        }
    }

    private sealed class TestCompileContext : DensityFunction.CompileContext
    {
        public Asteria.World.LevelGen.Synth.Noise CreateNoiseSampler(
            Holder<NormalNoise> parameters) =>
            throw new InvalidOperationException("FindTopSurfaceFunction tests must not resolve normal noise.");

        public Asteria.World.LevelGen.IRandomSource CreateRandom(ResourceLocation seed) =>
            throw new InvalidOperationException("FindTopSurfaceFunction tests must not resolve random streams.");

#pragma warning disable CS0618
        public Asteria.World.LevelGen.IRandomSource CreateEndIslandRandom() =>
            throw new InvalidOperationException("FindTopSurfaceFunction tests must not resolve End Island random streams.");
#pragma warning restore CS0618
    }

    private static void CheckOldBlendedNoiseCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        BlendedNoise expected = new(
            XzScale: 0.75,
            YScale: 1.25,
            XzFactor: 8.0,
            YFactor: 4.0,
            SmearScaleMultiplier: 2.5);

        JsonElement encoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:old_blended_noise" &&
            ops.Get(encoded, "xz_scale").GetOrThrow().GetDouble() == 0.75 &&
            ops.Get(encoded, "y_scale").GetOrThrow().GetDouble() == 1.25 &&
            ops.Get(encoded, "xz_factor").GetOrThrow().GetDouble() == 8.0 &&
            ops.Get(encoded, "y_factor").GetOrThrow().GetDouble() == 4.0 &&
            ops.Get(encoded, "smear_scale_multiplier").GetOrThrow().GetDouble() == 2.5,
            "BlendedNoise must encode all five 26.3 old_blended_noise fields.");

        BlendedNoise decoded = DensityFunctions.DIRECT_CODEC
            .Parse(ops, encoded)
            .GetOrThrow() as BlendedNoise
            ?? throw new InvalidOperationException("old_blended_noise codec returned another density-function node.");

        Require(
            decoded == expected,
            "BlendedNoise must round-trip all scale and factor parameters.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:old_blended_noise","xz_scale":0.0009,"y_scale":1.0,"xz_factor":1.0,"y_factor":1.0,"smear_scale_multiplier":1.0}"""))
                .IsError &&
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:old_blended_noise","xz_scale":1000.1,"y_scale":1.0,"xz_factor":1.0,"y_factor":1.0,"smear_scale_multiplier":1.0}"""))
                .IsError,
            "BlendedNoise must enforce the inclusive [0.001,1000] scale and factor ranges.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:old_blended_noise","xz_scale":1.0,"y_scale":1.0,"xz_factor":1.0,"y_factor":1.0,"smear_scale_multiplier":0.99}"""))
                .IsError &&
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:old_blended_noise","xz_scale":1.0,"y_scale":1.0,"xz_factor":1.0,"y_factor":1.0,"smear_scale_multiplier":8.01}"""))
                .IsError,
            "BlendedNoise must enforce the inclusive [1,8] smear scale multiplier range.");

        Require(
            expected.DomainAxes() == DensityFunction.ALL_AXES &&
            expected.Range().Min() < 0.0f &&
            expected.Range().Max() > 0.0f,
            "BlendedNoise must expose all coordinate axes and its symmetric limit-noise range.");

        Require(
            ReferenceEquals(expected.RewriteChildren(static child => child), expected),
            "BlendedNoise has no density-function children and must preserve identity under rewriting.");

        BlendedNoise.FbmSet fbms = expected.CreateFbmSet(
            new XoroshiroRandomSource(0x1234_5678_9ABC_DEF0L));
        Require(
            fbms.MinLimitNoise.GetLayer(0) is SmearedPerlinNoise &&
            fbms.MaxLimitNoise.GetLayer(0) is SmearedPerlinNoise &&
            fbms.MainNoise.GetLayer(0) is SmearedPerlinNoise,
            "BlendedNoise must construct all three octave stacks from SmearedPerlinNoise.");

        bool positiveFirstOctaveRejected = false;
        try
        {
            _ = BlendedNoise.CreateFbm(
                new XoroshiroRandomSource(1L),
                firstOctave: 1,
                smearScaleY: 1.0,
                valueFactor: 1.0);
        }
        catch (ArgumentException exception) when (
            exception.Message.Contains("firstOctave>0", StringComparison.Ordinal))
        {
            positiveFirstOctaveRejected = true;
        }

        Require(
            positiveFirstOctaveRejected,
            "BlendedNoise.createFbm must reject a positive first octave.");

        var context = new BlendedNoiseCompileContext();
        DensitySampler contextualSampler = expected.CompileSampler(context);
        DensitySampler explicitlySeededSampler = expected.CompileSampler(
            new XoroshiroRandomSource(0x5A17_2026L));

        foreach ((int x, int y, int z) in new[]
        {
            (-32, -64, 48),
            (0, 0, 0),
            (37, 91, -113)
        })
        {
            float contextualValue = contextualSampler.SampleValue(
                SamplerContext.EmptyUncached, x, y, z);
            float explicitValue = explicitlySeededSampler.SampleValue(
                SamplerContext.EmptyUncached, x, y, z);

            Require(
                float.IsFinite(contextualValue) &&
                contextualValue == explicitValue,
                "BlendedNoise.compileSampler must use the compile context's deterministic terrain random stream.");
        }

        var volume = new DensityVolume(2, 3, 2, -8, -16, 24, 2, 4, 3);
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        contextualSampler.SampleVolume(SamplerContext.EmptyUncached, output, volume);

        for (int z = 0; z < volume.SizeZ; z++)
        for (int x = 0; x < volume.SizeX; x++)
        for (int y = 0; y < volume.SizeY; y++)
        {
            float expectedValue = contextualSampler.SampleValue(
                SamplerContext.EmptyUncached,
                volume.BlockX(x),
                volume.BlockY(y),
                volume.BlockZ(z));
            float actual = output.Get(volume.IndexUnchecked(x, y, z));
            Require(
                MathF.Abs(actual - expectedValue) <= 0.0002f,
                "BlendedNoise volume sampling must agree with point evaluation for each sampled block.");
        }
    }

    private sealed class BlendedNoiseCompileContext : DensityFunction.CompileContext
    {
        public Asteria.World.LevelGen.Synth.Noise CreateNoiseSampler(
            Holder<NormalNoise> parameters) =>
            throw new InvalidOperationException("BlendedNoise must not resolve registered NormalNoise holders.");

        public Asteria.World.LevelGen.IRandomSource CreateRandom(ResourceLocation seed) =>
            new XoroshiroRandomSource(0x5A17_2026L);

#pragma warning disable CS0618
        public Asteria.World.LevelGen.IRandomSource CreateEndIslandRandom() =>
            throw new InvalidOperationException("BlendedNoise must not resolve End Island random streams.");
#pragma warning restore CS0618
    }

    private static void CheckHolderCodecs()
    {
        DynamicOps<JsonElement> jsonOps = JsonOps.INSTANCE;
        var registry = new Registry<DensityFunction>(Registries.DENSITY_FUNCTION.Location);
        RegistryKey<DensityFunction> entryKey = RegistryKey<DensityFunction>.Create(
            Registries.DENSITY_FUNCTION,
            ResourceLocation.Parse("minecraft:shared_density"));
        DensityFunction registeredValue = new BinaryFunction(
            BinaryFunction.Type.ADD,
            new ConstantFunction(1.25f),
            new ConstantFunction(2.5f));

        Holder<DensityFunction>.Reference registeredHolder = registry.Register(
            entryKey,
            registeredValue,
            RegistrationInfo.BuiltIn);

        Require(
            !registeredHolder.IsBound,
            "Standalone registry holders must remain unbound until the registry is frozen.");

        registry.Freeze();

        Require(
            registeredHolder.IsBound && ReferenceEquals(registeredHolder.Value, registeredValue),
            "Registry.Freeze must bind the registered value into its reference holder.");

        HolderLookupProvider lookupProvider =
            HolderLookupProvider.Create(new IRegistryLookup[] { registry });
        RegistryOps<JsonElement> registryOps =
            RegistryOps<JsonElement>.Create(jsonOps, lookupProvider);

        JsonElement referenceEncoded = DensityFunction.REFERENCE_CODEC
            .EncodeStart(registryOps, registeredHolder)
            .GetOrThrow();

        Require(
            referenceEncoded.ValueKind == JsonValueKind.String &&
            referenceEncoded.GetString() == "minecraft:shared_density",
            "REFERENCE_CODEC must encode a registry reference as its identifier.");

        Holder<DensityFunction> referenceDecoded = DensityFunction.REFERENCE_CODEC
            .Parse(registryOps, referenceEncoded)
            .GetOrThrow();

        Require(
            referenceDecoded.Kind == HolderKind.Reference &&
            ReferenceEquals(referenceDecoded, registeredHolder),
            "REFERENCE_CODEC must resolve identifiers to the registry's canonical holder instance.");

        Require(
            DensityFunction.REFERENCE_CODEC.Parse(registryOps, ParseJson("1.0")).IsError,
            "REFERENCE_CODEC must reject inline numeric density values.");

        Require(
            DensityFunction.REFERENCE_CODEC.Parse(
                jsonOps,
                ParseJson("\"" + "minecraft:shared_density" + "\"")).IsError,
            "REFERENCE_CODEC must reject identifiers when no registry context is available.");

        Require(
            DensityFunction.REFERENCE_CODEC.Parse(
                registryOps,
                ParseJson("\"" + "minecraft:missing_density" + "\"")).IsError,
            "REFERENCE_CODEC must reject identifiers missing from the selected registry.");

        JsonElement inlineEncoded = DensityFunction.CODEC
            .EncodeStart(registryOps, new ConstantFunction(3.75f))
            .GetOrThrow();

        Require(
            inlineEncoded.ValueKind == JsonValueKind.Number &&
            inlineEncoded.GetSingle() == 3.75f,
            "DensityFunction.CODEC must preserve inline constant encoding when registry context is present.");

        DensityFunction inlineDecoded = DensityFunction.CODEC
            .Parse(registryOps, inlineEncoded)
            .GetOrThrow();

        Require(
            inlineDecoded is ConstantFunction { Value: 3.75f },
            "DensityFunction.CODEC must decode inline definitions into direct density nodes.");

        JsonElement holderEncoded = DensityFunction.CODEC
            .EncodeStart(registryOps, new DensityFunctions.HolderHolder(registeredHolder))
            .GetOrThrow();

        Require(
            holderEncoded.ValueKind == JsonValueKind.String &&
            holderEncoded.GetString() == "minecraft:shared_density",
            "DensityFunction.CODEC must serialize HolderHolder as its registered identifier.");

        DensityFunctions.HolderHolder holderDecoded = DensityFunction.CODEC
            .Parse(registryOps, holderEncoded)
            .GetOrThrow() as DensityFunctions.HolderHolder
            ?? throw new InvalidOperationException("DensityFunction.CODEC did not preserve a registered holder.");

        Require(
            ReferenceEquals(holderDecoded.Function, registeredHolder),
            "DensityFunction.CODEC must reconstruct HolderHolder around the canonical registry reference.");

        var parentGraph = new BinaryFunction(
            BinaryFunction.Type.MUL,
            new DensityFunctions.HolderHolder(registeredHolder),
            new ConstantFunction(-4.0f));

        JsonElement graphEncoded = DensityFunctions.DIRECT_CODEC
            .EncodeStart(registryOps, parentGraph)
            .GetOrThrow();

        Require(
            registryOps.Get(graphEncoded, "left").GetOrThrow().GetString() == "minecraft:shared_density" &&
            registryOps.Get(graphEncoded, "right").GetOrThrow().GetSingle() == -4.0f,
            "Density graph child codecs must encode references by key and direct children inline.");

        BinaryFunction graphDecoded = DensityFunctions.DIRECT_CODEC
            .Parse(registryOps, graphEncoded)
            .GetOrThrow() as BinaryFunction
            ?? throw new InvalidOperationException("Density graph decode returned another node type.");

        Require(
            graphDecoded.Left is DensityFunctions.HolderHolder leftReference &&
            ReferenceEquals(leftReference.Function, registeredHolder) &&
            graphDecoded.Right is ConstantFunction { Value: -4.0f },
            "Density graph decoding must preserve registered child holders while decoding inline siblings.");

        Require(
            DensityFunction.CODEC.Parse(
                jsonOps,
                ParseJson("""{"type":"minecraft:add","left":1.0,"right":2.0}"""))
                .IsSuccess,
            "DensityFunction.CODEC must allow inline definitions when no RegistryOps context exists.");

        Codec<Holder<DensityFunction>> inlineDisabled = RegistryCodecs.Holder(
            Registries.DENSITY_FUNCTION,
            DensityFunctions.DIRECT_CODEC,
            allowInline: false);

        Require(
            inlineDisabled.Parse(
                registryOps,
                ParseJson("""{"type":"minecraft:constant","value":1.0}"""))
                .IsError,
            "Registry file codecs must reject inline definitions when allowInline is false in registry context.");

        Require(
            inlineDisabled.Parse(
                jsonOps,
                ParseJson("""{"type":"minecraft:constant","value":1.0}"""))
                .IsSuccess,
            "Registry file codecs without RegistryOps must follow vanilla inline fallback behavior.");

        var foreignRegistry = new Registry<DensityFunction>(
            ResourceLocation.FromNamespaceAndPath("asteria", "foreign_density"));
        RegistryKey<Registry<DensityFunction>> foreignRegistryKey =
            RegistryKey<DensityFunction>.CreateRegistryKey(foreignRegistry.Location);
        RegistryKey<DensityFunction> foreignEntryKey =
            RegistryKey<DensityFunction>.Create(
                foreignRegistryKey,
                ResourceLocation.Parse("minecraft:foreign"));
        Holder<DensityFunction>.Reference foreignHolder = foreignRegistry.Register(
            foreignEntryKey,
            new ConstantFunction(9.0f),
            RegistrationInfo.BuiltIn);

        Require(
            DensityFunction.CODEC.EncodeStart(
                registryOps,
                new DensityFunctions.HolderHolder(foreignHolder))
                .IsError,
            "Registry file codecs must reject references owned by a different registry.");
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
                ParseJson("""{"type":"minecraft:abs","argument":1.0}""")).IsError,
            "The old unary argument field must not be accepted in the 26.3 codec.");

        Require(
            DensityFunctions.DIRECT_CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:unknown","input":1.0}""")).IsError,
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
    private sealed class EndIslandCompileContext : DensityFunction.CompileContext
    {
        public Noise CreateNoiseSampler(Holder<NormalNoise> parameters) =>
            throw new NotSupportedException("EndIslandFunction does not request registered normal-noise holders.");

        public IRandomSource CreateRandom(ResourceLocation seed) =>
            throw new NotSupportedException("EndIslandFunction uses the dedicated end-island random source.");

        public IRandomSource CreateEndIslandRandom() =>
            new LegacyRandomSource(0x5EEDL);
    }
}
