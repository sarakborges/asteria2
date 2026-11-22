using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Storage;
using Asteria.Core;
using Asteria.World;

internal static class ChunkChecks
{
    public static void Run(BlockState nonAirState, BlockState fluidState)
    {
        ArgumentNullException.ThrowIfNull(nonAirState);
        ArgumentNullException.ThrowIfNull(fluidState);

        CheckHeightAccessor();
        CheckChunkAccessLifecycle();
        CheckChunkConstructionAndSections(nonAirState);
        CheckMismatchedSectionArray();
        CheckWorldSurfaceHeightmap(nonAirState);
        CheckMotionBlockingHeightmaps(fluidState);
        CheckHeightmapOwnershipAndBatchPriming(nonAirState);
        CheckHeightmapRawData(nonAirState);
    }

    private static void CheckChunkAccessLifecycle()
    {
        LevelHeightAccessor height = LevelHeightAccessor.Create(-64, 384);
        TestChunkAccess chunk = new(
            new ChunkPos(0, 0),
            height,
            PalettedContainerFactory.CreateBuiltIn(),
            inhabitedTime: 120);

        Require(chunk.GetInhabitedTime() == 120, "ChunkAccess must retain the constructor's inhabited time.");
        chunk.IncrementInhabitedTime();
        Require(chunk.GetInhabitedTime() == 121, "IncrementInhabitedTime must increment by one.");
        chunk.SetInhabitedTime(0);
        Require(chunk.GetInhabitedTime() == 0, "SetInhabitedTime must replace the inhabited time.");

        Require(!chunk.IsUnsaved(), "A new ChunkAccess must start saved.");
        Require(!chunk.TryMarkSaved(), "Trying to save a clean ChunkAccess must return false.");
        chunk.MarkUnsaved();
        Require(chunk.IsUnsaved(), "MarkUnsaved must set the dirty state.");
        Require(chunk.TryMarkSaved(), "Trying to save a dirty ChunkAccess must return true.");
        Require(!chunk.IsUnsaved() && !chunk.TryMarkSaved(), "TryMarkSaved must clear the dirty state after a successful transition.");

        Require(!chunk.IsLightCorrect(), "A new ChunkAccess must start with lighting marked incorrect.");
        chunk.SetLightCorrect(true);
        Require(chunk.IsLightCorrect(), "SetLightCorrect must update the lighting state.");
        Require(chunk.IsUnsaved(), "SetLightCorrect must mark the chunk unsaved.");
        chunk.TryMarkSaved();
        chunk.SetLightCorrect(false);
        Require(!chunk.IsLightCorrect() && chunk.IsUnsaved(), "Changing light correctness must mark the chunk unsaved.");
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
        PalettedContainerFactory factory = PalettedContainerFactory.CreateBuiltIn();
        var position = new ChunkPos(-3, 7);

        PalettedContainerFactory nonAirDefaultFactory = factory with
        {
            DefaultBlockState = nonAirState
        };
        var defaultStateChunk = new TestChunkAccess(new ChunkPos(0, 0), height, nonAirDefaultFactory);
        Require(
            ReferenceEquals(
                defaultStateChunk.GetBlockState(new BlockPos(0, -65, 0)),
                Blocks.AIR.DefaultState),
            "Chunk block lookup must use the built-in air state even when the container factory has a different default state.");

        var chunk = new TestChunkAccess(position, height, factory);
        Require(chunk.GetPos() == position, "Chunk must retain its ChunkPos.");
        Require(chunk.GetHeight() == 384 && chunk.GetMinY() == -64, "Chunk must delegate vertical bounds to its height accessor.");
        Require(chunk.GetSections().Length == 24, "Chunk must allocate one section for each intersecting section Y.");
        Require(ReferenceEquals(chunk.GetSections(), chunk.GetSections()), "ChunkAccess section retrieval must expose its backing section array.");
        Require(ReferenceEquals(chunk.GetSection(0), chunk.GetSections()[0]), "Chunk section lookup must use the section-array index directly.");
        Require(!ReferenceEquals(chunk.GetSection(0), chunk.GetSection(1)), "Missing sections must be materialized independently.");
        Require(chunk.GetHighestFilledSectionIndex() == ChunkAccess.NoFilledSection, "An all-air chunk must report no filled section.");
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

        var suppliedSection = new ChunkSection(factory);
        var supplied = new ChunkSection?[24];
        supplied[4] = suppliedSection;
        var fromSections = new TestChunkAccess(position, height, factory, supplied);

        Require(!ReferenceEquals(supplied, fromSections.GetSections()), "Chunk construction must copy the supplied sections array.");
        Require(ReferenceEquals(fromSections.GetSection(4), suppliedSection), "Chunk construction must retain supplied section instances.");
        Require(fromSections.GetSection(0).HasOnlyAir, "Missing entries in a supplied section array must become empty sections.");
        supplied[4] = null;
        Require(ReferenceEquals(fromSections.GetSection(4), suppliedSection), "Later edits to the source array must not change the chunk's section references.");

        var occupiedChunk = new TestChunkAccess(position, height, factory);
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
        PalettedContainerFactory factory = PalettedContainerFactory.CreateBuiltIn();
        var supplied = new ChunkSection?[23];
        supplied[0] = new ChunkSection(factory);

        var chunk = new TestChunkAccess(new ChunkPos(0, 0), height, factory, supplied);
        Require(chunk.GetSections().Length == 24, "Chunk section storage length must come from the height accessor.");
        Require(chunk.GetSection(0).HasOnlyAir, "A mismatched supplied section array must be discarded before missing sections are filled.");
        Require(!ReferenceEquals(chunk.GetSection(0), supplied[0]), "A mismatched supplied section array must not partially populate the chunk.");
    }

