using System.Text.Json;
using Asteria.Serialization;

internal static class DynamicOpsChecks
{
    public static void Run()
    {
        CheckPrimitiveValues();
        CheckNumberConversions();
        CheckLists();
        CheckMaps();
        CheckCompressedValues();
        CheckConversion();
    }

    private static void CheckPrimitiveValues()
    {
        JsonOps ops = JsonOps.INSTANCE;

        JsonElement boolean = ops.CreateBoolean(true);
        Require(ops.GetBooleanValue(boolean).GetOrThrow(),
            "JSON boolean values must round-trip.");

        JsonElement integer = ops.CreateInt(24);
        Require(ops.GetNumberValue(integer).GetOrThrow().IntValue == 24,
            "JSON integer values must round-trip.");

        JsonElement floating = ops.CreateFloat(1.25F);
        Require(ops.GetNumberValue(floating).GetOrThrow().FloatValue == 1.25F,
            "JSON floating-point values must round-trip.");

        JsonElement text = ops.CreateString("full_moon");
        Require(ops.GetStringValue(text).GetOrThrow() == "full_moon",
            "JSON string values must round-trip.");

        Require(ops.IsEmpty(ops.Empty) && ops.Empty.ValueKind == JsonValueKind.Null,
            "The JSON empty value must be JSON null.");
    }

    private static void CheckNumberConversions()
    {
        Require(NumberValue.Of(0x1_0000_0001L).IntValue == 1,
            "Long-to-int conversion must retain Java's low 32-bit behavior.");
        Require(NumberValue.Of(1.9D).IntValue == 1 &&
                NumberValue.Of(-1.9D).IntValue == -1,
            "Floating-to-int conversion must truncate toward zero.");
        Require(NumberValue.Of(double.NaN).IntValue == 0 &&
                NumberValue.Of(double.PositiveInfinity).IntValue == int.MaxValue &&
                NumberValue.Of(double.NegativeInfinity).IntValue == int.MinValue,
            "Floating-to-int conversion must match Java NaN and saturation behavior.");

        NumberValue large = NumberValue.Parse("9223372036854775807");
        Require(large.TryGetInt64Exact(out long value) && value == long.MaxValue,
            "Integer parsing must preserve the full Int64 range.");
    }

    private static void CheckLists()
    {
        JsonOps ops = JsonOps.INSTANCE;
        JsonElement emptyList = ops.EmptyList;
        JsonElement first = ops.CreateString("first");
        JsonElement second = ops.CreateString("second");

        JsonElement list = ops.MergeToList(emptyList, first).GetOrThrow();
        list = ops.MergeToList(list, second).GetOrThrow();
        IReadOnlyList<JsonElement> values = ops.GetList(list).GetOrThrow();
        Require(values.Count == 2 &&
                ops.GetStringValue(values[0]).GetOrThrow() == "first" &&
                ops.GetStringValue(values[1]).GetOrThrow() == "second",
            "List merging must preserve insertion order.");

        JsonElement unchanged = ops.MergeToList(list, Array.Empty<JsonElement>()).GetOrThrow();
        Require(unchanged.GetRawText() == list.GetRawText(),
            "Merging an empty list must preserve an existing list.");

        JsonElement invalid = ops.CreateString("not_a_list");
        DataResult<JsonElement> failure = ops.MergeToList(invalid, first);
        Require(failure.IsError && failure.TryGetPartial(out JsonElement partial) &&
                partial.GetRawText() == invalid.GetRawText(),
            "Merging into a non-list must return the original input as a partial result.");
    }

    private static void CheckMaps()
    {
        JsonOps ops = JsonOps.INSTANCE;
        JsonElement emptyMap = ops.EmptyMap;
        JsonElement key = ops.CreateString("phase");
        JsonElement value = ops.CreateString("full_moon");

        JsonElement map = ops.MergeToMap(emptyMap, key, value).GetOrThrow();
        Require(ops.Get(map, "phase").GetOrThrow().GetString() == "full_moon",
            "Map merging and field lookup must round-trip.");

        JsonElement updated = ops.Set(map, "phase", ops.CreateString("new_moon"));
        Require(ops.Get(updated, "phase").GetOrThrow().GetString() == "new_moon",
            "Set must replace an existing map entry.");

        JsonElement removed = ops.Remove(updated, "phase");
        Require(ops.Get(removed, "phase").IsError,
            "Remove must delete the requested map entry.");

        Require(ops.MergeToMap(emptyMap, Array.Empty<KeyValuePair<JsonElement, JsonElement>>())
                .GetOrThrow().ValueKind == JsonValueKind.Object,
            "Merging no map entries into the empty value must produce an empty object.");

        JsonElement invalid = ops.CreateString("not_a_map");
        DataResult<JsonElement> failure = ops.MergeToMap(invalid, key, value);
        Require(failure.IsError && failure.TryGetPartial(out JsonElement partial) &&
                partial.GetRawText() == invalid.GetRawText(),
            "Merging into a non-map must return the original input as a partial result.");
    }

    private static void CheckCompressedValues()
    {
        JsonOps strict = JsonOps.INSTANCE;
        JsonOps compressed = JsonOps.COMPRESSED;
        JsonElement numericString = strict.CreateString("42");
        JsonElement numericValue = strict.CreateInt(42);

        Require(strict.GetNumberValue(numericString).IsError,
            "Strict JSON ops must reject numeric strings as numbers.");
        Require(compressed.GetNumberValue(numericString).GetOrThrow().IntValue == 42,
            "Compressed JSON ops must parse numeric strings.");
        Require(compressed.GetStringValue(numericValue).GetOrThrow() == "42",
            "Compressed JSON ops must permit numeric primitives as strings.");
        Require(!strict.CompressMaps && compressed.CompressMaps,
            "JSON ops variants must retain their compressed-map setting.");
    }

    private static void CheckConversion()
    {
        JsonOps source = JsonOps.INSTANCE;
        JsonOps target = JsonOps.COMPRESSED;

        JsonElement map = source.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                source.CreateString("count"),
                source.CreateLong(120L)),
            new KeyValuePair<JsonElement, JsonElement>(
                source.CreateString("enabled"),
                source.CreateBoolean(true))
        });

        JsonElement converted = source.ConvertTo(target, map);
        Require(target.GetNumberValue(target.Get(converted, "count").GetOrThrow())
                    .GetOrThrow().IntValue == 120,
            "Dynamic conversion must recursively preserve numeric fields.");
        Require(target.GetBooleanValue(target.Get(converted, "enabled").GetOrThrow()).GetOrThrow(),
            "Dynamic conversion must recursively preserve boolean fields.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
