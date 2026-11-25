using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.LevelGen.Structure.Placement;

namespace Asteria.World.LevelGen.Structure;

/// <summary>
/// A weighted selection of structures that share one placement rule,
/// matching Minecraft Java 26.3 StructureSet.
/// Codec registration and registry dispatch are implemented with the structure registries.
/// </summary>
public sealed record StructureSet(
    IReadOnlyList<StructureSet.StructureSelectionEntry> Structures,
    StructurePlacement Placement)
{
    public static Codec<StructureSet> DIRECT_CODEC { get; } =
        RecordCodecBuilder.Create<StructureSet>(instance =>
            instance.Group(
                StructureSelectionEntry.CODEC.ListOf()
                    .FieldOf("structures")
                    .ForGetter<StructureSet>(static set => set.Structures),
                StructurePlacementCodecs.CODEC
                    .FieldOf("placement")
                    .ForGetter<StructureSet>(static set => set.Placement))
            .Apply(
                instance,
                static (structures, placement) =>
                    new StructureSet(structures, placement)));

    public static Codec<Holder<StructureSet>> CODEC { get; } =
        RegistryCodecs.Holder<StructureSet>(
            BuiltInRegistries.StructureSet.Key,
            DIRECT_CODEC);

    public StructureSet(
        Holder<Structure> singleEntry,
        StructurePlacement placement)
        : this(new[] { new StructureSelectionEntry(singleEntry, 1) }, placement)
    {
    }

    public static StructureSelectionEntry Entry(
        Holder<Structure> structure,
        int weight) =>
        new(structure, weight);

    public static StructureSelectionEntry Entry(Holder<Structure> structure) =>
        new(structure, 1);

    public sealed record StructureSelectionEntry(
        Holder<Structure> Structure,
        int Weight)
    {
        public static Codec<StructureSelectionEntry> CODEC { get; } =
            RecordCodecBuilder.Create<StructureSelectionEntry>(instance =>
                instance.Group(
                    global::Asteria.World.LevelGen.Structure.Structure.CODEC
                        .FieldOf("structure")
                        .ForGetter<StructureSelectionEntry>(static entry => entry.Structure),
                    Codecs.IntRange(1, int.MaxValue)
                        .FieldOf("weight")
                        .ForGetter<StructureSelectionEntry>(static entry => entry.Weight))
                .Apply(
                    instance,
                    static (structure, weight) =>
                        new StructureSelectionEntry(structure, weight)));
    }
}
