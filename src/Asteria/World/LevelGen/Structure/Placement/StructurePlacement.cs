using Asteria.Core;
using Asteria.World;

namespace Asteria.World.LevelGen.Structure.Placement;

/// <summary>
/// Common placement contract from Minecraft Java 26.3.
/// The state interface exposes only the structure-placement queries used by this contract;
/// the concrete ChunkGeneratorStructureState port supplies its full algorithm separately.
/// </summary>
public interface StructurePlacement
{
    bool IsStructureChunk(
        IStructurePlacementState state,
        int sourceX,
        int sourceZ);

    bool ApplyAdditionalChunkRestrictions(
        int sourceX,
        int sourceZ,
        long levelSeed);

    BlockPos GetLocatePos(ChunkPos chunkPos);

    Vec3i LocateOffset();
}