    private static void CheckWorldSurfaceHeightmap(BlockState nonAirState)
    {
        LevelHeightAccessor height = LevelHeightAccessor.Create(-64, 384);
        PalettedContainerFactory factory = PalettedContainerFactory.CreateBuiltIn();
        var chunk = new TestChunkAccess(new ChunkPos(-2, 3), height, factory);
        Heightmap heightmap = chunk.GetOrCreateHeightmapUnprimed(Heightmap.Types.WORLD_SURFACE);

        Require(heightmap.GetRawData().Length == 37, "A 9-bit, 256-column heightmap must use 37 packed longs.");
        Require(heightmap.GetFirstAvailable(3, 5) == -64, "An unprimed heightmap column starts at minimum build Y.");
        Require(heightmap.GetHighestTaken(3, 5) == -65, "An empty unprimed column has no occupied block above its first available Y.");

        chunk.GetSection(chunk.GetSectionIndex(10)).SetBlockState(3, 10 & 15, 5, nonAirState);
        Heightmap.PrimeHeightmaps(chunk, new HashSet<Heightmap.Types> { Heightmap.Types.WORLD_SURFACE });
        Require(heightmap.GetFirstAvailable(3, 5) == 11, "Priming must find the highest non-air block in the column.");
        Require(heightmap.GetHighestTaken(3, 5) == 10, "Highest taken Y must be first available Y minus one.");
        Require(heightmap.GetFirstAvailable(4, 5) == -64, "Priming one column must not change adjacent column heights.");

        chunk.GetSection(chunk.GetSectionIndex(20)).SetBlockState(3, 20 & 15, 5, nonAirState);
        Require(heightmap.Update(3, 20, 5, nonAirState), "Adding a block above the current surface must update its height.");
        Require(heightmap.GetFirstAvailable(3, 5) == 21, "A new top block must set first available Y to block Y plus one.");

        chunk.GetSection(chunk.GetSectionIndex(12)).SetBlockState(3, 12, 5, nonAirState);
        Require(!heightmap.Update(3, 12, 5, nonAirState), "Adding a block sufficiently below the surface must not update the heightmap.");

        BlockState air = Blocks.AIR.DefaultState;
        chunk.GetSection(chunk.GetSectionIndex(20)).SetBlockState(3, 20 & 15, 5, air);
        Require(heightmap.Update(3, 20, 5, air), "Removing the surface block must trigger a downward rescan.");
        Require(heightmap.GetFirstAvailable(3, 5) == 13, "Removing the top block must reveal the previously buried block.");

        chunk.GetSection(chunk.GetSectionIndex(12)).SetBlockState(3, 12, 5, air);
        Require(heightmap.Update(3, 12, 5, air), "Removing the next surface block must trigger another downward rescan.");
        Require(heightmap.GetFirstAvailable(3, 5) == 11, "Downward rescan must find the original lower block.");

        chunk.GetSection(chunk.GetSectionIndex(10)).SetBlockState(3, 10 & 15, 5, air);
        Require(heightmap.Update(3, 10, 5, air), "Removing the final column block must reset its height to minimum build Y.");
        Require(heightmap.GetFirstAvailable(3, 5) == -64, "An empty column must return to minimum build Y.");

        chunk.GetSection(chunk.GetSectionIndex(0)).SetBlockState(4, 0, 2, nonAirState);
        Require(heightmap.Update(4, 0, 2, nonAirState), "A new block in another column must update independently.");
        Require(heightmap.GetFirstAvailable(4, 2) == 1, "The second column must store its own first available Y.");
        Require(heightmap.GetFirstAvailable(3, 5) == -64, "Updating a neighboring column must not alter the first column.");
    }

