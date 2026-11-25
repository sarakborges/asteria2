using Asteria.Core;
using Asteria.World;
using Asteria.Serialization;

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
        long levelSeed) =>
        true;

    BlockPos GetLocatePos(ChunkPos chunkPos)
    {
        BlockPos origin = new(
            chunkPos.GetMinBlockX(),
            0,
            chunkPos.GetMinBlockZ());

        return origin.Offset(LocateOffset());
    }

    Vec3i LocateOffset() =>
        Vec3i.Zero;

    MapCodec<StructurePlacement> Codec() =>
        throw new NotSupportedException(
            $"No placement codec has been ported for {GetType().Name}.");
}
