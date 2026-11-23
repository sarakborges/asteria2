using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Content.Materials;
using Asteria.Core;
using Asteria.Core.Shapes;
using Asteria.World;

namespace Asteria.Content.Blocks;

/// <summary>
/// Built-in Asteria block definitions.
/// </summary>
public static class Blocks
{
    public static Block AIR { get; } = new AirBlock(
        BlockProperties.Of().Replaceable().NoCollision().Air());

    public static Block STONE { get; } = new(
        BlockProperties.Of().MapColor(MapColor.STONE).Strength(1.5f, 6.0f));

    public static Block DIRT { get; } = new(
        BlockProperties.Of().MapColor(MapColor.DIRT).Strength(0.5f));

    public static Block GRASS_BLOCK { get; } = new(
        BlockProperties.Of().MapColor(MapColor.GRASS).Strength(0.6f));

    internal static void RegisterAll()
    {
        Register("air", AIR);
        Register("stone", STONE);
        Register("dirt", DIRT);
        Register("grass_block", GRASS_BLOCK);
    }

    private static void Register(string path, Block block) =>
        Registry.Register(
            BuiltInRegistries.Block,
            ResourceLocation.FromNamespaceAndPath("asteria", path),
            block);

    private sealed class AirBlock : Block
    {
        public AirBlock(BlockProperties properties)
            : base(properties)
        {
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos position) =>
            Shapes.Empty();
    }
}
