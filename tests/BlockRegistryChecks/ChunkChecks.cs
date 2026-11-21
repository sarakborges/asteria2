using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Storage;
using Asteria.Core;
using Asteria.World;

internal static class ChunkChecks
{
    public static void Run(BlockState nonAirState)
    {
        ArgumentNullException.ThrowIfNull(nonAirState);

        CheckHeightAccessor();
        CheckChunkConstructionAndSections(nonAirState);
        CheckMismatchedSectionArray();
    }

    private static void CheckHeightAccessor()
    {
        LevelHeightAccessor height = LevelHeightAccessor.Create(-64, 384);

        Require(height.GetMinY() == -64, "Height accessor must preserve the minimum build Y.");
        Require(height.GetHeight() == 384, "Height accessor must preserve the build height.");
        Require(height.GetMaxY() == 319, "Height accessor maximum must be inclusive.");
        Require(height.GetMinSectionY() == -4, "Minimum section Y must use arithmetic block-to-section conversion.");
        Require(height.GetMaxSectionY() == 19, "Maximum section Y must derive from the inclusive maximum block Y.");
        Require(height.GetSectionsCount() == 24, "A 384-block height must cover 24 vertical sections.");
        Require(height.GetSectionIndex(-64) == 0, "Minimum block Y must map to the first section index.");
        Require(height.GetSectionIndex(319) == 23, "Maximum block Y must map to the last section index.");
        Require(height.GetSectionIndexFromSectionY(-4) == 0, "Minimum section Y must map to index zero.");
        Require(height.GetSectionIndexFromSectionY(19) == 23, "Maximum section Y must map to the final index.");
        Require(height.GetSectionYFromSectionIndex(23) == 19, "Section indices must convert back to world section coordinates.");
        Require(height.IsInsideBuildHeight(-64) && height.IsInsideBuildHeight(319), "Build-height checks must include both boundaries.");
        Require(height.IsOutsideBuildHeight(-65) && height.IsOutsideBuildHeight(320), "Outside-build-height checks must reject values beyond either boundary.");
        Require(height.IsInsideBuildHeight(new BlockPos(0, -64, 0)), "BlockPos height checks must use Y only.");
        Require(height.IsOutsideBuildHeight(new BlockPos(0, 320, 0)), "BlockPos outside-height checks must use Y only.");

        LevelHeightAccessor unaligned = LevelHeightAccessor.Create(-63, 48);
        Require(unaligned.GetMinSectionY() == -4, "Unaligned minimum Y must floor to its containing section.");
        Require(unaligned.GetMaxSectionY() == -1, "Unaligned maximum Y must floor to its containing section.");
        Require(unaligned.GetSectionsCount() == 4, "Unaligned build bounds must count every intersecting section.");
        Require(unaligned.GetSectionIndex(-63) == 0, "Unaligned minimum Y must map to section index zero.");
        Require(!unaligned.IsInsideBuildHeight(-64), "Section membership must not widen the build-height bounds.");
    }