    private static void CheckMotionBlockingHeightmaps(BlockState fluidState)
    {
        LevelHeightAccessor height = LevelHeightAccessor.Create(-64, 384);
        PalettedContainerFactory factory = PalettedContainerFactory.CreateBuiltIn();
        var chunk = new TestChunkAccess(new ChunkPos(2, -3), height, factory);
        const int x = 7;
        const int y = 23;
        const int z = 9;

        ChunkSection section = chunk.GetSection(chunk.GetSectionIndex(y));
        section.SetBlockState(x, y & 15, z, fluidState);

        Heightmap.PrimeHeightmaps(chunk, new HashSet<Heightmap.Types>
        {
            Heightmap.Types.MOTION_BLOCKING,
            Heightmap.Types.MOTION_BLOCKING_NO_LEAVES,
            Heightmap.Types.OCEAN_FLOOR
        });

        Heightmap motionBlocking = chunk.GetOrCreateHeightmapUnprimed(Heightmap.Types.MOTION_BLOCKING);
        Heightmap motionBlockingNoLeaves = chunk.GetOrCreateHeightmapUnprimed(Heightmap.Types.MOTION_BLOCKING_NO_LEAVES);
        Heightmap oceanFloor = chunk.GetOrCreateHeightmapUnprimed(Heightmap.Types.OCEAN_FLOOR);

        Require(
            motionBlocking.GetFirstAvailable(x, z) == y + 1,
            "MOTION_BLOCKING must include a non-empty fluid state even when its block is not in the heightmap tag.");
        Require(
            motionBlockingNoLeaves.GetFirstAvailable(x, z) == y + 1,
            "MOTION_BLOCKING_NO_LEAVES must include a non-empty fluid state.");
        Require(
            oceanFloor.GetFirstAvailable(x, z) == height.GetMinY(),
            "OCEAN_FLOOR must use the block heightmap tag and must not count fluid by itself.");

        section.SetBlockState(x, y & 15, z, Blocks.AIR.DefaultState);
        Require(
            motionBlocking.Update(x, y, z, Blocks.AIR.DefaultState),
            "Removing the fluid-bearing surface block must update MOTION_BLOCKING.");
        Require(
            motionBlockingNoLeaves.Update(x, y, z, Blocks.AIR.DefaultState),
            "Removing the fluid-bearing surface block must update MOTION_BLOCKING_NO_LEAVES.");
        Require(
            motionBlocking.GetFirstAvailable(x, z) == height.GetMinY() &&
            motionBlockingNoLeaves.GetFirstAvailable(x, z) == height.GetMinY(),
            "Fluid-aware heightmaps must rescan down to minimum Y after the last qualifying state is removed.");
    }

