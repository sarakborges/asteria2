using Asteria.Serialization;

namespace Asteria.World.LevelGen.Structure;

/// <summary>
/// Terrain deformation mode used by structure pieces. Serialized names and singleton
/// identities match Minecraft Java 26.3 TerrainAdjustment.
/// </summary>
public sealed class TerrainAdjustment
{
    public static readonly TerrainAdjustment NONE = new("NONE", "none");
    public static readonly TerrainAdjustment BURY = new("BURY", "bury");
    public static readonly TerrainAdjustment BEARD_THIN = new("BEARD_THIN", "beard_thin");
    public static readonly TerrainAdjustment BEARD_BOX = new("BEARD_BOX", "beard_box");
    public static readonly TerrainAdjustment ENCAPSULATE = new("ENCAPSULATE", "encapsulate");

    public static IReadOnlyList<TerrainAdjustment> Values { get; } =
        Array.AsReadOnly(new[]
        {
            NONE,
            BURY,
            BEARD_THIN,
            BEARD_BOX,
            ENCAPSULATE
        });

    private static readonly IReadOnlyDictionary<string, TerrainAdjustment> ByName =
        Values.ToDictionary(static value => value._id, StringComparer.Ordinal);

    public static Codec<TerrainAdjustment> CODEC { get; } =
        Codecs.STRING.FlatXMap(
            static name => ByName.TryGetValue(name, out TerrainAdjustment? value)
                ? DataResult<TerrainAdjustment>.Success(value)
                : DataResult<TerrainAdjustment>.Error(
                    () => $"Unknown terrain adjustment: {name}"),
            static value => value is not null
                ? DataResult<string>.Success(value.GetSerializedName())
                : DataResult<string>.Error("Terrain adjustment cannot be null"));

    private readonly string _name;
    private readonly string _id;

    private TerrainAdjustment(string name, string id)
    {
        _name = name;
        _id = id;
    }

    public string GetSerializedName() => _id;

    public override string ToString() => _name;
}
