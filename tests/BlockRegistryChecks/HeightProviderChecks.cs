using System.Text.Json;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.Util.Random;
using Asteria.World.LevelGen;

internal static class HeightProviderChecks
{
    public static void Run()
    {
        CheckTypeRegistry();
        CheckConstantAnchorShorthand();
        CheckUniformHeightCodecAndSampling();
        CheckBiasedToBottomSampling();
        CheckVeryBiasedToBottomSampling();
        CheckTrapezoidSampling();
        CheckWeightedListHeight();
        CheckInvalidRanges();
    }

    private static void CheckTypeRegistry()
    {
        Require(
            BuiltInRegistries.HeightProviderType.Count == 6,
            "The height-provider type registry must contain all six Minecraft 26.3 variants.");

        string[] identifiers =
        [
            "constant", "uniform", "biased_to_bottom",
            "very_biased_to_bottom", "trapezoid", "weighted_list"
        ];

        foreach (string identifier in identifiers)
        {
            Require(
                BuiltInRegistries.HeightProviderType.ContainsKey(
                    ResourceLocation.Parse($"minecraft:{identifier}")),
                $"The height-provider registry must expose minecraft:{identifier}.");
        }

        Require(
            !BuiltInRegistries.HeightProviderType.ContainsKey(ResourceLocation.Parse("asteria:uniform")),
            "Built-in height-provider types must retain their Minecraft namespace identifiers.");
    }

    private static void CheckConstantAnchorShorthand()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        WorldGenerationContext context = new(-64, 384, 63);

        HeightProvider decoded = HeightProvider.CODEC
            .Parse(ops, ParseJson("""{"above_bottom":12}"""))
            .GetOrThrow();

        Require(
            decoded is ConstantHeight constant &&
            constant.Value == VerticalAnchor.AboveBottom(12) &&
            constant.Sample(new LegacyRandomSource(1L), context) == -52,
            "A VerticalAnchor object must decode as ConstantHeight and resolve against the generation context.");

        JsonElement encoded = HeightProvider.CODEC
            .EncodeStart(ops, ConstantHeight.Of(VerticalAnchor.RelativeToSeaLevel(-5)))
            .GetOrThrow();

        Require(
            ops.Get(encoded, "relative_to_sea_level").GetOrThrow().GetInt32() == -5,
            "ConstantHeight must encode through the anchor shorthand rather than a dispatched provider object.");