    private static void CheckChunkConstructionAndSections(BlockState nonAirState)
    {
        LevelHeightAccessor height = LevelHeightAccessor.Create(-64, 384);
        BlockStateContainerFactory factory = BlockStateContainerFactory.CreateBuiltIn();
        var position = new ChunkPos(-3, 7);

        var chunk = new Chunk(position, height, factory);
        Require(chunk.GetPos() == position, "Chunk must retain its ChunkPos.");
        Require(chunk.GetHeight() == 384 && chunk.GetMinY() == -64, "Chunk must delegate vertical bounds to its height accessor.");
        Require(chunk.GetSections().Length == 24, "Chunk must allocate one section for each intersecting section Y.");
        Require(ReferenceEquals(chunk.GetSections(), chunk.GetSections()), "ChunkAccess section retrieval must expose its backing section array.");
        Require(ReferenceEquals(chunk.GetSection(0), chunk.GetSections()[0]), "Chunk section lookup must use the section-array index directly.");
        Require(!ReferenceEquals(chunk.GetSection(0), chunk.GetSection(1)), "Missing sections must be materialized independently.");
        Require(chunk.GetHighestFilledSectionIndex() == Chunk.NoFilledSection, "An all-air chunk must report no filled section.");
#pragma warning disable CS0618
        Require(chunk.GetHighestSectionPosition() == -64, "An all-air chunk's highest section position must fall back to minimum Y.");
#pragma warning restore CS0618
        Require(chunk.IsYSpaceEmpty(int.MinValue, int.MaxValue), "An all-air chunk must report its entire build range as empty.");
        Require(
            ReferenceEquals(
                chunk.GetBlockState(new BlockPos(position.GetMinBlockX(), -64, position.GetMinBlockZ())),
                Blocks.AIR.DefaultState),
            "Block lookup in an empty section must return the factory's default air state.");
        Require(
            ReferenceEquals(
                chunk.GetBlockState(new BlockPos(position.GetMinBlockX(), -65, position.GetMinBlockZ())),
                Blocks.AIR.DefaultState),
            "Block lookup below the section array must return the default air state.");
        Require(
            ReferenceEquals(
                chunk.GetBlockState(new BlockPos(position.GetMinBlockX(), 320, position.GetMinBlockZ())),
                Blocks.AIR.DefaultState),
            "Block lookup above the section array must return the default air state.");

        var suppliedSection = factory.CreateSection();
        var supplied = new ChunkSection?[24];
        supplied[4] = suppliedSection;
        var fromSections = new Chunk(position, height, factory, supplied);

        Require(!ReferenceEquals(supplied, fromSections.GetSections()), "Chunk construction must copy the supplied sections array.");
        Require(ReferenceEquals(fromSections.GetSection(4), suppliedSection), "Chunk construction must retain supplied section instances.");
        Require(fromSections.GetSection(0).HasOnlyAir, "Missing entries in a supplied section array must become empty sections.");
        supplied[4] = null;
        Require(ReferenceEquals(fromSections.GetSection(4), suppliedSection), "Later edits to the source array must not change the chunk's section references.");

        var occupiedChunk = new Chunk(position, height, factory);
        BlockPos occupiedPosition = position.GetBlockAt(15, 15, 9);
        occupiedChunk.GetSection(4).SetBlockState(15, 15, 9, nonAirState);
        Require(
            ReferenceEquals(occupiedChunk.GetBlockState(occupiedPosition), nonAirState),
            "Chunk block lookup must route world coordinates to the correct section-local coordinates.");

        BlockPos aliasedPosition = new(
            occupiedPosition.GetX() + 16,
            occupiedPosition.GetY(),
            occupiedPosition.GetZ() - 32);
        Require(
            ReferenceEquals(occupiedChunk.GetBlockState(aliasedPosition), nonAirState),
            "Chunk block lookup must preserve vanilla local-coordinate masking without adding an X/Z ownership check.");

        Require(occupiedChunk.GetHighestFilledSectionIndex() == 4, "Highest filled section must return the greatest non-air section-array index.");
#pragma warning disable CS0618
        Require(occupiedChunk.GetHighestSectionPosition() == 0, "Highest section position must convert the section index back to world block Y.");
#pragma warning restore CS0618
        Require(!occupiedChunk.IsYSpaceEmpty(0, 15), "Y-space emptiness must detect a non-air section.");
        Require(occupiedChunk.IsYSpaceEmpty(16, 319), "Y-space emptiness must ignore non-air sections outside the requested interval.");
        Require(!occupiedChunk.IsYSpaceEmpty(-100, 15), "Y-space emptiness must clamp the lower bound and scan intersecting sections.");
    }

    private static void CheckMismatchedSectionArray()
    {
        LevelHeightAccessor height = LevelHeightAccessor.Create(-64, 384);
        BlockStateContainerFactory factory = BlockStateContainerFactory.CreateBuiltIn();
        var supplied = new ChunkSection?[23];
        supplied[0] = factory.CreateSection();

        var chunk = new Chunk(new ChunkPos(0, 0), height, factory, supplied);
        Require(chunk.GetSections().Length == 24, "Chunk section storage length must come from the height accessor.");
        Require(chunk.GetSection(0).HasOnlyAir, "A mismatched supplied section array must be discarded before missing sections are filled.");
        Require(!ReferenceEquals(chunk.GetSection(0), supplied[0]), "A mismatched supplied section array must not partially populate the chunk.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
