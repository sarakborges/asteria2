using System.Text.Json;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.Util.Random;
using Asteria.World.LevelGen;

internal static class IntProviderChecks
{
    public static void Run()
    {
        CheckTypeRegistry();
        CheckConstantShorthand();
        CheckUniformProvider();
        CheckBiasedProviders();
        CheckClampedProvider();
        CheckClampedNormalProvider();
        CheckTrapezoidProvider();
        CheckWeightedListProvider();
        CheckRangeCodecs();
        CheckInvalidInputs();
    }

    private static void CheckTypeRegistry()
    {
        Require(
            BuiltInRegistries.IntProviderType.Count == 8,
            "The world-generation integer-provider registry must contain the eight Minecraft 26.3 types.");

        string[] identifiers =
        [
            "constant", "uniform", "biased_to_bottom", "very_biased_to_bottom",
            "clamped", "weighted_list", "clamped_normal", "trapezoid"
        ];

        foreach (string identifier in identifiers)
        {
            Require(
                BuiltInRegistries.IntProviderType.ContainsKey(
                    ResourceLocation.Parse($"minecraft:{identifier}")),
                $"The integer-provider registry must expose minecraft:{identifier}.");
        }

        Require(
            !BuiltInRegistries.IntProviderType.ContainsKey(ResourceLocation.Parse("asteria:uniform")),
            "Built-in integer-provider types must retain their Minecraft namespace identifiers.");
    }

    private static void CheckConstantShorthand()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        IntProvider zero = IntProviders.CODEC.Parse(ops, ParseJson("0")).GetOrThrow();
        Require(
            ReferenceEquals(zero, ConstantInt.ZERO),
            "Numeric zero shorthand must resolve to the canonical ConstantInt.ZERO instance.");

        IntProvider expected = ConstantInt.Of(-37);
        JsonElement encoded = IntProviders.CODEC.EncodeStart(ops, expected).GetOrThrow();
        Require(
            encoded.ValueKind == JsonValueKind.Number && encoded.GetInt32() == -37,
            "ConstantInt must encode as an integer instead of a typed object.");

        Require(
            IntProviders.CODEC.Parse(ops, encoded).GetOrThrow().Equals(expected),
            "ConstantInt numeric shorthand must round-trip.");

