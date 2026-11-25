using Asteria.Content.Registries;

namespace Asteria.World.LevelGen.Structure.Placement;

/// <summary>
/// Narrow C# dependency surface used by StructurePlacement. A concrete
/// ChunkGeneratorStructureState will implement both members when its indexed
/// placement-position state is ported.
/// </summary>
public interface IStructurePlacementState
{
    long LevelSeed { get; }

    bool HasStructureChunkInRange(
        Holder<StructureSet> otherSet,
        int sourceX,
        int sourceZ,
        int chunkCount);
}
