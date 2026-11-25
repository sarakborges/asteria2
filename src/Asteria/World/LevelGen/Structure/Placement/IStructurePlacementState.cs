using Asteria.Content.Registries;
using Asteria.World;

namespace Asteria.World.LevelGen.Structure.Placement;

/// <summary>
/// Narrow C# dependency surface used by StructurePlacement. A concrete
/// ChunkGeneratorStructureState will implement both members when its indexed
/// placement-position state is ported.
/// </summary>
public interface IStructurePlacementState
{
    long LevelSeed { get; }

    ChunkPos GetDimensionOrigin();

    bool HasStructureChunkInRange(
        Holder<StructureSet> otherSet,
        int sourceX,
        int sourceZ,
        int chunkCount);
}
