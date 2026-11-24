using System.Text.Json;
using Asteria.Serialization;
using Asteria.Util.Random;
using Asteria.World.LevelGen;

internal static class WeightedListChecks
{
    private sealed class FixedRandomSource : LegacyRandomSource
    {
        private readonly int _selection;
        public int Calls { get; private set; }

        public FixedRandomSource(int selection) : base(0L) => _selection = selection;

        public override int NextInt(int bound)
        {
            Calls++;
            if (_selection < 0 || _selection >= bound)
                throw new InvalidOperationException("Fixed selection is outside requested bound.");
            return _selection;
        }
    }

    public static void Run()
    {
        CheckWeightedValueAndBuilder();
        CheckFlatAndCompactSelection();
        CheckZeroAndInvalidWeights();
        CheckWeightedRandomBoundaries();
        CheckWeightedListCodecs();
    }

    private static void CheckWeightedValueAndBuilder()
    {
        Weighted<int> mapped = new Weighted<string>("stone", 4).Map(static value => value.Length);
        Require(mapped.Value == 5 && mapped.Weight == 4,
            "Weighted.Map must transform only the value and preserve its weight.");

        WeightedList<string> baseList = WeightedList<string>.CreateBuilder()
            .Add("oak")
            .Add("spruce", 3)
            .Build();
        WeightedList<string> combined = WeightedList<string>.CreateBuilder()
            .AddAll(baseList)
            .Add("birch", 2)
            .Build();

        Require(combined.TotalWeight == 6 && combined.Count == 3 &&
                combined[0] == new Weighted<string>("oak", 1) &&
                combined[1] == new Weighted<string>("spruce", 3) &&
                combined[2] == new Weighted<string>("birch", 2),
            "Builder must preserve order, default weight and addAll weights.");

        IList<Weighted<string>> exposed = (IList<Weighted<string>>)combined.Unwrap();
        bool immutable = false;
        try { exposed.Add(new Weighted<string>("ash", 1)); }
        catch (NotSupportedException) { immutable = true; }
        Require(immutable, "Unwrap must expose the immutable item snapshot.");

        WeightedList<int> mappedList = combined.Map(static value => value.Length);
        Require(mappedList.TotalWeight == combined.TotalWeight &&
                mappedList[1] == new Weighted<int>(6, 3),
            "WeightedList.Map must preserve weights and declaration order.");

        Require(combined.Contains("spruce") && !combined.Contains("jungle"),
            "Contains must compare item values rather than entry wrappers.");
        WeightedList<string> copy = WeightedList<string>.FromWeighted(combined.Unwrap());
        Require(combined.Equals(copy) && combined.GetHashCode() == copy.GetHashCode(),
            "Equality and hashing must use total weight and ordered weighted entries.");
    }

    private static void CheckFlatAndCompactSelection()
    {
        WeightedList<string> flat = WeightedList<string>.OfWeighted(
            new Weighted<string>("first", 2),
            new Weighted<string>("second", 61),
            new Weighted<string>("zero", 0));

        Require(flat.TotalWeight == 63 && !flat.IsEmpty,
            "A positive total below 64 must select through the flat selector.");

        for (int selection = 0; selection < 63; selection++)
        {
            var random = new FixedRandomSource(selection);
            string? value = flat.GetRandomOrThrow(random);
            Require(value == (selection < 2 ? "first" : "second") && random.Calls == 1,
                "Flat selection must map weighted intervals exactly.");
        }

        WeightedList<string> compact = WeightedList<string>.OfWeighted(
            new Weighted<string>("first", 63),
            new Weighted<string>("second", 1));
        var compactRandom = new FixedRandomSource(63);

        Require(compact.TotalWeight == 64 &&
                compact.GetRandomOrThrow(compactRandom) == "second" &&
                compactRandom.Calls == 1,
            "A total weight of 64 must use compact cumulative selection.");

        var tryRandom = new FixedRandomSource(0);
        Require(compact.TryGetRandom(tryRandom, out string? selected) && selected == "first",
            "TryGetRandom must return the selected weighted value.");
    }

    private static void CheckZeroAndInvalidWeights()
    {
        WeightedList<string> zero = WeightedList<string>.OfWeighted(
            new Weighted<string>("never", 0),
            new Weighted<string>("also-never", 0));

        var random = new FixedRandomSource(0);
        Require(zero.IsEmpty && !zero.TryGetRandom(random, out _) && random.Calls == 0,
            "A zero-total list is empty and must not consume random values.");

        bool emptyThrows = false;
        try { zero.GetRandomOrThrow(random); }
        catch (InvalidOperationException exception)
        {
            emptyThrows = exception.Message == "Weighted list has no elements";
        }
        Require(emptyThrows && random.Calls == 0,
            "GetRandomOrThrow must use the vanilla empty-list error without random consumption.");

        bool negativeThrows = false;
        try { _ = new Weighted<string>("bad", -1); }
        catch (ArgumentException exception)
        {
            negativeThrows = exception.Message == "Weight should be >= 0";
        }
        Require(negativeThrows, "Weighted must reject negative weights with the vanilla error.");

        RequireThrows<ArgumentException>(
            () => WeightedList<string>.FromWeighted(new[]
            {
                new Weighted<string>("max", int.MaxValue),
                new Weighted<string>("overflow", 1)
            }),
            "WeightedList construction must reject totals above Int32.MaxValue.");
    }

