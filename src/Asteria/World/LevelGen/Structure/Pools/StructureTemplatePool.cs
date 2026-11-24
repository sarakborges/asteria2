using Asteria.Serialization;

namespace Asteria.World.LevelGen.Structure.Pools;

/// <summary>
/// Structure-template projection modes from Minecraft Java 26.3.
/// Projection processors are ported with the structure-template processor pipeline.
/// </summary>
public static class StructureTemplatePool
{
    public sealed class Projection
    {
        public static readonly Projection TERRAIN_MATCHING = new("terrain_matching");
        public static readonly Projection RIGID = new("rigid");

        public static IReadOnlyList<Projection> Values { get; } =
            Array.AsReadOnly(new[] { TERRAIN_MATCHING, RIGID });

        private static readonly IReadOnlyDictionary<string, Projection> BySerializedName =
            Values.ToDictionary(
                static projection => projection._name,
                StringComparer.Ordinal);

        public static Codec<Projection> CODEC { get; } =
            Codecs.STRING.FlatXMap(
                static name => ByName(name) is { } projection
                    ? DataResult<Projection>.Success(projection)
                    : DataResult<Projection>.Error(
                        () => $"Unknown structure-template projection: {name}"),
                static projection => DataResult<string>.Success(
                    projection.GetSerializedName()));

        private readonly string _name;

        private Projection(string name) =>
            _name = name;

        public string GetName() => _name;

        public string GetSerializedName() => _name;

        public static Projection? ByName(string name) =>
            BySerializedName.TryGetValue(name, out Projection? projection)
                ? projection
                : null;

        public override string ToString() =>
            ReferenceEquals(this, TERRAIN_MATCHING)
                ? "TERRAIN_MATCHING"
                : ReferenceEquals(this, RIGID)
                    ? "RIGID"
                    : base.ToString() ?? string.Empty;
    }
}
