using System.Text.Json;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World.LevelGen;

internal static class FloatProviderChecks
{
    public static void Run()
    {
        CheckTypeRegistry();
        CheckConstantShorthand();
        CheckUniformProvider();
        CheckClampedNormalProvider();
        CheckTrapezoidProvider();
        CheckRangeCodecs();
        CheckInvalidInputs();
    }

    private static void CheckTypeRegistry()
    {
        Require(
            BuiltInRegistries.FloatProviderType.Count == 4,
            "The world-generation float-provider registry must contain the four Minecraft 26.3 types.");

        Require(
            BuiltInRegistries.FloatProviderType.ContainsKey(ResourceLocation.Parse("minecraft:constant")) &&
            BuiltInRegistries.FloatProviderType.ContainsKey(ResourceLocation.Parse("minecraft:uniform")) &&
            BuiltInRegistries.FloatProviderType.ContainsKey(ResourceLocation.Parse("minecraft:clamped_normal")) &&
            BuiltInRegistries.FloatProviderType.ContainsKey(ResourceLocation.Parse("minecraft:trapezoid")),
            "The float-provider type registry must expose the four vanilla 26.3 identifiers.");

        Require(
            !BuiltInRegistries.FloatProviderType.ContainsKey(ResourceLocation.Parse("asteria:uniform")),
            "Built-in float-provider types must retain their Minecraft namespace identifiers.");
    }

    private static void CheckConstantShorthand()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        FloatProvider zero = FloatProviders.CODEC
            .Parse(ops, ParseJson("0.0"))
            .GetOrThrow();

        Require(
            ReferenceEquals(zero, ConstantFloat.ZERO),
            "The numeric zero shorthand must resolve to the canonical ConstantFloat.ZERO instance.");

