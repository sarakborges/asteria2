using Asteria.Content.Blocks;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;

internal static class BlockTagChecks
{
    public static void Run()
    {
        Holder<Block>.Reference airHolder = Blocks.AIR.BuiltInRegistryHolder ??
            throw new InvalidOperationException("The built-in air block must own an intrusive registry holder.");

        Require(
            BlockTags.BlocksMotionInHeightmap.Registry == BuiltInRegistries.Block.Key &&
            BlockTags.BlocksMotionInHeightmapNoLeaves.Registry == BuiltInRegistries.Block.Key &&
            BlockTags.Leaves.Registry == BuiltInRegistries.Block.Key,
            "Heightmap block tags must target the built-in block registry.");

        Require(
            BlockTags.BlocksMotionInHeightmap.Location == ResourceLocation.Parse("asteria:blocks_motion_in_heightmap") &&
            BlockTags.BlocksMotionInHeightmapNoLeaves.Location == ResourceLocation.Parse("asteria:blocks_motion_in_heightmap_no_leaves") &&
            BlockTags.Leaves.Location == ResourceLocation.Parse("asteria:leaves"),
            "Block-tag locations must preserve the canonical tag paths in Asteria's namespace.");

        var definitions = new[]
        {
            new KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>(
                BlockTags.BlocksMotionInHeightmap,
                new Holder<Block>[] { airHolder }),
            new KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>(
                BlockTags.BlocksMotionInHeightmapNoLeaves,
                new Holder<Block>[] { airHolder }),
            new KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>(
                BlockTags.Leaves,
                new Holder<Block>[] { airHolder })
        };

        PendingTagReload<Block> pending = BuiltInRegistries.Block.PrepareTagReload(definitions);
        Require(pending.Size == definitions.Length, "Pending tag reload must retain each heightmap-related tag.");
        Require(
            !airHolder.Is(BlockTags.BlocksMotionInHeightmap),
            "Holder tag membership must not change before a pending reload is applied.");

        pending.Apply();

        Require(
            airHolder.Is(BlockTags.BlocksMotionInHeightmap) &&
            airHolder.Is(BlockTags.BlocksMotionInHeightmapNoLeaves) &&
            airHolder.Is(BlockTags.Leaves),
            "Applying a tag reload must refresh tag membership in block holders.");
        Require(
            Blocks.AIR.DefaultState.Is(BlockTags.BlocksMotionInHeightmap) &&
            Blocks.AIR.DefaultState.Is(BlockTags.BlocksMotionInHeightmapNoLeaves) &&
            Blocks.AIR.DefaultState.Is(BlockTags.Leaves),
            "BlockState.Is(tag) must delegate membership to its block's registry holder.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
