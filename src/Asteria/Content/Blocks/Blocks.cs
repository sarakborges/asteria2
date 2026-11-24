using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Content.Materials;
using BuiltInFluids = Asteria.Content.Fluids.Fluids;
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

    public static Block VOID_AIR { get; } = new AirBlock(
        BlockProperties.Of().Replaceable().NoCollision().Air());

    public static Block STONE { get; } = new(
        BlockProperties.Of().MapColor(MapColor.STONE).RequiresCorrectToolForDrops().Strength(1.5f, 6.0f));

    public static Block DIRT { get; } = new(
        BlockProperties.Of().MapColor(MapColor.DIRT).Strength(0.5f));

    public static Block GRASS_BLOCK { get; } = new(
        BlockProperties.Of().MapColor(MapColor.GRASS).RandomTicks().Strength(0.6f));

    public static Block WATER { get; } = new LiquidBlock(
        CreateLiquidProperties(MapColor.WATER),
        BuiltInFluids.WATER);

    public static Block LAVA { get; } = new LiquidBlock(
        CreateLiquidProperties(MapColor.FIRE),
        BuiltInFluids.LAVA);


    public static Block BEDROCK { get; } = new(
        BlockProperties.Of().MapColor(MapColor.STONE).Strength(-1.0f, 3_600_000.0f));

    public static Block COPPER_ORE { get; } = new(
        BlockProperties.Of().MapColor(MapColor.STONE).RequiresCorrectToolForDrops().Strength(3.0f, 3.0f));

    public static Block GRANITE { get; } = new(
        BlockProperties.Of().MapColor(MapColor.DIRT).RequiresCorrectToolForDrops().Strength(1.5f, 6.0f));

    public static Block RAW_COPPER_BLOCK { get; } = new(
        BlockProperties.Of().MapColor(MapColor.COLOR_ORANGE).RequiresCorrectToolForDrops().Strength(5.0f, 6.0f));

    public static Block GRAVEL { get; } = new(
        BlockProperties.Of().MapColor(MapColor.STONE).Strength(0.6f));

    public static Block DEEPSLATE_IRON_ORE { get; } = new(
        BlockProperties.Of().MapColor(MapColor.DEEPSLATE).RequiresCorrectToolForDrops().Strength(4.5f, 3.0f));

    public static Block RAW_IRON_BLOCK { get; } = new(
        BlockProperties.Of().MapColor(MapColor.RAW_IRON).RequiresCorrectToolForDrops().Strength(5.0f, 6.0f));

    public static Block TUFF { get; } = new(
        BlockProperties.Of().MapColor(MapColor.TERRACOTTA_GRAY).RequiresCorrectToolForDrops().Strength(1.5f, 6.0f));

    public static Block POWDER_SNOW { get; } = new(
        BlockProperties.Of().MapColor(MapColor.SNOW).Strength(0.25f).DynamicShape().NoOcclusion()
            .IsRedstoneConductor(static (_, _, _) => false));

    public static Block SAND { get; } = new(
        BlockProperties.Of().MapColor(MapColor.SAND).Strength(0.5f));

    public static Block SANDSTONE { get; } = new(
        BlockProperties.Of().MapColor(MapColor.SAND).RequiresCorrectToolForDrops().Strength(0.8f));

    public static Block CINNABAR { get; } = new(
        BlockProperties.Of().MapColor(MapColor.COLOR_RED).RequiresCorrectToolForDrops().Strength(1.5f, 6.0f));

    public static Block SULFUR { get; } = new(
        BlockProperties.Of().MapColor(MapColor.COLOR_YELLOW).RequiresCorrectToolForDrops().Strength(1.5f, 6.0f));

    public static Block COARSE_DIRT { get; } = new(
        BlockProperties.Of().MapColor(MapColor.DIRT).Strength(0.5f));

    public static Block ORANGE_TERRACOTTA { get; } = new(
        BlockProperties.Of().MapColor(MapColor.TERRACOTTA_ORANGE).RequiresCorrectToolForDrops().Strength(1.25f, 4.2f));

    public static Block RED_SAND { get; } = new(
        BlockProperties.Of().MapColor(MapColor.COLOR_ORANGE).Strength(0.5f));

    public static Block RED_SANDSTONE { get; } = new(
        BlockProperties.Of().MapColor(MapColor.COLOR_ORANGE).RequiresCorrectToolForDrops().Strength(0.8f));

    public static Block TERRACOTTA { get; } = new(
        BlockProperties.Of().MapColor(MapColor.COLOR_ORANGE).RequiresCorrectToolForDrops().Strength(1.25f, 4.2f));

    public static Block WHITE_TERRACOTTA { get; } = new(
        BlockProperties.Of().MapColor(MapColor.TERRACOTTA_WHITE).RequiresCorrectToolForDrops().Strength(1.25f, 4.2f));

    public static Block DEEPSLATE { get; } = new(
        BlockProperties.Of().MapColor(MapColor.DEEPSLATE).RequiresCorrectToolForDrops().Strength(3.0f, 6.0f));

    public static Block ICE { get; } = new(
        BlockProperties.Of().MapColor(MapColor.ICE).Friction(0.98f).RandomTicks().Strength(0.5f).NoOcclusion());

    public static Block PACKED_ICE { get; } = new(
        BlockProperties.Of().MapColor(MapColor.ICE).Friction(0.98f).Strength(0.5f));

    public static Block SNOW_BLOCK { get; } = new(
        BlockProperties.Of().MapColor(MapColor.SNOW).RequiresCorrectToolForDrops().Strength(0.2f));

    public static Block MUD { get; } = new(
        BlockProperties.Of().MapColor(MapColor.TERRACOTTA_CYAN).Strength(0.5f));

    public static Block MYCELIUM { get; } = new(
        BlockProperties.Of().MapColor(MapColor.COLOR_PURPLE).RandomTicks().Strength(0.6f));

    public static Block PODZOL { get; } = new(
        BlockProperties.Of().MapColor(MapColor.PODZOL).Strength(0.5f));

    public static Block CALCITE { get; } = new(
        BlockProperties.Of().MapColor(MapColor.TERRACOTTA_WHITE).RequiresCorrectToolForDrops().Strength(0.75f));

    private static BlockProperties CreateLiquidProperties(MapColor mapColor)
    {
#pragma warning disable CS0618
        return BlockProperties.Of()
            .MapColor(mapColor)
            .Strength(100.0f)
            .Replaceable()
            .NoCollision()
            .Liquid();
#pragma warning restore CS0618
    }

    internal static void RegisterAll()
    {
        Register("air", AIR);
        Register("void_air", VOID_AIR);
        Register("stone", STONE);
        Register("dirt", DIRT);
        Register("grass_block", GRASS_BLOCK);
        Register("water", WATER);
        Register("lava", LAVA);

        Register("bedrock", BEDROCK);
        Register("copper_ore", COPPER_ORE);
        Register("granite", GRANITE);
        Register("raw_copper_block", RAW_COPPER_BLOCK);
        Register("gravel", GRAVEL);
        Register("deepslate_iron_ore", DEEPSLATE_IRON_ORE);
        Register("raw_iron_block", RAW_IRON_BLOCK);
        Register("tuff", TUFF);
        Register("powder_snow", POWDER_SNOW);
        Register("sand", SAND);
        Register("sandstone", SANDSTONE);
        Register("cinnabar", CINNABAR);
        Register("sulfur", SULFUR);
        Register("coarse_dirt", COARSE_DIRT);
        Register("orange_terracotta", ORANGE_TERRACOTTA);
        Register("red_sand", RED_SAND);
        Register("red_sandstone", RED_SANDSTONE);
        Register("terracotta", TERRACOTTA);
        Register("white_terracotta", WHITE_TERRACOTTA);
        Register("deepslate", DEEPSLATE);
        Register("ice", ICE);
        Register("packed_ice", PACKED_ICE);
        Register("snow_block", SNOW_BLOCK);
        Register("mud", MUD);
        Register("mycelium", MYCELIUM);
        Register("podzol", PODZOL);
        Register("calcite", CALCITE);
    }

    private static void Register(string path, Block block) =>
        Registry.Register(
            BuiltInRegistries.Block,
            ResourceLocation.FromNamespaceAndPath("minecraft", path),
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
