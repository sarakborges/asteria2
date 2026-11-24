using Asteria.Content.Registries;

using BuiltInBlocks = Asteria.Content.Blocks.Blocks;

namespace Asteria.Content.Blocks;

/// <summary>
/// Binds the Minecraft 26.3 block-tag closure projected onto block types currently
/// registered by Asteria. Unsupported vanilla block entries are intentionally absent
/// until their block definitions are implemented.
/// </summary>
public static class BuiltInBlockTags
{
    public static void BindSupportedValues()
    {
        Holder<Block>[] motionWithoutLeaves =
        [
            GetHolder(BuiltInBlocks.BEDROCK),
            GetHolder(BuiltInBlocks.CALCITE),
            GetHolder(BuiltInBlocks.CINNABAR),
            GetHolder(BuiltInBlocks.COPPER_ORE),
            GetHolder(BuiltInBlocks.COARSE_DIRT),
            GetHolder(BuiltInBlocks.DEEPSLATE),
            GetHolder(BuiltInBlocks.DEEPSLATE_IRON_ORE),
            GetHolder(BuiltInBlocks.DIRT),
            GetHolder(BuiltInBlocks.GRANITE),
            GetHolder(BuiltInBlocks.GRASS_BLOCK),
            GetHolder(BuiltInBlocks.GRAVEL),
            GetHolder(BuiltInBlocks.ICE),
            GetHolder(BuiltInBlocks.MUD),
            GetHolder(BuiltInBlocks.MYCELIUM),
            GetHolder(BuiltInBlocks.ORANGE_TERRACOTTA),
            GetHolder(BuiltInBlocks.PACKED_ICE),
            GetHolder(BuiltInBlocks.PODZOL),
            GetHolder(BuiltInBlocks.RAW_COPPER_BLOCK),
            GetHolder(BuiltInBlocks.RAW_IRON_BLOCK),
            GetHolder(BuiltInBlocks.RED_SAND),
            GetHolder(BuiltInBlocks.RED_SANDSTONE),
            GetHolder(BuiltInBlocks.RED_TERRACOTTA),
            GetHolder(BuiltInBlocks.SAND),
            GetHolder(BuiltInBlocks.SANDSTONE),
            GetHolder(BuiltInBlocks.SNOW_BLOCK),
            GetHolder(BuiltInBlocks.STONE),
            GetHolder(BuiltInBlocks.SULFUR),
            GetHolder(BuiltInBlocks.TERRACOTTA),
            GetHolder(BuiltInBlocks.TUFF),
            GetHolder(BuiltInBlocks.WHITE_TERRACOTTA),
            GetHolder(BuiltInBlocks.YELLOW_TERRACOTTA),
            GetHolder(BuiltInBlocks.BROWN_TERRACOTTA),
            GetHolder(BuiltInBlocks.LIGHT_GRAY_TERRACOTTA)
        ];

        Holder<Block>[] uncarvable =
        [
            GetHolder(BuiltInBlocks.BEDROCK)
        ];

        KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>[] definitions =
        [
            new(BlockTags.Uncarvable, uncarvable),
            new(BlockTags.BlocksMotionInHeightmap, motionWithoutLeaves),
            new(BlockTags.BlocksMotionInHeightmapNoLeaves, motionWithoutLeaves),
            new(BlockTags.Leaves, Array.Empty<Holder<Block>>()),
            new(BlockTags.FallDamageResetting, Array.Empty<Holder<Block>>()),
            new(BlockTags.CausesSuffocation, motionWithoutLeaves)
        ];

        BuiltInRegistries.Block.PrepareTagReload(definitions).Apply();
    }

    private static Holder<Block> GetHolder(Block block) =>
        block.BuiltInRegistryHolder ??
        throw new InvalidOperationException(
            $"Built-in block {block} must have a registry holder before its tags can be bound.");
}