        JsonElement typed = ConstantInt.MAP_CODEC
            .EncodeStart(ops, (ConstantInt)expected)
            .GetOrThrow();
        Require(
            ops.Get(typed, "value").GetOrThrow().GetInt32() == -37,
            "The registered ConstantInt map codec must use the 26.3 value field.");
    }

    private static void CheckUniformProvider()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var expected = UniformInt.Of(-4, 11);

        JsonElement encoded = IntProviders.CODEC.EncodeStart(ops, expected).GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:uniform" &&
            ops.Get(encoded, "min_inclusive").GetOrThrow().GetInt32() == -4 &&
            ops.Get(encoded, "max_inclusive").GetOrThrow().GetInt32() == 11,
            "UniformInt must serialize the inclusive 26.3 range fields.");

        Require(
            IntProviders.CODEC.Parse(ops, encoded).GetOrThrow().Equals(expected),
            "UniformInt must round-trip through registry dispatch.");

        var expectedRandom = new LegacyRandomSource(123456L);
        int expectedSample = RandomBetweenInclusive(expectedRandom, expected.Minimum, expected.Maximum);
        int actualSample = expected.Sample(new LegacyRandomSource(123456L));

        Require(
            actualSample == expectedSample &&
            actualSample >= expected.Minimum &&
            actualSample <= expected.Maximum,
            "UniformInt must consume one bounded nextInt and sample both inclusive endpoints.");
    }

    private static void CheckBiasedProviders()
    {
        const int minimum = -7;
        const int maximum = 19;
        const long seed = 7654321L;

        var biased = BiasedToBottomInt.Of(minimum, maximum);
        var biasedRandom = new LegacyRandomSource(seed);
        int span = unchecked(maximum - minimum + 1);
        int expectedBiased = unchecked(
            minimum + biasedRandom.NextInt(biasedRandom.NextInt(span) + 1));

        Require(
            biased.Sample(new LegacyRandomSource(seed)) == expectedBiased,
            "BiasedToBottomInt must preserve the two nested bounded-random calls.");

        var veryBiased = VeryBiasedToBottomInt.Of(minimum, maximum);
        var veryBiasedRandom = new LegacyRandomSource(seed);
        int expectedVeryBiased = unchecked(
            minimum +
            veryBiasedRandom.NextInt(
                veryBiasedRandom.NextInt(
                    veryBiasedRandom.NextInt(span) + 1) + 1));

        Require(
            veryBiased.Sample(new LegacyRandomSource(seed)) == expectedVeryBiased,
            "VeryBiasedToBottomInt must preserve the three nested bounded-random calls.");

        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        foreach (IntProvider provider in new IntProvider[] { biased, veryBiased })
        {
            JsonElement encoded = IntProviders.CODEC.EncodeStart(ops, provider).GetOrThrow();
            Require(
                IntProviders.CODEC.Parse(ops, encoded).GetOrThrow().Equals(provider),
                $"{provider.GetType().Name} must round-trip through registry dispatch.");
        }
    }

    private static void CheckClampedProvider()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var expected = ClampedInt.Of(UniformInt.Of(1, 12), 4, 8);

        JsonElement encoded = IntProviders.CODEC.EncodeStart(ops, expected).GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:clamped" &&
            ops.Get(encoded, "source").GetOrThrow().ValueKind == JsonValueKind.Object &&
            ops.Get(encoded, "min_inclusive").GetOrThrow().GetInt32() == 4 &&
            ops.Get(encoded, "max_inclusive").GetOrThrow().GetInt32() == 8,
            "ClampedInt must encode the source provider and both clamp boundaries.");

        IntProvider decoded = IntProviders.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(
            decoded is ClampedInt clamped &&
            clamped.Source.Equals(expected.Source) &&
            clamped.Minimum == expected.Minimum &&
            clamped.Maximum == expected.Maximum,
            "ClampedInt must round-trip its nested source and bounds.");

        Require(
            expected.MinInclusive() == 4 && expected.MaxInclusive() == 8,
            "ClampedInt must intersect its configured bounds with the source provider's effective range.");

        var narrow = ClampedInt.Of(UniformInt.Of(6, 7), 2, 12);
        Require(
            narrow.MinInclusive() == 6 && narrow.MaxInclusive() == 7,
            "ClampedInt's reported range must not exceed the source provider's effective range.");

        var expectedRandom = new LegacyRandomSource(90210L);
        int expectedSample = Math.Clamp(expected.Source.Sample(expectedRandom), expected.Minimum, expected.Maximum);
        int actualSample = expected.Sample(new LegacyRandomSource(90210L));
        Require(
            actualSample == expectedSample,
            "ClampedInt must sample its source once and clamp that result.");
    }

    private static void CheckClampedNormalProvider()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var expected = ClampedNormalInt.Of(2.5f, 3.0f, -2, 5);

        JsonElement encoded = IntProviders.CODEC.EncodeStart(ops, expected).GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:clamped_normal" &&
            ops.Get(encoded, "mean").GetOrThrow().GetSingle() == expected.Mean &&
            ops.Get(encoded, "deviation").GetOrThrow().GetSingle() == expected.Deviation &&
            ops.Get(encoded, "min_inclusive").GetOrThrow().GetInt32() == expected.Minimum &&
            ops.Get(encoded, "max_inclusive").GetOrThrow().GetInt32() == expected.Maximum,
            "ClampedNormalInt must serialize the 26.3 Gaussian and inclusive-bound fields.");

        Require(
            IntProviders.CODEC.Parse(ops, encoded).GetOrThrow().Equals(expected),
            "ClampedNormalInt must round-trip through registry dispatch.");

        var expectedRandom = new LegacyRandomSource(112233L);
        float normal = expected.Mean + (float)expectedRandom.NextGaussian() * expected.Deviation;
        int expectedSample = (int)Mth.Clamp(normal, expected.Minimum, expected.Maximum);
        int actualSample = expected.Sample(new LegacyRandomSource(112233L));
        Require(
            actualSample == expectedSample,
            "ClampedNormalInt must clamp the float normal sample before truncating toward zero.");
    }

    private static void CheckTrapezoidProvider()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var expected = TrapezoidInt.Of(-8, 12, 4);

        JsonElement encoded = IntProviders.CODEC.EncodeStart(ops, expected).GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:trapezoid" &&
            ops.Get(encoded, "min").GetOrThrow().GetInt32() == expected.Minimum &&
            ops.Get(encoded, "max").GetOrThrow().GetInt32() == expected.Maximum &&
            ops.Get(encoded, "plateau").GetOrThrow().GetInt32() == expected.Plateau,
            "TrapezoidInt must serialize min, max and plateau using the 26.3 field names.");

        Require(
            IntProviders.CODEC.Parse(ops, encoded).GetOrThrow().Equals(expected),
            "TrapezoidInt must round-trip through registry dispatch.");

        var expectedRandom = new LegacyRandomSource(334455L);
        int range = expected.Maximum - expected.Minimum;
        int plateauStart = (range - expected.Plateau) / 2;
        int plateauEnd = range - plateauStart;
        int expectedSample = unchecked(
            expected.Minimum +
            RandomBetweenInclusive(expectedRandom, 0, plateauEnd) +
            RandomBetweenInclusive(expectedRandom, 0, plateauStart));
        int actualSample = expected.Sample(new LegacyRandomSource(334455L));

        Require(
            actualSample == expectedSample,
            "TrapezoidInt must preserve the two inclusive subrange samples and their order.");

        var symmetric = TrapezoidInt.Triangle(9);
        var symmetricRandom = new LegacyRandomSource(556677L);
        int expectedTriangle = symmetricRandom.NextInt(10) - symmetricRandom.NextInt(10);
        Require(
            symmetric.Sample(new LegacyRandomSource(556677L)) == expectedTriangle,
            "A zero-plateau symmetric trapezoid must use the vanilla difference-of-uniforms branch.");

        Require(
            expected.ToString() == "trapezoid(4) in [-8-12]",
            "TrapezoidInt diagnostics must match the Minecraft 26.3 string representation.");
    }

    private static void CheckWeightedListProvider()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var distribution = WeightedList<IntProvider>.OfWeighted(
            new Weighted<IntProvider>(ConstantInt.Of(3), 2),
            new Weighted<IntProvider>(UniformInt.Of(5, 7), 1));
        var expected = new WeightedListInt(distribution);

        JsonElement encoded = IntProviders.CODEC.EncodeStart(ops, expected).GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:weighted_list" &&
            ops.Get(encoded, "distribution").GetOrThrow().ValueKind == JsonValueKind.Array,
            "WeightedListInt must encode its non-empty weighted distribution.");

        IntProvider decoded = IntProviders.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(
            decoded is WeightedListInt weighted &&
            weighted.Distribution.Equals(distribution),
            "WeightedListInt must round-trip its weighted provider list.");

        Require(
            expected.MinInclusive() == 3 && expected.MaxInclusive() == 7,
            "WeightedListInt must expose the union of the entry providers' minimum and maximum values.");

        var expectedRandom = new LegacyRandomSource(778899L);
        int selection = expectedRandom.NextInt(distribution.TotalWeight);
        IntProvider selected = selection < 2
            ? ConstantInt.Of(3)
            : UniformInt.Of(5, 7);
        int expectedSample = selected.Sample(expectedRandom);
        int actualSample = expected.Sample(new LegacyRandomSource(778899L));
        Require(
            actualSample == expectedSample,
            "WeightedListInt must first select by weight, then sample only the selected provider.");
    }

    private static void CheckRangeCodecs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        Require(
            IntProviders.Codec(0, 10)
                .Parse(ops, ParseJson("""{"type":"minecraft:uniform","min_inclusive":2,"max_inclusive":9}"""))
                .IsSuccess,
            "Bounded IntProvider codec must accept providers fully contained in the requested range.");

        Require(
            IntProviders.Codec(0, 10)
                .Parse(ops, ParseJson("""{"type":"minecraft:uniform","min_inclusive":-1,"max_inclusive":9}"""))
                .IsError,
            "Bounded IntProvider codec must reject a provider below its minimum.");

        Require(
            IntProviders.Codec(0, 10)
                .Parse(ops, ParseJson("""{"type":"minecraft:uniform","min_inclusive":2,"max_inclusive":11}"""))
                .IsError,
            "Bounded IntProvider codec must reject a provider above its maximum.");

        Require(
            IntProviders.NON_NEGATIVE_CODEC.Parse(ops, ParseJson("-1")).IsError,
            "NON_NEGATIVE_CODEC must reject negative numeric constants.");

        Require(
            IntProviders.POSITIVE_CODEC.Parse(ops, ParseJson("0")).IsError,
            "POSITIVE_CODEC must reject zero numeric constants.");
    }

    private static void CheckInvalidInputs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        Require(
            IntProviders.CODEC.Parse(ops, ParseJson(
                """{"type":"minecraft:uniform","min_inclusive":8,"max_inclusive":7}""")).IsError,
            "UniformInt must reject a reversed inclusive range.");

        Require(
            IntProviders.CODEC.Parse(ops, ParseJson(
                """{"type":"minecraft:biased_to_bottom","min_inclusive":8,"max_inclusive":7}""")).IsError,
            "BiasedToBottomInt must reject a reversed inclusive range.");

        Require(
            IntProviders.CODEC.Parse(ops, ParseJson(
                """{"type":"minecraft:clamped","source":4,"min_inclusive":8,"max_inclusive":7}""")).IsError,
            "ClampedInt must reject a reversed clamp range.");

        Require(
            IntProviders.CODEC.Parse(ops, ParseJson(
                """{"type":"minecraft:clamped_normal","mean":0.0,"deviation":1.0,"min_inclusive":8,"max_inclusive":7}""")).IsError,
            "ClampedNormalInt must reject a reversed inclusive range.");

        Require(
            IntProviders.CODEC.Parse(ops, ParseJson(
                """{"type":"minecraft:trapezoid","min":2,"max":1,"plateau":0}""")).IsError,
            "TrapezoidInt must reject max values below min.");

        Require(
            IntProviders.CODEC.Parse(ops, ParseJson(
                """{"type":"minecraft:trapezoid","min":0,"max":5,"plateau":6}""")).IsError,
            "TrapezoidInt must reject a plateau larger than the full span.");

        Require(
            IntProviders.CODEC.Parse(ops, ParseJson(
                """{"type":"minecraft:weighted_list","distribution":[]}""")).IsError,
            "WeightedListInt must reject an empty distribution.");

        Require(
            IntProviders.CODEC.Parse(ops, ParseJson(
                """{"type":"minecraft:missing"}""")).IsError,
            "The dispatch codec must reject unknown integer-provider types.");
    }

    private static int RandomBetweenInclusive(IRandomSource random, int minimum, int maximum) =>
        unchecked(random.NextInt(unchecked(maximum - minimum + 1)) + minimum);

    private static JsonElement ParseJson(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