    private static void CheckHeightmapOwnershipAndBatchPriming(BlockState nonAirState)
    {
        LevelHeightAccessor height = LevelHeightAccessor.Create(-64, 384);
        PalettedContainerFactory factory = PalettedContainerFactory.CreateBuiltIn();
        var position = new ChunkPos(-2, 3);
        var chunk = new TestChunkAccess(position, height, factory);

        Require(chunk.GetHeightmaps().Count == 0, "A new chunk must not eagerly allocate heightmaps.");
        Require(!chunk.HasPrimedHeightmap(Heightmap.Types.WORLD_SURFACE), "A missing heightmap must not be reported as present.");

        chunk.GetSection(chunk.GetSectionIndex(17)).SetBlockState(2, 1, 4, nonAirState);
        chunk.GetSection(chunk.GetSectionIndex(8)).SetBlockState(2, 8, 4, nonAirState);

        Heightmap unprimed = chunk.GetOrCreateHeightmapUnprimed(Heightmap.Types.WORLD_SURFACE);
        Require(chunk.HasPrimedHeightmap(Heightmap.Types.WORLD_SURFACE), "Heightmap presence must follow the reference map-presence semantics.");
        Require(ReferenceEquals(unprimed, chunk.GetOrCreateHeightmapUnprimed(Heightmap.Types.WORLD_SURFACE)), "Creating an existing heightmap type must preserve its instance.");
        Require(chunk.GetHeightmaps().Count == 1, "Chunk heightmap ownership must retain one entry per type.");

        Heightmap.PrimeHeightmaps(chunk, new HashSet<Heightmap.Types>
        {
            Heightmap.Types.WORLD_SURFACE_WG,
            Heightmap.Types.WORLD_SURFACE,
            Heightmap.Types.OCEAN_FLOOR_WG,
            Heightmap.Types.OCEAN_FLOOR,
            Heightmap.Types.MOTION_BLOCKING,
            Heightmap.Types.MOTION_BLOCKING_NO_LEAVES,
            Heightmap.Types.WORLD_SURFACE
        });

        Require(chunk.GetHeightmaps().Count == 6, "Batch priming must create each of the six requested types once.");
        Require(
            chunk.GetHeight(Heightmap.Types.WORLD_SURFACE_WG, position.GetMinBlockX() + 2, position.GetMinBlockZ() + 4) == 17,
            "World-surface worldgen height must select the highest non-air block using world coordinates.");
        Require(
            chunk.GetHeight(Heightmap.Types.WORLD_SURFACE, position.GetMinBlockX() + 2, position.GetMinBlockZ() + 4) == 17,
            "World-surface height variants must share the non-air predicate.");
        Require(
            Heightmap.Types.WORLD_SURFACE_WG.GetSerializationKey() == "WORLD_SURFACE_WG" &&
            !Heightmap.Types.WORLD_SURFACE_WG.SendToClient() &&
            !Heightmap.Types.WORLD_SURFACE_WG.KeepAfterWorldgen(),
            "Worldgen heightmap metadata must preserve the reference identifier and usage flags.");
        Require(
            Heightmap.Types.WORLD_SURFACE.GetSerializationKey() == "WORLD_SURFACE" &&
            Heightmap.Types.WORLD_SURFACE.SendToClient() &&
            Heightmap.Types.WORLD_SURFACE.KeepAfterWorldgen(),
            "Client heightmap metadata must preserve the reference identifier and usage flags.");
        Require(
            Heightmap.Types.OCEAN_FLOOR.GetSerializationKey() == "OCEAN_FLOOR" &&
            !Heightmap.Types.OCEAN_FLOOR.SendToClient() &&
            Heightmap.Types.OCEAN_FLOOR.KeepAfterWorldgen(),
            "Live-world heightmap metadata must preserve the reference identifier and usage flags.");

        Require(
            Heightmap.Types.MOTION_BLOCKING.GetSerializationKey() == "MOTION_BLOCKING" &&
            Heightmap.Types.MOTION_BLOCKING_NO_LEAVES.GetSerializedName() == "MOTION_BLOCKING_NO_LEAVES",
            "Motion-blocking heightmap types must preserve their serialization names.");
        Require(
            Heightmap.Types.MOTION_BLOCKING.SendToClient() &&
            Heightmap.Types.MOTION_BLOCKING_NO_LEAVES.SendToClient() &&
            Heightmap.Types.MOTION_BLOCKING.KeepAfterWorldgen() &&
            Heightmap.Types.MOTION_BLOCKING_NO_LEAVES.KeepAfterWorldgen(),
            "Both motion-blocking heightmaps use CLIENT retention and visibility.");

        var lazyChunk = new TestChunkAccess(position, height, factory);
        lazyChunk.GetSection(lazyChunk.GetSectionIndex(23)).SetBlockState(1, 7, 3, nonAirState);
        Require(
            lazyChunk.GetHeight(Heightmap.Types.WORLD_SURFACE, position.GetMinBlockX() + 1, position.GetMinBlockZ() + 3) == 23,
            "GetHeight must lazily create and prime a missing heightmap.");
        Require(lazyChunk.HasPrimedHeightmap(Heightmap.Types.WORLD_SURFACE), "Lazy height lookup must retain the created heightmap.");
    }

