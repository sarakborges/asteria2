using Asteria.Serialization;
using Asteria.Core;
using Asteria.World;
using Asteria.World.LevelGen.Structure;

namespace Asteria.World.LevelGen.Structure.Placement;

/// <summary>
/// Places one structure candidate exactly at the dimension origin, matching
/// Minecraft Java 26.3 DimensionOriginStructurePlacement.
/// </summary>
public sealed class DimensionOriginStructurePlacement : StructurePlacement
{
    public static readonly DimensionOriginStructurePlacement INSTANCE = new();

    public static MapCodec<DimensionOriginStructurePlacement> MAP_CODEC { get; } =
        MapCodec<DimensionOriginStructurePlacement>.Unit(INSTANCE);

    public static MapCodec<StructurePlacement> PLACEMENT_CODEC { get; } =
        MAP_CODEC.XMap<StructurePlacement>(
            static value => value,
            static placement => placement as DimensionOriginStructurePlacement ??
                throw new InvalidOperationException(
                    $"Expected {nameof(DimensionOriginStructurePlacement)}, received {placement.GetType().Name}."));

    private DimensionOriginStructurePlacement()
    {
    }

    public bool IsStructureChunk(
        IStructurePlacementState state,
        int sourceX,
        int sourceZ)
    {
        ArgumentNullException.ThrowIfNull(state);

        ChunkPos origin = state.GetDimensionOrigin();
        return origin.X == sourceX && origin.Z == sourceZ;
    }

    public Vec3i LocateOffset() =>
        Vec3i.Zero;

    public BlockPos GetLocatePos(ChunkPos chunkPos)
    {
        BlockPos origin = new(
            chunkPos.GetMinBlockX(),
            0,
            chunkPos.GetMinBlockZ());

        return origin.Offset(LocateOffset());
    }

    public MapCodec<StructurePlacement> Codec() =>
        PLACEMENT_CODEC;
}
