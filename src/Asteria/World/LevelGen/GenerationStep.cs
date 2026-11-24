using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>
/// Ordered world-generation decoration stages from Minecraft Java 26.3.
/// </summary>
public static class GenerationStep
{
    public sealed class Decoration
    {
        public static readonly Decoration RAW_GENERATION = new("raw_generation");
        public static readonly Decoration LAKES = new("lakes");
        public static readonly Decoration LOCAL_MODIFICATIONS = new("local_modifications");
        public static readonly Decoration UNDERGROUND_STRUCTURES = new("underground_structures");
        public static readonly Decoration SURFACE_STRUCTURES = new("surface_structures");
        public static readonly Decoration STRONGHOLDS = new("strongholds");
        public static readonly Decoration UNDERGROUND_ORES = new("underground_ores");
        public static readonly Decoration UNDERGROUND_DECORATION = new("underground_decoration");
        public static readonly Decoration FLUID_SPRINGS = new("fluid_springs");
        public static readonly Decoration VEGETAL_DECORATION = new("vegetal_decoration");
        public static readonly Decoration TOP_LAYER_MODIFICATION = new("top_layer_modification");

        private static readonly Decoration[] VALUES_ARRAY =
        {
            RAW_GENERATION,
            LAKES,
            LOCAL_MODIFICATIONS,
            UNDERGROUND_STRUCTURES,
            SURFACE_STRUCTURES,
            STRONGHOLDS,
            UNDERGROUND_ORES,
            UNDERGROUND_DECORATION,
            FLUID_SPRINGS,
            VEGETAL_DECORATION,
            TOP_LAYER_MODIFICATION
        };

        public static IReadOnlyList<Decoration> Values { get; } =
            Array.AsReadOnly(VALUES_ARRAY);

        private static readonly IReadOnlyDictionary<string, Decoration> BY_NAME =
            VALUES_ARRAY.ToDictionary(static value => value._name, StringComparer.Ordinal);

        public static Codec<Decoration> CODEC { get; } = CreateCodec();

        private readonly string _name;

        private Decoration(string name) =>
            _name = name;

        private static Codec<Decoration> CreateCodec()
        {
            Codec<Decoration> byName = Codecs.STRING.FlatXMap(
                static name => BY_NAME.TryGetValue(name, out Decoration? value)
                    ? DataResult<Decoration>.Success(value)
                    : DataResult<Decoration>.Error(() => $"Unknown element name: {name}"),
                static value => DataResult<string>.Success(
                    value?._name ?? throw new ArgumentNullException(nameof(value))));

            Codec<Decoration> byId = Codecs.INT.FlatXMap(
                static id => id >= 0 && id < VALUES_ARRAY.Length
                    ? DataResult<Decoration>.Success(VALUES_ARRAY[id])
                    : DataResult<Decoration>.Error(() => $"Unknown element id: {id}"),
                static value => DataResult<int>.Success(
                    value is null
                        ? throw new ArgumentNullException(nameof(value))
                        : Array.IndexOf(VALUES_ARRAY, value)));

            return byName.OrCompressed(byId);
        }

        public string GetName() => _name;

        public string GetSerializedName() => _name;

        public override string ToString() => _name;
    }
}