        Require(
            ReferenceEquals(
                ConstantHeight.Of(VerticalAnchor.Absolute(0)),
                ConstantHeight.ZERO),
            "The zero constant height must use its canonical singleton.");
    }

    private static void CheckUniformHeightCodecAndSampling()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var expected = UniformHeight.Of(
            VerticalAnchor.Absolute(-11),
            VerticalAnchor.AboveBottom(23));

        JsonElement encoded = HeightProvider.CODEC.EncodeStart(ops, expected).GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:uniform" &&
            ops.Get(encoded, "min_inclusive").GetOrThrow().ValueKind == JsonValueKind.Object &&
            ops.Get(encoded, "max_inclusive").GetOrThrow().ValueKind == JsonValueKind.Object,
            "UniformHeight must dispatch by registry ID and serialize both vertical anchors.");

        HeightProvider decoded = HeightProvider.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(
            decoded is UniformHeight uniform &&
            uniform.Minimum == expected.Minimum &&
            uniform.Maximum == expected.Maximum,
            "UniformHeight must round-trip both anchors through the full height-provider codec.");

        var expectedRandom = new LegacyRandomSource(12345L);
        int minimum = -32;
        int maximum = 48;
        int expectedSample = unchecked(
            expectedRandom.NextInt(maximum - minimum + 1) + minimum);
        int actualSample = UniformHeight.Of(
            VerticalAnchor.Absolute(minimum),
            VerticalAnchor.Absolute(maximum))
            .Sample(new LegacyRandomSource(12345L), new WorldGenerationContext(0, 256, 63));

        Require(
            actualSample == expectedSample &&
            actualSample >= minimum &&
            actualSample <= maximum,
            "UniformHeight must sample the inclusive resolved anchor interval with one bounded RNG call.");
    }

    private static void CheckBiasedToBottomSampling()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var provider = BiasedToBottomHeight.Of(
            VerticalAnchor.Absolute(-24),
            VerticalAnchor.Absolute(72),
            inner: 4);

        JsonElement encoded = HeightProvider.CODEC.EncodeStart(ops, provider).GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:biased_to_bottom" &&
            ops.Get(encoded, "inner").GetOrThrow().GetInt32() == 4,
            "BiasedToBottomHeight must serialize its registered type and inner parameter.");

        HeightProvider decoded = HeightProvider.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(
            decoded is BiasedToBottomHeight biased &&
            biased.Minimum == provider.Minimum &&
            biased.Maximum == provider.Maximum &&
            biased.Inner == provider.Inner,
            "BiasedToBottomHeight must round-trip anchors and its inner parameter.");

        Require(
            HeightProvider.CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:biased_to_bottom","min_inclusive":{"absolute":0},"max_inclusive":{"absolute":8}}"""))
                .GetOrThrow() is BiasedToBottomHeight { Inner: 1 },
            "BiasedToBottomHeight must default the optional inner field to one.");

        var expectedRandom = new LegacyRandomSource(998877L);
        int span = 72 - (-24) - 4 + 1;
        int limit = expectedRandom.NextInt(span);
        int expectedSample = unchecked(expectedRandom.NextInt(limit + 4) - 24);
        int actualSample = provider.Sample(
            new LegacyRandomSource(998877L),
            new WorldGenerationContext(0, 256, 63));

        Require(
            actualSample == expectedSample,
            "BiasedToBottomHeight must preserve the exact two-call 26.3 sampling sequence.");
    }

    private static void CheckVeryBiasedToBottomSampling()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var provider = VeryBiasedToBottomHeight.Of(
            VerticalAnchor.Absolute(-18),
            VerticalAnchor.Absolute(90),
            inner: 3);

        JsonElement encoded = HeightProvider.CODEC.EncodeStart(ops, provider).GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:very_biased_to_bottom" &&
            ops.Get(encoded, "inner").GetOrThrow().GetInt32() == 3,
            "VeryBiasedToBottomHeight must serialize its registered type and inner parameter.");

        HeightProvider decoded = HeightProvider.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(
            decoded is VeryBiasedToBottomHeight biased &&
            biased.Minimum == provider.Minimum &&
            biased.Maximum == provider.Maximum &&
            biased.Inner == provider.Inner,
            "VeryBiasedToBottomHeight must round-trip anchors and its inner parameter.");

        var expectedRandom = new LegacyRandomSource(554433L);
        int upper = RandomBetweenInclusive(expectedRandom, -18 + 3, 90);
        int biasedUpper = RandomBetweenInclusive(expectedRandom, -18, upper - 1);
        int expectedSample = RandomBetweenInclusive(expectedRandom, -18, biasedUpper - 1 + 3);
        int actualSample = provider.Sample(
            new LegacyRandomSource(554433L),
            new WorldGenerationContext(0, 256, 63));

        Require(
            actualSample == expectedSample,
            "VeryBiasedToBottomHeight must preserve its exact three-stage nested range sampling sequence.");
    }

    private static void CheckTrapezoidSampling()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var provider = TrapezoidHeight.Of(
            VerticalAnchor.Absolute(-12),
            VerticalAnchor.Absolute(24),
            plateau: 8);

        JsonElement encoded = HeightProvider.CODEC.EncodeStart(ops, provider).GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:trapezoid" &&
            ops.Get(encoded, "plateau").GetOrThrow().GetInt32() == 8,
            "TrapezoidHeight must serialize its plateau and anchor fields.");

        HeightProvider decoded = HeightProvider.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(
            decoded is TrapezoidHeight trapezoid &&
            trapezoid.Minimum == provider.Minimum &&
            trapezoid.Maximum == provider.Maximum &&
            trapezoid.Plateau == provider.Plateau,
            "TrapezoidHeight must round-trip both anchors and the plateau.");

        Require(
            HeightProvider.CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:trapezoid","min_inclusive":{"absolute":0},"max_inclusive":{"absolute":10}}"""))
                .GetOrThrow() is TrapezoidHeight { Plateau: 0 },
            "TrapezoidHeight must default the optional plateau field to zero.");

        var expectedRandom = new LegacyRandomSource(112244L);
        int range = 24 - (-12);
        int plateauStart = (range - 8) / 2;
        int plateauEnd = range - plateauStart;
        int expectedSample = unchecked(
            -12 +
            RandomBetweenInclusive(expectedRandom, 0, plateauEnd) +
            RandomBetweenInclusive(expectedRandom, 0, plateauStart));
        int actualSample = provider.Sample(
            new LegacyRandomSource(112244L),
            new WorldGenerationContext(0, 256, 63));

        Require(
            actualSample == expectedSample,
            "TrapezoidHeight must consume its two inclusive subrange samples in the 26.3 order.");

        var triangle = TrapezoidHeight.Of(
            VerticalAnchor.Absolute(-7),
            VerticalAnchor.Absolute(7));
        Require(
            triangle.ToString() == "triangle (-7 absolute-7 absolute)",
            "A zero-plateau TrapezoidHeight must retain the vanilla triangle diagnostic form.");
    }

    private static void CheckWeightedListHeight()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var distribution = WeightedList<HeightProvider>.OfWeighted(
            new Weighted<HeightProvider>(
                ConstantHeight.Of(VerticalAnchor.Absolute(4)),
                2),
            new Weighted<HeightProvider>(
                UniformHeight.Of(
                    VerticalAnchor.Absolute(8),
                    VerticalAnchor.Absolute(10)),
                1));
        var provider = new WeightedListHeight(distribution);

        JsonElement encoded = HeightProvider.CODEC.EncodeStart(ops, provider).GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:weighted_list" &&
            ops.Get(encoded, "distribution").GetOrThrow().ValueKind == JsonValueKind.Array,
            "WeightedListHeight must serialize a non-empty distribution of height providers.");

        HeightProvider decoded = HeightProvider.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(
            decoded is WeightedListHeight weighted &&
            weighted.Distribution.Count == 2 &&
            weighted.Distribution[0].Weight == 2 &&
            weighted.Distribution[0].Value is ConstantHeight { Value: var firstAnchor } &&
            firstAnchor == VerticalAnchor.Absolute(4) &&
            weighted.Distribution[1].Weight == 1 &&
            weighted.Distribution[1].Value is UniformHeight
                {
                    Minimum: var secondMinimum,
                    Maximum: var secondMaximum
                } &&
            secondMinimum == VerticalAnchor.Absolute(8) &&
            secondMaximum == VerticalAnchor.Absolute(10),
            "WeightedListHeight must round-trip nested providers and their weights.");

        var expectedRandom = new LegacyRandomSource(667788L);
        int selected = expectedRandom.NextInt(distribution.TotalWeight);
        int expectedSample = selected < 2
            ? 4
            : RandomBetweenInclusive(expectedRandom, 8, 10);
        int actualSample = provider.Sample(
            new LegacyRandomSource(667788L),
            new WorldGenerationContext(0, 256, 63));

        Require(
            actualSample == expectedSample,
            "WeightedListHeight must select by weight before sampling the selected nested provider.");
    }

    private static void CheckInvalidRanges()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        Require(
            HeightProvider.CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:uniform","min_inclusive":{"absolute":0},"max_inclusive":{"absolute":8}}"""))
                .IsSuccess,
            "UniformHeight must accept valid anchor pairs.");

        Require(
            HeightProvider.CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:biased_to_bottom","min_inclusive":{"absolute":0},"max_inclusive":{"absolute":8},"inner":0}"""))
                .IsError,
            "BiasedToBottomHeight must require a positive inner value in its codec.");

        Require(
            HeightProvider.CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:very_biased_to_bottom","min_inclusive":{"absolute":0},"max_inclusive":{"absolute":8},"inner":0}"""))
                .IsError,
            "VeryBiasedToBottomHeight must require a positive inner value in its codec.");

        Require(
            HeightProvider.CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:missing"}"""))
                .IsError,
            "HeightProvider dispatch must reject unknown provider IDs.");

        Require(
            HeightProvider.CODEC.Parse(
                ops,
                ParseJson("""{"type":"minecraft:weighted_list","distribution":[]}"""))
                .IsError,
            "WeightedListHeight must reject an empty weighted distribution.");

        var context = new WorldGenerationContext(0, 256, 63);
        Require(
            UniformHeight.Of(
                VerticalAnchor.Absolute(12),
                VerticalAnchor.Absolute(8))
                .Sample(new LegacyRandomSource(1L), context) == 12,
            "UniformHeight must return the resolved minimum for an inverted range.");

        Require(
            BiasedToBottomHeight.Of(
                VerticalAnchor.Absolute(12),
                VerticalAnchor.Absolute(13),
                inner: 3)
                .Sample(new LegacyRandomSource(1L), context) == 12,
            "BiasedToBottomHeight must return the minimum when its inner window empties the range.");

        Require(
            VeryBiasedToBottomHeight.Of(
                VerticalAnchor.Absolute(12),
                VerticalAnchor.Absolute(13),
                inner: 3)
                .Sample(new LegacyRandomSource(1L), context) == 12,
            "VeryBiasedToBottomHeight must return the minimum when its inner window empties the range.");

        Require(
            TrapezoidHeight.Of(
                VerticalAnchor.Absolute(12),
                VerticalAnchor.Absolute(8))
                .Sample(new LegacyRandomSource(1L), context) == 12,
            "TrapezoidHeight must return the resolved minimum for an inverted range.");
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
