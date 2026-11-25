using System.Text.Json;
using Asteria.Serialization;
using Asteria.World.LevelGen.Structure.Placement;

internal static class FrequencyReductionMethodChecks
{
    public static void Run()
    {
        CheckValuesAndCodec();
        CheckAllLegacyAlgorithmsAgainstGoldenFixtures();
        CheckLegacyTypeTwoUsesTheFixedSalt();
        CheckZeroProbabilityPreservesJavaFloatCastSemantics();
    }

    private static void CheckValuesAndCodec()
    {
        Require(
            FrequencyReductionMethod.Values.Count == 4 &&
            ReferenceEquals(FrequencyReductionMethod.Values[0], FrequencyReductionMethod.DEFAULT) &&
            ReferenceEquals(FrequencyReductionMethod.Values[1], FrequencyReductionMethod.LEGACY_TYPE_1) &&
            ReferenceEquals(FrequencyReductionMethod.Values[2], FrequencyReductionMethod.LEGACY_TYPE_2) &&
            ReferenceEquals(FrequencyReductionMethod.Values[3], FrequencyReductionMethod.LEGACY_TYPE_3),
            "FrequencyReductionMethod must expose the four vanilla singleton values in declaration order.");

        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        foreach (FrequencyReductionMethod value in FrequencyReductionMethod.Values)
        {
            JsonElement encoded = FrequencyReductionMethod.CODEC
                .EncodeStart(ops, value)
                .GetOrThrow();

            Require(
                encoded.GetString() == value.GetSerializedName() &&
                ReferenceEquals(
                    FrequencyReductionMethod.CODEC.Parse(ops, encoded).GetOrThrow(),
                    value),
                $"{value.GetSerializedName()} must round-trip through the frequency reduction codec.");
        }

        Require(
            FrequencyReductionMethod.CODEC
                .Parse(ops, ops.CreateString("unknown"))
                .IsError,
            "Unknown frequency reduction identifiers must be rejected.");
    }

    private static void CheckAllLegacyAlgorithmsAgainstGoldenFixtures()
    {
        const long firstSeed = 123456789L;
        const int firstSalt = 10387320;
        const int firstX = -7;
        const int firstZ = 19;
        const float firstProbability = 0.25F;

        Require(
            !FrequencyReductionMethod.DEFAULT.ShouldGenerate(
                firstSeed, firstSalt, firstX, firstZ, firstProbability),
            "DEFAULT must match the 26.3 nextFloat/large-feature-with-salt golden fixture.");
        Require(
            FrequencyReductionMethod.LEGACY_TYPE_1.ShouldGenerate(
                firstSeed, firstSalt, firstX, firstZ, firstProbability),
            "LEGACY_TYPE_1 must retain the pillager-outpost seed, discarded first int and reciprocal-bound test.");
        Require(
            FrequencyReductionMethod.LEGACY_TYPE_2.ShouldGenerate(
                firstSeed, firstSalt, firstX, firstZ, firstProbability),
            "LEGACY_TYPE_2 must retain its fixed arbitrary salt probability test.");
        Require(
            !FrequencyReductionMethod.LEGACY_TYPE_3.ShouldGenerate(
                firstSeed, firstSalt, firstX, firstZ, firstProbability),
            "LEGACY_TYPE_3 must retain the large-feature seed and nextDouble probability test.");

        const long secondSeed = 987654321L;
        Require(
            !FrequencyReductionMethod.DEFAULT.ShouldGenerate(
                secondSeed, 42, 128, -256, 0.375F) &&
            !FrequencyReductionMethod.LEGACY_TYPE_1.ShouldGenerate(
                secondSeed, 42, 128, -256, 0.375F) &&
            FrequencyReductionMethod.LEGACY_TYPE_2.ShouldGenerate(
                secondSeed, 42, 128, -256, 0.375F) &&
            !FrequencyReductionMethod.LEGACY_TYPE_3.ShouldGenerate(
                secondSeed, 42, 128, -256, 0.375F),
            "All reducers must match a second seeded fixture where their legacy formulas diverge.");
    }

    private static void CheckLegacyTypeTwoUsesTheFixedSalt()
    {
        bool withVanillaSalt = FrequencyReductionMethod.LEGACY_TYPE_2.ShouldGenerate(
            123456789L, 10387320, -7, 19, 0.25F);
        bool withDifferentSalt = FrequencyReductionMethod.LEGACY_TYPE_2.ShouldGenerate(
            123456789L, -991, -7, 19, 0.25F);

        Require(
            withVanillaSalt && withDifferentSalt,
            "LEGACY_TYPE_2 must ignore the supplied salt and use the fixed legacy salt 10387320.");
    }

    private static void CheckZeroProbabilityPreservesJavaFloatCastSemantics()
    {
        Require(
            !FrequencyReductionMethod.LEGACY_TYPE_1.ShouldGenerate(
                123456789L, 0, -7, 19, 0.0F),
            "LEGACY_TYPE_1 must convert positive infinity from 1.0F / 0.0F to Java's Integer.MAX_VALUE bound.");
        Require(
            !FrequencyReductionMethod.DEFAULT.ShouldGenerate(
                123456789L, 0, -7, 19, 0.0F) &&
            !FrequencyReductionMethod.LEGACY_TYPE_2.ShouldGenerate(
                123456789L, 0, -7, 19, 0.0F) &&
            !FrequencyReductionMethod.LEGACY_TYPE_3.ShouldGenerate(
                123456789L, 0, -7, 19, 0.0F),
            "A zero probability must reject candidates for every probability reducer.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
