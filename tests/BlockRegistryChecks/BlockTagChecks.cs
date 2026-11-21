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

        TagKey<Block> markerTag = TagKey<Block>.Create(
            BuiltInRegistries.Block.Key,
            ResourceLocation.Parse("asteria:test_tag_membership"));
        var definitions = new[]
        {
            new KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>(
                BlockTags.BlocksMotionInHeightmap,
                Array.Empty<Holder<Block>>()),
            new KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>(
                BlockTags.BlocksMotionInHeightmapNoLeaves,
                Array.Empty<Holder<Block>>()),
            new KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>(
                BlockTags.Leaves,
                Array.Empty<Holder<Block>>()),
            new KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>(
                markerTag,
                new Holder<Block>[] { airHolder })
        };

        PendingTagReload<Block> pending = BuiltInRegistries.Block.PrepareTagReload(definitions);
        Require(pending.Size == definitions.Length, "Pending tag reload must retain each block tag.");
        Require(
            !airHolder.Is(markerTag),
            "Holder tag membership must not change before a pending reload is applied.");

        pending.Apply();

        Require(
            airHolder.Is(markerTag) && Blocks.AIR.DefaultState.Is(markerTag),
            "Applying a tag reload must expose membership through both block holders and block states.");
        Require(
            !Blocks.AIR.DefaultState.Is(BlockTags.BlocksMotionInHeightmap) &&
            !Blocks.AIR.DefaultState.Is(BlockTags.BlocksMotionInHeightmapNoLeaves) &&
            !Blocks.AIR.DefaultState.Is(BlockTags.Leaves),
            "Empty heightmap and leaves tags must remain empty rather than inventing built-in membership.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
