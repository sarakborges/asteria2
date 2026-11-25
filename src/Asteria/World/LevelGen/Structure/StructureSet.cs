using Asteria.Content.Registries;
using Asteria.World.LevelGen.Structure.Placement;

namespace Asteria.World.LevelGen.Structure;

/// <summary>
/// A weighted selection of structures that share one placement rule,
/// matching Minecraft Java 26.3 StructureSet.
/// Codec registration and registry dispatch are implemented with the structure registries.
/// </summary>
public sealed record StructureSet(
    IReadOnlyList<StructureSelectionEntry> Structures,
    StructurePlacement Placement)
{
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
        int Weight);
}
