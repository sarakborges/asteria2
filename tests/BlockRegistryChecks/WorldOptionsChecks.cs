using System.Text.Json;
using Asteria.Serialization;
using Asteria.World.LevelGen;

internal static class WorldOptionsChecks
{
    public static void Run()
    {
        CheckDefaultsAndImmutability();
        CheckRandomSeedCreation();
        CheckSeedParsing();
        CheckCodec();
    }

    private static void CheckDefaultsAndImmutability()
    {
        WorldOptions options = new(seed: 123456789L, generateStructures: true, generateBonusChest: false);
        Require(
            options.Seed == 123456789L &&
            options.GenerateStructures &&
            !options.GenerateBonusChest,
            "WorldOptions must retain the supplied seed and generation flags.");

        WorldOptions changed = options.WithStructures(false).WithBonusChest(true);
        Require(
            options.GenerateStructures && !options.GenerateBonusChest &&
            !changed.GenerateStructures && changed.GenerateBonusChest &&
            changed.Seed == options.Seed,
            "WorldOptions with-methods must return immutable copies and preserve unrelated values.");

        Require(
            WorldOptions.DEMO_OPTIONS.Seed == JavaStringHashCode("North Carolina") &&
            WorldOptions.DEMO_OPTIONS.IsOldCustomizedWorld() == false,
            "DEMO_OPTIONS must use the Java String hash and have no legacy custom options.");

        Require(
            options.WithSeed(987654321L).Seed == 987654321L &&
            options.WithSeed(null).Seed != options.Seed,
            "WithSeed must use an explicit seed when present and generate a fresh seed when absent.");
    }

    private static void CheckRandomSeedCreation()
    {
        WorldOptions first = WorldOptions.DefaultWithRandomSeed();
        WorldOptions second = WorldOptions.DefaultWithRandomSeed();
        WorldOptions test = WorldOptions.TestWorldWithRandomSeed();

        Require(
            first.Seed != second.Seed,
            "Each new default world options instance must obtain a fresh random seed.");

        Require(
            first.GenerateStructures && !first.GenerateBonusChest &&
            !test.GenerateStructures && !test.GenerateBonusChest,
            "Default and test world options must preserve the reference generation flags.");

        Require(
            first.Seed == first.WithStructures(false).Seed,
            "Changing generation flags must not reroll a world's seed.");
    }

    private static void CheckSeedParsing()
    {
        Require(
            WorldOptions.ParseSeed("  -42  ") == -42L,
            "Seed parsing must trim ASCII whitespace and parse signed 64-bit decimal values.");

        Require(
            WorldOptions.ParseSeed("9223372036854775807") == long.MaxValue,
            "Seed parsing must accept the largest signed 64-bit value.");

        Require(
            WorldOptions.ParseSeed("9223372036854775808") == JavaStringHashCode("9223372036854775808"),
            "A numeric string outside the signed 64-bit range must fall back to its Java string hash.");

        Require(
            WorldOptions.ParseSeed("glacier") == JavaStringHashCode("glacier"),
            "Non-numeric seeds must use Java String.hashCode rather than the runtime's randomized string hash.");

        Require(
            WorldOptions.ParseSeed("   ") is null,
            "An empty seed string must remain absent so the options layer can choose a random seed.");
    }

    private static void CheckCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        WorldOptions source = new(seed: -1234L, generateStructures: false, generateBonusChest: true);
        JsonElement encoded = WorldOptions.CODEC.EncodeStart(ops, source).GetOrThrow();

        Require(
            ops.Get(encoded, "seed").GetOrThrow().GetInt64() == -1234L &&
            !ops.Get(encoded, "generate_structures").GetOrThrow().GetBoolean() &&
            ops.Get(encoded, "bonus_chest").GetOrThrow().GetBoolean(),
            "WorldOptions codec must emit seed, generate_structures and bonus_chest fields.");

        WorldOptions decoded = WorldOptions.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(
            decoded.Seed == source.Seed &&
            decoded.GenerateStructures == source.GenerateStructures &&
            decoded.GenerateBonusChest == source.GenerateBonusChest &&
            !decoded.IsOldCustomizedWorld(),
            "WorldOptions codec must preserve the generation options when round-tripped.");

        JsonElement minimal = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("seed"),
                ops.CreateLong(77L))
        });
        WorldOptions defaults = WorldOptions.CODEC.Parse(ops, minimal).GetOrThrow();
        Require(
            defaults.Seed == 77L &&
            defaults.GenerateStructures &&
            !defaults.GenerateBonusChest,
            "WorldOptions codec must decode absent optional flags using the reference defaults.");

        JsonElement legacy = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("seed"),
                ops.CreateLong(88L)),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("legacy_custom_options"),
                ops.CreateString("customized"))
        });
        Require(
            WorldOptions.CODEC.Parse(ops, legacy).GetOrThrow().IsOldCustomizedWorld(),
            "WorldOptions codec must preserve legacy customized-world metadata.");
    }

    private static int JavaStringHashCode(string value)
    {
        int hash = 0;
        foreach (char character in value)
            hash = unchecked(31 * hash + character);

        return hash;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