    private static void CheckWeightedRandomBoundaries()
    {
        string[] items = { "one", "two", "three" };
        var random = new FixedRandomSource(3);
        Func<string, int> weight = static item => item == "two" ? 2 : 1;

        Require(WeightedRandom.GetTotalWeight(items, weight) == 4,
            "Total weight must sum into a long before converting to int.");

        Require(WeightedRandom.TryGetRandomItem(random, items, 4, weight, out string? selected) &&
                selected == "three" && random.Calls == 1,
            "Random item selection must call NextInt once, then subtract weights in order.");

        Require(!WeightedRandom.TryGetWeightedItem(items, 4, static _ => 1, out _),
            "An index beyond total weight must produce no item.");

        Require(!WeightedRandom.TryGetRandomItem(random, items, 0, weight, out _) &&
                random.Calls == 1,
            "Zero total weight must return no item without consuming random values.");

        bool overflowThrows = false;
        try
        {
            _ = WeightedRandom.GetTotalWeight(
                new[] { "max", "one" },
                static item => item == "max" ? int.MaxValue : 1);
        }
        catch (ArgumentException exception)
        {
            overflowThrows = exception.Message == "Sum of weights must be <= 2147483647";
        }
        Require(overflowThrows, "Totals above Int32.MaxValue must be rejected.");

        bool negativeTotalThrows = false;
        try { _ = WeightedRandom.TryGetRandomItem(random, items, -1, weight, out _); }
        catch (ArgumentException exception)
        {
            negativeTotalThrows = exception.Message == "Negative total weight in getRandomItem";
        }
        Require(negativeTotalThrows, "Negative supplied totals must use the vanilla error.");
    }

    private static void CheckWeightedListCodecs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Codec<WeightedList<string>> codec = WeightedList<string>.CreateCodec(Codecs.STRING);
        WeightedList<string> expected = WeightedList<string>.OfWeighted(
            new Weighted<string>("oak", 3),
            new Weighted<string>("spruce", 1));

        JsonElement encoded = codec.EncodeStart(ops, expected).GetOrThrow();
        Require(encoded.ValueKind == JsonValueKind.Array &&
                encoded.GetArrayLength() == 2 &&
                encoded[0].GetProperty("data").GetString() == "oak" &&
                encoded[0].GetProperty("weight").GetInt32() == 3,
            "Codec must encode values under data and weights under weight.");

        Require(codec.Parse(ops, encoded).GetOrThrow().Equals(expected),
            "WeightedList Codec must round-trip weighted entries in order.");

        Codec<WeightedList<string>> mapCodec =
            WeightedList<string>.CreateCodec(Codecs.STRING.FieldOf("element"));
        JsonElement mapEncoded = mapCodec.EncodeStart(
            ops, WeightedList<string>.OfWeighted(new Weighted<string>("birch", 2))).GetOrThrow();

        Require(mapEncoded[0].GetProperty("element").GetString() == "birch" &&
                mapEncoded[0].GetProperty("weight").GetInt32() == 2 &&
                !mapEncoded[0].TryGetProperty("data", out _),
            "The MapCodec overload must flatten element fields alongside weight.");

        Require(WeightedList<string>.CreateNonEmptyCodec(Codecs.STRING)
                .Parse(ops, ops.CreateList(Array.Empty<JsonElement>())).IsError,
            "The non-empty codec must reject a zero-total list.");

        JsonElement zeroWeightArray = ops.CreateList(new[]
        {
            ops.CreateMap(new[]
            {
                new KeyValuePair<JsonElement, JsonElement>(ops.CreateString("data"), ops.CreateString("zero")),
                new KeyValuePair<JsonElement, JsonElement>(ops.CreateString("weight"), ops.CreateInt(0))
            })
        });
        Require(WeightedList<string>.CreateNonEmptyCodec(Codecs.STRING)
                .Parse(ops, zeroWeightArray).IsError,
            "The non-empty codec must reject lists whose total selection weight is zero.");

        JsonElement negativeWeight = ops.CreateList(new[]
        {
            ops.CreateMap(new[]
            {
                new KeyValuePair<JsonElement, JsonElement>(ops.CreateString("data"), ops.CreateString("negative")),
                new KeyValuePair<JsonElement, JsonElement>(ops.CreateString("weight"), ops.CreateInt(-1))
            })
        });
        Require(codec.Parse(ops, negativeWeight).IsError,
            "Weighted codec must reject negative values through NON_NEGATIVE_INT.");

        Require(ExtraCodecs.NON_NEGATIVE_INT.Parse(ops, ops.CreateInt(-1)).ErrorMessage ==
                "Value must be non-negative: -1" &&
                ExtraCodecs.NON_NEGATIVE_INT.Parse(ops, ops.CreateInt(0)).GetOrThrow() == 0,
            "NON_NEGATIVE_INT must accept zero and reject negative values with the vanilla error.");
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
