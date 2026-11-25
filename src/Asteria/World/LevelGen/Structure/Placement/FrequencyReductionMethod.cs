using Asteria.Serialization;

namespace Asteria.World.LevelGen.Structure.Placement;

/// <summary>
/// Seeded probability reducers used by Minecraft Java 26.3
/// AbstractSpreadingStructurePlacement.FrequencyReductionMethod.
/// The legacy variants intentionally retain their historical random-seed formulas.
/// </summary>
public sealed class FrequencyReductionMethod
{
    private const int HIGHLY_ARBITRARY_RANDOM_SALT = 10387320;

    public static readonly FrequencyReductionMethod DEFAULT =
        new("default", ProbabilityReducer);

    public static readonly FrequencyReductionMethod LEGACY_TYPE_1 =
        new("legacy_type_1", LegacyPillagerOutpostReducer);

    public static readonly FrequencyReductionMethod LEGACY_TYPE_2 =
        new("legacy_type_2", LegacyArbitrarySaltProbabilityReducer);

    public static readonly FrequencyReductionMethod LEGACY_TYPE_3 =
        new("legacy_type_3", LegacyProbabilityReducerWithDouble);

    private static readonly FrequencyReductionMethod[] VALUES_ARRAY =
    {
        DEFAULT,
        LEGACY_TYPE_1,
        LEGACY_TYPE_2,
        LEGACY_TYPE_3
    };

    public static IReadOnlyList<FrequencyReductionMethod> Values { get; } =
        Array.AsReadOnly(VALUES_ARRAY);

    private static readonly IReadOnlyDictionary<string, FrequencyReductionMethod> BY_NAME =
        VALUES_ARRAY.ToDictionary(static value => value._name, StringComparer.Ordinal);

    public static Codec<FrequencyReductionMethod> CODEC { get; } =
        Codecs.STRING.FlatXMap(
            static name => BY_NAME.TryGetValue(name, out FrequencyReductionMethod? value)
                ? DataResult<FrequencyReductionMethod>.Success(value)
                : DataResult<FrequencyReductionMethod>.Error(
                    () => $"Unknown element name: {name}"),
            static value => value is not null
                ? DataResult<string>.Success(value.GetSerializedName())
                : DataResult<string>.Error("Frequency reduction method cannot be null"));

    private readonly string _name;
    private readonly Func<long, int, int, int, float, bool> _reducer;

    private FrequencyReductionMethod(
        string name,
        Func<long, int, int, int, float, bool> reducer)
    {
        _name = name;
        _reducer = reducer;
    }

    public string GetSerializedName() =>
        _name;

    public bool ShouldGenerate(
        long seed,
        int salt,
        int sourceX,
        int sourceZ,
        float probability) =>
        _reducer(seed, salt, sourceX, sourceZ, probability);

    private static bool ProbabilityReducer(
        long seed,
        int salt,
        int sourceX,
        int sourceZ,
        float probability)
    {
        var random = new WorldgenRandom(new LegacyRandomSource(0L));
        random.SetLargeFeatureWithSalt(seed, salt, sourceX, sourceZ);
        return random.NextFloat() < probability;
    }

    private static bool LegacyProbabilityReducerWithDouble(
        long seed,
        int salt,
        int sourceX,
        int sourceZ,
        float probability)
    {
        var random = new WorldgenRandom(new LegacyRandomSource(0L));
        random.SetLargeFeatureSeed(seed, sourceX, sourceZ);
        return random.NextDouble() < probability;
    }

    private static bool LegacyArbitrarySaltProbabilityReducer(
        long seed,
        int salt,
        int sourceX,
        int sourceZ,
        float probability)
    {
        var random = new WorldgenRandom(new LegacyRandomSource(0L));
        random.SetLargeFeatureWithSalt(
            seed,
            sourceX,
            sourceZ,
            HIGHLY_ARBITRARY_RANDOM_SALT);
        return random.NextFloat() < probability;
    }

    private static bool LegacyPillagerOutpostReducer(
        long seed,
        int salt,
        int sourceX,
        int sourceZ,
        float probability)
    {
        int chunkX = sourceX >> 4;
        int chunkZ = sourceZ >> 4;

        var random = new WorldgenRandom(new LegacyRandomSource(0L));
        random.SetSeed(unchecked((long)chunkX ^ ((long)chunkZ << 4) ^ seed));
        _ = random.NextInt();

        int bound = JavaFloatToInt(1.0F / probability);
        return random.NextInt(bound) == 0;
    }

    private static int JavaFloatToInt(float value)
    {
        if (float.IsNaN(value))
            return 0;
        if (value >= int.MaxValue)
            return int.MaxValue;
        if (value <= int.MinValue)
            return int.MinValue;
        return (int)value;
    }

    public override string ToString() =>
        _name;
}
