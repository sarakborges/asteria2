using Asteria.Content.Blocks;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Core.Shapes;
using Asteria.World;

internal static class BlockTagChecks
{
    public static void Run()
    {
        Holder<Block>.Reference airHolder = Blocks.AIR.BuiltInRegistryHolder ??
            throw new InvalidOperationException("The built-in air block must own an intrusive registry holder.");
        TagKey<Block> causesSuffocation = BlockTags.CausesSuffocation;

        Require(
            BlockTags.BlocksMotionInHeightmap.Registry == BuiltInRegistries.Block.Key &&
            BlockTags.BlocksMotionInHeightmapNoLeaves.Registry == BuiltInRegistries.Block.Key &&
            BlockTags.Leaves.Registry == BuiltInRegistries.Block.Key &&
            BlockTags.FallDamageResetting.Registry == BuiltInRegistries.Block.Key &&
            causesSuffocation.Registry == BuiltInRegistries.Block.Key,
            "Heightmap block tags must target the built-in block registry.");

        Require(
            BlockTags.BlocksMotionInHeightmap.Location == ResourceLocation.Parse("asteria:blocks_motion_in_heightmap") &&
            BlockTags.BlocksMotionInHeightmapNoLeaves.Location == ResourceLocation.Parse("asteria:blocks_motion_in_heightmap_no_leaves") &&
            BlockTags.Leaves.Location == ResourceLocation.Parse("asteria:leaves") &&
            BlockTags.FallDamageResetting.Location == ResourceLocation.Parse("asteria:fall_damage_resetting") &&
            causesSuffocation.Location == ResourceLocation.Parse("asteria:causes_suffocation"),
            "Block-tag locations must preserve the canonical tag paths in Asteria's namespace.");

        Block[] expectedMotionBlocks =
        [
            Blocks.BEDROCK,
            Blocks.CALCITE,
            Blocks.CINNABAR,
            Blocks.COPPER_ORE,
            Blocks.COARSE_DIRT,
            Blocks.DEEPSLATE,
            Blocks.DEEPSLATE_IRON_ORE,
            Blocks.DIRT,
            Blocks.GRANITE,
            Blocks.GRASS_BLOCK,
            Blocks.GRAVEL,
            Blocks.ICE,
            Blocks.MUD,
            Blocks.MYCELIUM,
            Blocks.ORANGE_TERRACOTTA,
            Blocks.PACKED_ICE,
            Blocks.PODZOL,
            Blocks.RAW_COPPER_BLOCK,
            Blocks.RAW_IRON_BLOCK,
            Blocks.RED_SAND,
            Blocks.RED_SANDSTONE,
            Blocks.RED_TERRACOTTA,
            Blocks.SAND,
            Blocks.SANDSTONE,
            Blocks.SNOW_BLOCK,
            Blocks.STONE,
            Blocks.SULFUR,
            Blocks.TERRACOTTA,
            Blocks.TUFF,
            Blocks.WHITE_TERRACOTTA,
            Blocks.YELLOW_TERRACOTTA,
            Blocks.BROWN_TERRACOTTA,
            Blocks.LIGHT_GRAY_TERRACOTTA
        ];
        Require(
            expectedMotionBlocks.All(block =>
                block.DefaultState.Is(BlockTags.BlocksMotionInHeightmap) &&
                block.DefaultState.Is(BlockTags.BlocksMotionInHeightmapNoLeaves) &&
                block.DefaultState.Is(causesSuffocation)),
            "Built-in heightmap and suffocation tags must contain every supported block in the vanilla 26.3 block-motion tag closure.");
        Require(
            new[] { Blocks.AIR, Blocks.VOID_AIR, Blocks.WATER, Blocks.LAVA, Blocks.POWDER_SNOW }
                .All(block =>
                    !block.DefaultState.Is(BlockTags.BlocksMotionInHeightmap) &&
                    !block.DefaultState.Is(BlockTags.BlocksMotionInHeightmapNoLeaves) &&
                    !block.DefaultState.Is(causesSuffocation)),
            "Air, fluids and powder snow must remain outside the built-in block-motion tags.");
        Require(
            !Blocks.AIR.DefaultState.Is(BlockTags.Leaves) &&
            !Blocks.STONE.DefaultState.Is(BlockTags.FallDamageResetting),
            "The leaves and fall-damage-resetting tags must stay empty while their vanilla block types are not implemented.");

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
                BlockTags.FallDamageResetting,
                Array.Empty<Holder<Block>>()),
            new KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>(
                causesSuffocation,
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

        int predicateCalls = 0;
        Require(
            Blocks.AIR.DefaultState.Is(markerTag, state => ReferenceEquals(state, Blocks.AIR.DefaultState)),
            "BlockState.Is(tag, predicate) must evaluate the predicate for a matching tag member.");
        Require(
            !Blocks.AIR.DefaultState.Is(markerTag, _ => false),
            "BlockState.Is(tag, predicate) must return false when a matching member fails the predicate.");
        Require(
            !Blocks.STONE.DefaultState.Is(markerTag, _ =>
            {
                predicateCalls++;
                return true;
            }) && predicateCalls == 0,
            "BlockState.Is(tag, predicate) must short-circuit without evaluating the predicate for a non-member.");
        Require(
            !Blocks.AIR.DefaultState.Is(BlockTags.BlocksMotionInHeightmap) &&
            !Blocks.AIR.DefaultState.Is(BlockTags.BlocksMotionInHeightmapNoLeaves) &&
            !Blocks.AIR.DefaultState.Is(BlockTags.Leaves) &&
            !Blocks.STONE.DefaultState.Is(BlockTags.FallDamageResetting),
            "Empty heightmap, leaves and fall-damage reset tags must remain empty rather than inventing built-in membership.");

        var fallDamageContext = new ClipContext(
            new Vec3(0.5, 0.5, 0.5),
            new Vec3(1.5, 0.5, 0.5),
            ClipContext.Block.FallDamageResetting,
            ClipContext.Fluid.None);
        Require(
            fallDamageContext.GetBlockShape(
                Blocks.STONE.DefaultState,
                EmptyBlockGetter.Instance,
                BlockPos.Zero).IsEmpty(),
            "Fall-damage-resetting clipping must ignore blocks outside its tag when no entity context is supplied.");

        Holder<Block>.Reference stoneHolder = Blocks.STONE.BuiltInRegistryHolder ??
            throw new InvalidOperationException("The built-in stone block must own an intrusive registry holder.");
        var fallDamageDefinitions = new[]
        {
            new KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>(
                BlockTags.FallDamageResetting,
                new Holder<Block>[] { stoneHolder }),
            new KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>(
                causesSuffocation,
                Array.Empty<Holder<Block>>())
        };
        BuiltInRegistries.Block.PrepareTagReload(fallDamageDefinitions).Apply();

        Require(
            Shapes.Equal(
                Shapes.Block(),
                fallDamageContext.GetBlockShape(
                    Blocks.STONE.DefaultState,
                    EmptyBlockGetter.Instance,
                    BlockPos.Zero)),
            "Fall-damage-resetting clipping must use a full block shape for tagged blocks.");

        BuiltInRegistries.Block.PrepareTagReload(definitions).Apply();
        BuiltInBlockTags.BindSupportedValues();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
