using Asteria.Serialization;

namespace Asteria.World.Entities;

/// <summary>
/// Mob spawning categories and their per-chunk/despawn properties from Minecraft Java 26.3.
/// </summary>
public sealed class MobCategory
{
    public static readonly MobCategory MONSTER =
        new("monster", "MO", 70, false, false, 128);
    public static readonly MobCategory CREATURE =
        new("creature", "C", 10, true, true, 128);
    public static readonly MobCategory AMBIENT =
        new("ambient", "AM", 15, true, false, 128);
    public static readonly MobCategory AXOLOTLS =
        new("axolotls", "AX", 5, true, false, 128);
    public static readonly MobCategory UNDERGROUND_WATER_CREATURE =
        new("underground_water_creature", "UWC", 5, true, false, 128);
    public static readonly MobCategory WATER_CREATURE =
        new("water_creature", "WC", 5, true, false, 128);
    public static readonly MobCategory WATER_AMBIENT =
        new("water_ambient", "WA", 20, true, false, 64);
    public static readonly MobCategory MISC =
        new("misc", "MI", -1, true, true, 128);

    private static readonly MobCategory[] VALUES_ARRAY =
    {
        MONSTER,
        CREATURE,
        AMBIENT,
        AXOLOTLS,
        UNDERGROUND_WATER_CREATURE,
        WATER_CREATURE,
        WATER_AMBIENT,
        MISC
    };

    public static IReadOnlyList<MobCategory> Values { get; } =
        Array.AsReadOnly(VALUES_ARRAY);

    private static readonly IReadOnlyDictionary<string, MobCategory> BY_NAME =
        VALUES_ARRAY.ToDictionary(static value => value._name, StringComparer.Ordinal);

    public static Codec<MobCategory> CODEC { get; } = CreateCodec();

    private readonly string _name;
    private readonly string _debugAbbreviation;
    private readonly int _max;
    private readonly bool _isFriendly;
    private readonly bool _isPersistent;
    private readonly int _despawnDistance;

    private MobCategory(
        string name,
        string debugAbbreviation,
        int max,
        bool isFriendly,
        bool isPersistent,
        int despawnDistance)
    {
        _name = name;
        _debugAbbreviation = debugAbbreviation;
        _max = max;
        _isFriendly = isFriendly;
        _isPersistent = isPersistent;
        _despawnDistance = despawnDistance;
    }

    private static Codec<MobCategory> CreateCodec()
    {
        Codec<MobCategory> byName = Codecs.STRING.FlatXMap(
            static name => BY_NAME.TryGetValue(name, out MobCategory? value)
                ? DataResult<MobCategory>.Success(value)
                : DataResult<MobCategory>.Error(() => $"Unknown element name: {name}"),
            static value => DataResult<string>.Success(
                value?._name ?? throw new ArgumentNullException(nameof(value))));

        Codec<MobCategory> byId = Codecs.INT.FlatXMap(
            static id => id >= 0 && id < VALUES_ARRAY.Length
                ? DataResult<MobCategory>.Success(VALUES_ARRAY[id])
                : DataResult<MobCategory>.Error(() => $"Unknown element id: {id}"),
            static value => DataResult<int>.Success(
                value is null
                    ? throw new ArgumentNullException(nameof(value))
                    : Array.IndexOf(VALUES_ARRAY, value)));

        return byName.OrCompressed(byId);
    }

    public string GetName() => _name;

    public string GetDebugAbbreviation() => _debugAbbreviation;

    public string GetSerializedName() => _name;

    public int GetMaxInstancesPerChunk() => _max;

    public bool IsFriendly() => _isFriendly;

    public bool IsPersistent() => _isPersistent;

    public int GetDespawnDistance() => _despawnDistance;

    public int GetNoDespawnDistance() => 32;

    public override string ToString() => _name;
}
