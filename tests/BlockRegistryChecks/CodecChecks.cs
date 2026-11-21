using System.Text.Json;
using Asteria.Serialization;

internal static class CodecChecks
{
    public static void Run()
    {
        CheckPrimitiveCodecs();
        CheckMappedCodecs();
        CheckFieldCodecs();
        CheckListCodecs();
        CheckRangeValidation();
    }

    private static void CheckPrimitiveCodecs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        JsonElement integer = Codecs.INT.EncodeStart(ops, 42).GetOrThrow();
        Require(Codecs.INT.Parse(ops, integer).GetOrThrow() == 42,
            "Integer codecs must round-trip through DynamicOps.");

        JsonElement floating = Codecs.FLOAT.EncodeStart(ops, 1.25F).GetOrThrow();
        Require(Codecs.FLOAT.Parse(ops, floating).GetOrThrow() == 1.25F,
            "Float codecs must round-trip through DynamicOps.");

        JsonElement boolean = Codecs.BOOL.EncodeStart(ops, true).GetOrThrow();
        Require(Codecs.BOOL.Parse(ops, boolean).GetOrThrow(),
            "Boolean codecs must round-trip through DynamicOps.");

        JsonElement text = Codecs.STRING.EncodeStart(ops, "full_moon").GetOrThrow();
        Require(Codecs.STRING.Parse(ops, text).GetOrThrow() == "full_moon",
            "String codecs must round-trip through DynamicOps.");
    }

    private static void CheckMappedCodecs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Codec<int> positive = Codecs.INT.XMap(
            static value => value,
            static value => value);

        Require(positive.Parse(ops, ops.CreateInt(9)).GetOrThrow() == 9,
            "XMap must preserve a reversible type mapping.");

        Codec<int> bounded = Codecs.INT.FlatXMap(
            value => value >= 0
                ? DataResult<int>.Success(value)
                : DataResult<int>.Error("negative"),
            value => value >= 0
                ? DataResult<int>.Success(value)
                : DataResult<int>.Error("negative"));

        Require(bounded.Parse(ops, ops.CreateInt(4)).GetOrThrow() == 4,
            "FlatXMap must decode valid mapped values.");
        Require(bounded.Parse(ops, ops.CreateInt(-4)).IsError,
            "FlatXMap must preserve validation failures.");
    }

    private static void CheckFieldCodecs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        MapCodec<float> probability = Codecs.FloatRange(0.0F, 1.0F).FieldOf("probability");

        JsonElement input = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("probability"),
                ops.CreateFloat(0.75F))
        });

        Require(probability.Parse(ops, input).GetOrThrow() == 0.75F,
            "A field codec must decode a named map field.");

        JsonElement encoded = probability.EncodeStart(ops, 0.25F).GetOrThrow();
        Require(probability.Parse(ops, encoded).GetOrThrow() == 0.25F,
            "A field map codec must encode and decode its own map representation.");

        DataResult<float> missing = probability.Parse(ops, ops.EmptyMap);
        Require(missing.IsError && missing.ErrorMessage!.Contains("probability", StringComparison.Ordinal),
            "A required field codec must report the missing field name.");

        Require(probability.Parse(ops, ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("probability"),
                ops.CreateFloat(1.25F))
        })).IsError,
            "A field codec must retain its underlying value validation.");
    }

    private static void CheckListCodecs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Codec<IReadOnlyList<int>> codec = Codecs.INT.ListOf();

        IReadOnlyList<int> source = new[] { 1, 2, 3 };
        JsonElement encoded = codec.EncodeStart(ops, source).GetOrThrow();
        IReadOnlyList<int> decoded = codec.Parse(ops, encoded).GetOrThrow();

        Require(decoded.SequenceEqual(source),
            "List codecs must preserve value order during encode and decode.");

        Require(Codecs.INT.ListOf(2, 3).Parse(ops, encoded).IsSuccess,
            "List codecs must accept values within the declared length bounds.");

        Require(Codecs.INT.ListOf(4, 6).Parse(ops, encoded).IsError,
            "List codecs must reject values outside the declared length bounds.");
    }

    private static void CheckRangeValidation()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Codec<float> range = Codecs.FloatRange(0.0F, 1.0F);

        Require(range.Parse(ops, ops.CreateFloat(0.0F)).IsSuccess &&
                range.Parse(ops, ops.CreateFloat(1.0F)).IsSuccess,
            "Float range endpoints must be inclusive.");

        Require(range.Parse(ops, ops.CreateFloat(-0.01F)).IsError &&
                range.Parse(ops, ops.CreateFloat(1.01F)).IsError,
            "Float ranges must reject values outside the inclusive interval.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
