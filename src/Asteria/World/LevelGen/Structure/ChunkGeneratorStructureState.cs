using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.World;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Structure.Placement;

namespace Asteria.World.LevelGen.Structure;

/// <summary>
/// Seed- and biome-aware index of structure placements for one generated dimension.
/// Port of Minecraft Java 26.3 ChunkGeneratorStructureState, excluding concentric-ring
/// position generation until the biome-source horizontal search is ported.
/// </summary>
public sealed class ChunkGeneratorStructureState : IStructurePlacementState
{
    private readonly RandomState _randomState;
    private readonly MultiNoiseBiomeSource _biomeSource;
    private readonly long _levelSeed;
    private readonly long _concentricRingsSeed;
    private readonly ChunkPos _origin;
    private readonly IReadOnlyList<Holder<StructureSet>> _possibleStructureSets;

    private readonly Dictionary<Structure, List<StructurePlacement>> _placementsForStructure =
        new(ReferenceEqualityComparer.Instance);

    private bool _hasGeneratedPositions;

    private ChunkGeneratorStructureState(
        RandomState randomState,
        MultiNoiseBiomeSource biomeSource,
        long levelSeed,
        ChunkPos origin,
        long concentricRingsSeed,
        IReadOnlyList<Holder<StructureSet>> possibleStructureSets)
    {
        _randomState = randomState ?? throw new ArgumentNullException(nameof(randomState));
        _biomeSource = biomeSource ?? throw new ArgumentNullException(nameof(biomeSource));
        _origin = origin;
        _levelSeed = levelSeed;
        _concentricRingsSeed = concentricRingsSeed;
        _possibleStructureSets = possibleStructureSets;
    }

    /// <summary>
    /// Creates state from a supplied structure-set sequence. This is the local
    /// equivalent of createForNormal; registry lookup enumeration is kept by the caller.
    /// </summary>
    public static ChunkGeneratorStructureState CreateForNormal(
        RandomState randomState,
        long levelSeed,
        ChunkPos origin,
        MultiNoiseBiomeSource biomeSource,
        IEnumerable<Holder<StructureSet>> allStructures)
    {
        ArgumentNullException.ThrowIfNull(allStructures);

        Holder<StructureSet>[] structures = allStructures
            .Where(holder => HasBiomesForStructureSet(holder.Value, biomeSource))
            .ToArray();

        return new ChunkGeneratorStructureState(
            randomState,
            biomeSource,
            levelSeed,
            origin,
            levelSeed,
            Array.AsReadOnly(structures));
    }

    /// <summary>
    /// Creates state from the structure overrides used by flat dimensions.
    /// Flat state uses zero as its concentric-rings seed in Minecraft 26.3.
    /// </summary>
    public static ChunkGeneratorStructureState CreateForFlat(
        RandomState randomState,
        long levelSeed,
        ChunkPos origin,
        MultiNoiseBiomeSource biomeSource,
        IEnumerable<Holder<StructureSet>> structureOverrides)
    {
        ArgumentNullException.ThrowIfNull(structureOverrides);

        Holder<StructureSet>[] structures = structureOverrides
            .Where(holder => HasBiomesForStructureSet(holder.Value, biomeSource))
            .ToArray();

        return new ChunkGeneratorStructureState(
            randomState,
            biomeSource,
            levelSeed,
            origin,
            0L,
            Array.AsReadOnly(structures));
    }

    private static bool HasBiomesForStructureSet(
        StructureSet structureSet,
        MultiNoiseBiomeSource biomeSource)
    {
        ArgumentNullException.ThrowIfNull(structureSet);
        ArgumentNullException.ThrowIfNull(biomeSource);

        foreach (StructureSet.StructureSelectionEntry entry in structureSet.Structures)
        {
            foreach (Holder<Biome> biome in entry.Structure.Value.Biomes())
            {
                if (biomeSource.PossibleBiomes.Contains(biome))
                    return true;
            }
        }

        return false;
    }

    public IReadOnlyList<Holder<StructureSet>> PossibleStructureSets() =>
        _possibleStructureSets;

    public ChunkPos GetDimensionOrigin() =>
        _origin;

    public long LevelSeed =>
        _levelSeed;

    public RandomState RandomState() =>
        _randomState;

    public long GetLevelSeed() =>
        _levelSeed;

    /// <summary>
    /// Builds the structure-to-placement index once, preserving source set order
    /// and selection-entry order. Only structures whose biome set intersects the
    /// biome source's possible-biome set are indexed.
    /// </summary>
    public void EnsureStructuresGenerated()
    {
        if (_hasGeneratedPositions)
            return;

        IReadOnlySet<Holder<Biome>> possibleBiomes = _biomeSource.PossibleBiomes;

        foreach (Holder<StructureSet> setHolder in _possibleStructureSets)
        {
            StructureSet structureSet = setHolder.Value;

            foreach (StructureSet.StructureSelectionEntry entry in structureSet.Structures)
            {
                Structure structure = entry.Structure.Value;
                if (!structure.Biomes().Any(possibleBiomes.Contains))
                    continue;

                if (!_placementsForStructure.TryGetValue(
                        structure,
                        out List<StructurePlacement>? placements))
                {
                    placements = new List<StructurePlacement>();
                    _placementsForStructure.Add(structure, placements);
                }

                placements.Add(structureSet.Placement);
            }
        }

        _hasGeneratedPositions = true;
    }

    public IReadOnlyList<StructurePlacement> GetPlacementsForStructure(
        Holder<Structure> structure)
    {
        ArgumentNullException.ThrowIfNull(structure);
        EnsureStructuresGenerated();

        return _placementsForStructure.TryGetValue(
            structure.Value,
            out List<StructurePlacement>? placements)
            ? placements
            : Array.Empty<StructurePlacement>();
    }

    /// <summary>
    /// Checks the square, inclusive chunk range in the original X-outer/Z-inner order.
    /// The structure placement itself owns candidate, frequency and exclusion checks.
    /// </summary>
    public bool HasStructureChunkInRange(
        Holder<StructureSet> structureSet,
        int sourceX,
        int sourceZ,
        int chunkCount)
    {
        ArgumentNullException.ThrowIfNull(structureSet);

        StructurePlacement placement = structureSet.Value.Placement;

        int minX = unchecked(sourceX - chunkCount);
        int maxX = unchecked(sourceX + chunkCount);
        int minZ = unchecked(sourceZ - chunkCount);
        int maxZ = unchecked(sourceZ + chunkCount);

        for (int testX = minX; testX <= maxX; testX++)
        {
            for (int testZ = minZ; testZ <= maxZ; testZ++)
            {
                if (placement.IsStructureChunk(this, testX, testZ))
                    return true;
            }
        }

        return false;
    }
}
