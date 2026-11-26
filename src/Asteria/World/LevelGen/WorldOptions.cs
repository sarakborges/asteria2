using System.Globalization;
using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>
/// Immutable world-generation options from Minecraft Java 26.3.
/// The seed selected here belongs to the world and must be persisted with its settings.
/// </summary>
public sealed class WorldOptions
{
    public static readonly MapCodec<WorldOptions> CODEC =
        RecordCodecBuilder.MapCodec<WorldOptions>(instance =>
            instance.Group(
                Codecs.LONG.FieldOf("seed")
                    .ForGetter<WorldOptions>(static options => options.Seed),
                Codecs.BOOL.OptionalAlwaysPresentFieldOf("generate_structures", true)
                    .ForGetter<WorldOptions>(static options => options.GenerateStructures),
                Codecs.BOOL.OptionalAlwaysPresentFieldOf("bonus_chest", false)
                    .ForGetter<WorldOptions>(static options => options.GenerateBonusChest),
                Codecs.STRING.OptionalFieldOf("legacy_custom_options")
                    .ForGetter<WorldOptions>(static options => options._legacyCustomOptions))
            .Apply(instance, static (seed, generateStructures, generateBonusChest, legacyCustomOptions) =>
                new WorldOptions(seed, generateStructures, generateBonusChest, legacyCustomOptions)));

    public static readonly WorldOptions DEMO_OPTIONS =
        new(JavaStringHashCode("North Carolina"), generateStructures: true, generateBonusChest: true);

    private readonly OptionalValue<string> _legacyCustomOptions;

    public long Seed { get; }
    public bool GenerateStructures { get; }
    public bool GenerateBonusChest { get; }

    public WorldOptions(long seed, bool generateStructures, bool generateBonusChest)
        : this(seed, generateStructures, generateBonusChest, OptionalValue<string>.Empty)
    {
    }

    private WorldOptions(
        long seed,
        bool generateStructures,
        bool generateBonusChest,
        OptionalValue<string> legacyCustomOptions)
    {
        Seed = seed;
        GenerateStructures = generateStructures;
        GenerateBonusChest = generateBonusChest;
        _legacyCustomOptions = legacyCustomOptions;
    }

    public static WorldOptions DefaultWithRandomSeed() =>
        new(RandomSeed(), generateStructures: true, generateBonusChest: false);

    public static WorldOptions TestWorldWithRandomSeed() =>
        new(RandomSeed(), generateStructures: false, generateBonusChest: false);

    public bool IsOldCustomizedWorld() =>
        _legacyCustomOptions.HasValue;

    public WorldOptions WithBonusChest(bool generateBonusChest) =>
        new(Seed, GenerateStructures, generateBonusChest, _legacyCustomOptions);

    public WorldOptions WithStructures(bool generateStructures) =>
        new(Seed, generateStructures, GenerateBonusChest, _legacyCustomOptions);

    public WorldOptions WithSeed(long? seed) =>
        new(seed ?? RandomSeed(), GenerateStructures, GenerateBonusChest, _legacyCustomOptions);

    public static long? ParseSeed(string seedString)
    {
        ArgumentNullException.ThrowIfNull(seedString);

        int start = 0;
        int end = seedString.Length;
        while (start < end && seedString[start] <= ' ')
            start++;
        while (end > start && seedString[end - 1] <= ' ')
            end--;

        string trimmed = seedString.Substring(start, end - start);
        if (trimmed.Length == 0)
            return null;

        return long.TryParse(
            trimmed,
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out long parsedSeed)
            ? parsedSeed
            : JavaStringHashCode(trimmed);
    }

    /// <summary>
    /// Equivalent to RandomSource.create().nextLong(): create a LegacyRandomSource
    /// from RandomSupport's unique seed and draw its first long.
    /// </summary>
    public static long RandomSeed() =>
        new LegacyRandomSource(RandomSupport.GenerateUniqueSeed()).NextLong();

    private static int JavaStringHashCode(string value)
    {
        int hash = 0;
        foreach (char character in value)
            hash = unchecked(31 * hash + character);

        return hash;
    }
}