    private static void CheckHeightmapRawData(BlockState nonAirState)
    {
        LevelHeightAccessor height = LevelHeightAccessor.Create(-64, 384);
        PalettedContainerFactory factory = PalettedContainerFactory.CreateBuiltIn();
        var position = new ChunkPos(-4, 6);

        var loadedChunk = new TestChunkAccess(position, height, factory);
        var sourceStorage = new PackedBitStorage(9, 256);
        sourceStorage.Set(3 + 5 * 16, 75);
        ulong[] sourceData = sourceStorage.GetRawCopy();

        loadedChunk.SetHeightmap(Heightmap.Types.WORLD_SURFACE, sourceData);
        Heightmap loaded = loadedChunk.GetOrCreateHeightmapUnprimed(Heightmap.Types.WORLD_SURFACE);
        Require(
            loaded.GetFirstAvailable(3, 5) == 11,
            "Matching raw heightmap data must be copied into the owned map.");
        sourceStorage.Set(3 + 5 * 16, 1);
        Require(
            loaded.GetFirstAvailable(3, 5) == 11,
            "Loading heightmap data must copy words rather than retain the caller's array.");

        var fallbackChunk = new TestChunkAccess(position, height, factory);
        fallbackChunk.GetSection(fallbackChunk.GetSectionIndex(42))
            .SetBlockState(2, 42 & 15, 4, nonAirState);
        fallbackChunk.SetHeightmap(Heightmap.Types.WORLD_SURFACE, Array.Empty<ulong>());

        Require(
            fallbackChunk.GetHeight(Heightmap.Types.WORLD_SURFACE, position.GetMinBlockX() + 2, position.GetMinBlockZ() + 4) == 42,
            "Mismatched raw heightmap data must be ignored and recomputed from chunk contents.");
        Require(
            fallbackChunk.GetHeightmaps().Count == 1,
            "Raw-data fallback must reuse the heightmap created by SetHeightmap.");
    }

    private sealed class TestChunkAccess : ChunkAccess
    {
        public TestChunkAccess(
            ChunkPos chunkPos,
            LevelHeightAccessor levelHeightAccessor,
            PalettedContainerFactory containerFactory,
            ChunkSection?[]? sections = null,
            long inhabitedTime = 0)
            : base(chunkPos, levelHeightAccessor, containerFactory, inhabitedTime, sections)
        {
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
