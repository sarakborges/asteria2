using Asteria.Core;
using Asteria.Serialization;
using Asteria.World;
using Asteria.World.LevelGen;

namespace Asteria.World.LevelGen.Placement;

/// <summary>Moves a placement position to the first available block above a heightmap.</summary>
public sealed class HeightmapPlacement : PlacementModifier
{
    public Heightmap.Types Heightmap { get; }

    public static MapCodec<HeightmapPlacement> MAP_CODEC { get; } =
        Heightmap.Types.CODEC
            .FieldOf("heightmap")
            .XMap<HeightmapPlacement>(
                static heightmap => new HeightmapPlacement(heightmap),
                static placement => placement.Heightmap);

    public static MapCodec<PlacementModifier> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<PlacementModifier>(
            static modifier => modifier,
            static modifier => (HeightmapPlacement)modifier);

    public HeightmapPlacement(Heightmap.Types heightmap) =>
        Heightmap = heightmap ?? throw new ArgumentNullException(nameof(heightmap));

    public static HeightmapPlacement OnHeightmap(Heightmap.Types heightmap) =>
        new(heightmap);

    public void Modify(
        PlacementContext context,
        IRandomSource random,
        BlockPos origin,
        Action<BlockPos> output)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(output);

        int x = origin.GetX();
        int z = origin.GetZ();
        int height = context.GetHeight(Heightmap, x, z);
        if (height > context.GetMinY())
            output(new BlockPos(x, height, z));
    }

    public MapCodec<PlacementModifier> Codec() => TYPE_CODEC;
}