        FloatProvider expected = ConstantFloat.Of(-2.75f);
        JsonElement encoded = FloatProviders.CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            encoded.ValueKind == JsonValueKind.Number &&
            encoded.GetSingle() == -2.75f,
            "ConstantFloat must encode as the numeric shorthand instead of a typed object.");

        Require(
            FloatProviders.CODEC.Parse(ops, encoded).GetOrThrow().Equals(expected),
            "ConstantFloat numeric shorthand must round-trip.");

        JsonElement typed = ConstantFloat.MAP_CODEC
            .EncodeStart(ops, expected as ConstantFloat ?? throw new InvalidOperationException())
            .GetOrThrow();
        Require(
            ops.Get(typed, "value").GetOrThrow().GetSingle() == -2.75f,
            "The registered ConstantFloat map codec must use the 26.3 value field.");
    }

    private static void CheckUniformProvider()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var expected = UniformFloat.Of(-0.75f, 1.25f);

        JsonElement encoded = FloatProviders.CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:uniform" &&
            ops.Get(encoded, "min_inclusive").GetOrThrow().GetSingle() == -0.75f &&
            ops.Get(encoded, "max_exclusive").GetOrThrow().GetSingle() == 1.25f,
            "UniformFloat must encode its registered type and the 26.3 inclusive/exclusive field names.");

        Require(
            FloatProviders.CODEC.Parse(ops, encoded).GetOrThrow().Equals(expected),
            "UniformFloat must round-trip through the registry dispatch codec.");

        var expectedRandom = new LegacyRandomSource(123456L);
        float expectedSample = expected.Minimum +
            expectedRandom.NextFloat() * (expected.Maximum - expected.Minimum);
        float actualSample = expected.Sample(new LegacyRandomSource(123456L));

        Require(
            actualSample == expectedSample &&
            actualSample >= expected.Minimum &&
            actualSample < expected.Maximum,
            "UniformFloat must consume one nextFloat and sample its half-open interval.");
    }

    private static void CheckClampedNormalProvider()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var expected = ClampedNormalFloat.Of(
            mean: 0.25f,
            deviation: 1.5f,
            minimum: -0.5f,
            maximum: 0.75f);

        JsonElement encoded = FloatProviders.CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:clamped_normal" &&
            ops.Get(encoded, "mean").GetOrThrow().GetSingle() == expected.Mean &&
            ops.Get(encoded, "deviation").GetOrThrow().GetSingle() == expected.Deviation &&
            ops.Get(encoded, "min").GetOrThrow().GetSingle() == expected.Minimum &&
            ops.Get(encoded, "max").GetOrThrow().GetSingle() == expected.Maximum,
            "ClampedNormalFloat must encode all four 26.3 fields.");

        Require(
            FloatProviders.CODEC.Parse(ops, encoded).GetOrThrow().Equals(expected),
            "ClampedNormalFloat must round-trip through the registry dispatch codec.");

        var expectedRandom = new LegacyRandomSource(987654L);
        float expectedSample = Mth.Clamp(
            expected.Mean + (float)expectedRandom.NextGaussian() * expected.Deviation,
            expected.Minimum,
            expected.Maximum);
        float actualSample = expected.Sample(new LegacyRandomSource(987654L));

        Require(
            actualSample == expectedSample &&
            actualSample >= expected.Minimum &&
            actualSample <= expected.Maximum,
            "ClampedNormalFloat must use Mth.normal's Gaussian order and clamp to the configured bounds.");
    }

    private static void CheckTrapezoidProvider()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var expected = TrapezoidFloat.Of(-2.0f, 4.0f, 1.5f);

        JsonElement encoded = FloatProviders.CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:trapezoid" &&
            ops.Get(encoded, "min").GetOrThrow().GetSingle() == expected.Minimum &&
            ops.Get(encoded, "max").GetOrThrow().GetSingle() == expected.Maximum &&
            ops.Get(encoded, "plateau").GetOrThrow().GetSingle() == expected.Plateau,
            "TrapezoidFloat must encode the 26.3 min/max/plateau fields.");

        Require(
            FloatProviders.CODEC.Parse(ops, encoded).GetOrThrow().Equals(expected),
            "TrapezoidFloat must round-trip through the registry dispatch codec.");

        var expectedRandom = new LegacyRandomSource(456789L);
        float range = expected.Maximum - expected.Minimum;
        float plateauStart = (range - expected.Plateau) / 2.0f;
        float plateauEnd = range - plateauStart;
        float expectedSample = expected.Minimum +
            expectedRandom.NextFloat() * plateauEnd +
            expectedRandom.NextFloat() * plateauStart;
        float actualSample = expected.Sample(new LegacyRandomSource(456789L));

        Require(
            actualSample == expectedSample &&
            actualSample >= expected.Minimum &&
            actualSample <= expected.Maximum,
            "TrapezoidFloat must consume two nextFloat values in the vanilla plateau order.");

        Require(
            expected.ToString() == "trapezoid(1.5) in [-2-4]",
            "TrapezoidFloat diagnostics must match the Minecraft 26.3 string representation.");
    }

    private static void CheckRangeCodecs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        Require(
            FloatProviders.Codec(-1.0f, 1.0f)
                .Parse(ops, ParseJson("""{"type":"minecraft:uniform","min_inclusive":-0.5,"max_exclusive":0.75}"""))
                .IsSuccess,
            "The bounded float-provider codec must accept ranges fully contained in its inclusive bounds.");

        Require(
            FloatProviders.Codec(0.0f, 1.0f)
                .Parse(ops, ParseJson("""{"type":"minecraft:uniform","min_inclusive":-0.1,"max_exclusive":0.75}"""))
                .IsError,
            "The bounded float-provider codec must reject providers whose minimum is below the requested bound.");

        Require(
            FloatProviders.Codec(0.0f, 1.0f)
                .Parse(ops, ParseJson("""{"type":"minecraft:uniform","min_inclusive":0.25,"max_exclusive":1.1}"""))
                .IsError,
            "The bounded float-provider codec must reject providers whose maximum exceeds the requested bound.");

        Require(
            FloatProviders.Codec(0.0f)
                .Parse(ops, ParseJson("-0.01"))
                .IsError,
            "The minimum-only float-provider codec must reject a provider below its minimum.");
    }

    private static void CheckInvalidInputs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        Require(
            FloatProviders.CODEC.Parse(ops, ParseJson(
                """{"type":"minecraft:uniform","min_inclusive":1.0,"max_exclusive":1.0}""")).IsError,
            "UniformFloat must reject an empty or reversed half-open range.");

        Require(
            FloatProviders.CODEC.Parse(ops, ParseJson(
                """{"type":"minecraft:clamped_normal","mean":0.0,"deviation":1.0,"min":2.0,"max":1.0}""")).IsError,
            "ClampedNormalFloat must reject max values below min.");

        Require(
            FloatProviders.CODEC.Parse(ops, ParseJson(
                """{"type":"minecraft:trapezoid","min":2.0,"max":1.0,"plateau":0.0}""")).IsError,
            "TrapezoidFloat must reject max values below min.");

        Require(
            FloatProviders.CODEC.Parse(ops, ParseJson(
                """{"type":"minecraft:trapezoid","min":0.0,"max":1.0,"plateau":1.1}""")).IsError,
            "TrapezoidFloat must reject a plateau larger than the provider span.");

        Require(
            FloatProviders.CODEC.Parse(ops, ParseJson(
                """{"type":"minecraft:missing"}""")).IsError,
            "The dispatch codec must reject unregistered float-provider types.");
    }

    private static JsonElement ParseJson(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
