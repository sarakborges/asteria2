using Asteria.Serialization;
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

    public MapCodec<DimensionOriginStructurePlacement> Codec() =>
        MAP_CODEC;
}
