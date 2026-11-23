using System.Text.Json;
using Asteria.Content.Biomes;
using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Content.Fluids;
using Asteria.Content.Registries;
using Asteria.Content.Storage;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.Core.Shapes;
using Asteria.World;
using Asteria.World.Lighting;
using Asteria.World.BlockScan;
using Asteria.Util;
using Asteria.World.Ticks;

internal static class ChunkChecks
{
    public static void Run(BlockState nonAirState, BlockState fluidState, FluidState sourceFluidState)
    {
        ArgumentNullException.ThrowIfNull(nonAirState);
        ArgumentNullException.ThrowIfNull(fluidState);
        ArgumentNullException.ThrowIfNull(sourceFluidState);

        CheckHeightAccessor();
        CheckHeightmapTypeCodec();
        CheckChunkPositionBounds();
        CheckChunkAccessLifecycle();
        CheckChunkBlockMutationContract(nonAirState);
        CheckPostProcessingListHelper();
        CheckBlockGetterContract(fluidState);
        CheckBlockGetterDefaultMethods(nonAirState);
        CheckBlockGetterTraversal();
        CheckBlockGetterSweptTraversal();
        CheckBlockGetterLineQueries();
        CheckBlockGetterClip(fluidState);
        CheckFluidStateGeometry(fluidState, sourceFluidState);
        CheckChunkBiomeAccess();
        CheckChunkSkyLightSources(nonAirState);
        CheckChunkBlockScanning(nonAirState);
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

        Require(chunk.CanBeSerialized(), "ChunkAccess.canBeSerialized must return true for a newly created chunk.");
        Require(!chunk.IsUnsaved(), "A new ChunkAccess must start saved.");
        Require(!chunk.TryMarkSaved(), "Trying to save a clean ChunkAccess must return false.");
        chunk.MarkUnsaved();
        Require(chunk.IsUnsaved(), "MarkUnsaved must set the dirty state.");
        Require(chunk.CanBeSerialized(), "ChunkAccess.canBeSerialized must remain true while a chunk is dirty.");
        Require(chunk.TryMarkSaved(), "Trying to save a dirty ChunkAccess must return true.");
        Require(!chunk.IsUnsaved() && !chunk.TryMarkSaved(), "TryMarkSaved must clear the dirty state after a successful transition.");

        Require(!chunk.IsLightCorrect(), "A new ChunkAccess must start with lighting marked incorrect.");
        chunk.SetLightCorrect(true);
        Require(chunk.IsLightCorrect(), "SetLightCorrect must update the lighting state.");
        Require(chunk.IsUnsaved(), "SetLightCorrect must mark the chunk unsaved.");
        chunk.TryMarkSaved();
        chunk.SetLightCorrect(false);
        Require(!chunk.IsLightCorrect() && chunk.IsUnsaved(), "Changing light correctness must mark the chunk unsaved.");

        var bottomChunk = new TestChunkAccess(
            new ChunkPos(-2, 3),
            height,
            PalettedContainerFactory.CreateBuiltIn());
        SectionPos bottomSection = SectionPos.BottomOf(bottomChunk);
        Require(
            bottomSection.Equals(SectionPos.Of(-2, -4, 3)),
            "SectionPos.BottomOf must combine the chunk's horizontal coordinates with its minimum section Y.");
    }

    private static void CheckChunkBlockMutationContract(BlockState nonAirState)
    {
        LevelHeightAccessor height = LevelHeightAccessor.Create(-64, 384);
        var chunk = new TestChunkAccess(
            new ChunkPos(-2, 3),
            height,
            PalettedContainerFactory.CreateBuiltIn());
        var position = new BlockPos(
            chunk.GetPos().GetMinBlockX() + 4,
            12,
            chunk.GetPos().GetMinBlockZ() + 7);

        BlockState firstPrevious = chunk.SetBlockState(position, nonAirState);
        Require(
            ReferenceEquals(firstPrevious, Blocks.AIR.DefaultState) &&
            ReferenceEquals(chunk.GetBlockState(position), nonAirState) &&
            chunk.LastSetBlockFlags == 3,
            "ChunkAccess.SetBlockState(position, state) must delegate to the abstract mutator with the default flags value 3.");

        BlockState secondPrevious = chunk.SetBlockState(position, Blocks.AIR.DefaultState, flags: 19);
        Require(
            ReferenceEquals(secondPrevious, nonAirState) &&
            ReferenceEquals(chunk.GetBlockState(position), Blocks.AIR.DefaultState) &&
            chunk.LastSetBlockFlags == 19,
            "The explicit-flags overload must pass the caller's flags unchanged to the concrete chunk.");

        BlockPos outside = new(position.GetX(), height.GetMinY() - 1, position.GetZ());
        BlockState outsidePrevious = chunk.SetBlockState(outside, nonAirState);
        Require(
            ReferenceEquals(outsidePrevious, Blocks.VOID_AIR.DefaultState) &&
            ReferenceEquals(chunk.GetBlockState(outside), Blocks.AIR.DefaultState) &&
            chunk.LastSetBlockFlags == 3,
            "The shared overload must preserve the concrete chunk's outside-build-height return behavior.");
    }

    private static void CheckPostProcessingListHelper()
    {
        List<short>?[] slots = new List<short>?[3];
        List<short> first = ChunkAccess.GetOrCreateOffsetList(slots, 1);
        first.Add(42);

        List<short> same = ChunkAccess.GetOrCreateOffsetList(slots, 1);
        Require(
            ReferenceEquals(first, same) &&
            ReferenceEquals(slots[1], first) &&
            first.SequenceEqual(new short[] { 42 }),
            "ChunkAccess.GetOrCreateOffsetList must lazily allocate once and return the existing section list on later calls.");

        List<short> other = ChunkAccess.GetOrCreateOffsetList(slots, 0);
        Require(
            !ReferenceEquals(first, other) &&
            ReferenceEquals(slots[0], other),
            "Each section index must own an independent post-processing list.");

        try
        {
            _ = ChunkAccess.GetOrCreateOffsetList(slots, slots.Length);
        }
        catch (IndexOutOfRangeException)
        {
            return;
        }

        throw new InvalidOperationException(
            "GetOrCreateOffsetList must retain the backing-array bounds failure for invalid section indices.");
    }

    private static void CheckBlockGetterContract(BlockState fluidState)
    {
        LevelHeightAccessor height = LevelHeightAccessor.Create(-64, 384);
        var chunk = new TestChunkAccess(
            new ChunkPos(0, 0),
            height,
            PalettedContainerFactory.CreateBuiltIn());
        BlockPos position = new(2, 7, 4);
        chunk.GetSection(chunk.GetSectionIndex(position.GetY()))
            .SetBlockState(2, 7, 4, fluidState);

        BlockGetter blockGetter = chunk;
        Require(
            ReferenceEquals(blockGetter.GetBlockState(position), fluidState),
            "ChunkAccess must expose block-state lookup through BlockGetter.");
        Require(
            ReferenceEquals(blockGetter.GetFluidState(position), fluidState.GetFluidState()),
            "ChunkAccess must expose the block state's fluid through BlockGetter.");

        BlockPos outsideBuildHeight = new(2, height.GetMinY() - 1, 4);
        Require(
            ReferenceEquals(blockGetter.GetBlockState(outsideBuildHeight), Blocks.AIR.DefaultState),
            "The concrete chunk fixture must return air outside its section array.");
        Require(
            blockGetter.GetFluidState(outsideBuildHeight).IsEmpty(),
            "Fluid lookup outside the section array must return the empty fluid state.");
    }

    private static void CheckBlockGetterDefaultMethods(BlockState lightEmittingState)
    {
        LevelHeightAccessor height = LevelHeightAccessor.Create(-64, 384);
        var chunk = new TestChunkAccess(
            new ChunkPos(0, 0),
            height,
            PalettedContainerFactory.CreateBuiltIn());
        BlockGetter getter = chunk;

        BlockPos stonePosition = new(2, 6, 4);
        BlockPos lightPosition = new(3, 6, 4);
        BlockPos airAboveStone = stonePosition.Above();
        chunk.GetSection(chunk.GetSectionIndex(stonePosition.GetY()))
            .SetBlockState(2, stonePosition.GetY() & 15, 4, Blocks.STONE.DefaultState);
        chunk.GetSection(chunk.GetSectionIndex(lightPosition.GetY()))
            .SetBlockState(3, lightPosition.GetY() & 15, 4, lightEmittingState);

        Require(
            getter.GetLightEmission(lightPosition) == lightEmittingState.GetLightEmission(),
            "BlockGetter light emission must delegate to the stored block state.");
        Require(
            getter.GetBlockFloorHeight(stonePosition) == 1.0,
            "Block floor height inside a full collision shape must use its maximum Y.");
        Require(
            getter.GetBlockFloorHeight(airAboveStone) == 0.0,
            "An empty block above a full block must inherit the block below's floor height minus one.");

        Require(
            getter.GetBlockFloorHeight(Shapes.Empty(), () => Shapes.Block()) == 0.0,
            "An empty shape must derive its floor height from a full block below.");
        Require(
            getter.GetBlockFloorHeight(Shapes.Box(0, 0, 0, 1, 0.5, 1), () => Shapes.Empty()) == 0.5,
            "A non-empty shape must return its own maximum Y without evaluating the lower shape.");
        Require(
            double.IsNegativeInfinity(getter.GetBlockFloorHeight(Shapes.Empty(), () => Shapes.Empty())),
            "An empty shape above a lower shape that does not reach the block top must have negative-infinite floor height.");

        IReadOnlyList<BlockState> states = getter.GetBlockStates(
            new AABB(2.0, 6.0, 4.0, 2.0, 7.0, 4.0)).ToArray();
        Require(
            states.Count == 2 &&
            ReferenceEquals(states[0], Blocks.STONE.DefaultState) &&
            ReferenceEquals(states[1], Blocks.AIR.DefaultState),
            "BlockGetter block-state box queries must include both floored Y bounds in vertical order.");
    }

    private static void CheckBlockGetterTraversal()
    {
        var from = new Vec3(0.5, 0.5, 0.5);
        var to = new Vec3(3.5, 0.5, 0.5);
        var visited = new List<BlockPos>();

        BlockPos? hit = BlockGetter.TraverseBlocks<object, BlockPos>(
            from,
            to,
            new object(),
            (_, position) =>
            {
                visited.Add(position.Immutable());
                return position.GetX() == 2 ? position.Immutable() : null;
            },
            _ => new BlockPos(-1, -1, -1));

        Require(
            hit is not null && hit.GetX() == 2 &&
            visited.Select(position => position.GetX()).SequenceEqual(new[] { 0, 1, 2 }),
            "Block traversal must visit each crossed voxel in order and stop at the first returned hit.");

        var reverseVisited = new List<int>();
        BlockGetter.TraverseBlocks<object, BlockPos>(
            to,
            from,
            new object(),
            (_, position) =>
            {
                reverseVisited.Add(position.GetX());
                return null;
            },
            _ => new BlockPos(-1, -1, -1));
        Require(
            reverseVisited.SequenceEqual(new[] { 3, 2, 1, 0 }),
            "Block traversal must handle negative movement through voxel boundaries.");

        int consumerCalls = 0;
        BlockPos samePointMiss = BlockGetter.TraverseBlocks<object, BlockPos>(
            from,
            from,
            new object(),
            (_, _) =>
            {
                consumerCalls++;
                return null;
            },
            _ => new BlockPos(9, 8, 7))!;

        Require(
            consumerCalls == 0 && samePointMiss.Equals(new BlockPos(9, 8, 7)),
            "A zero-length traversal must return the miss result without visiting a block.");

        Require(
            Mth.Sign(-0.25) == -1 && Mth.Sign(0.0) == 0 && Mth.Sign(0.25) == 1,
            "Mth.Sign must preserve the reference sign semantics.");
        Require(
            Mth.Frac(-0.25) == 0.75 && Mth.Frac(2.25) == 0.25,
            "Mth.Frac must return the fractional part using floor semantics for negative coordinates.");
    }

    private static void CheckBlockGetterSweptTraversal()
    {
        BlockPos[] directionalCorners = BlockPos.BetweenCornersInDirection(
            0, 0, 0, 1, 1, 1, Vec3.X_AXIS)
            .Select(position => position.Immutable())
            .ToArray();
        BlockPos[] expectedDirectionalOrder =
        {
            new(0, 0, 0),
            new(0, 0, 1),
            new(1, 0, 0),
            new(1, 0, 1),
            new(0, 1, 0),
            new(0, 1, 1),
            new(1, 1, 0),
            new(1, 1, 1)
        };
        Require(
            directionalCorners.SequenceEqual(expectedDirectionalOrder),
            "Directional corner enumeration must preserve the reference axis-step and nested-loop order.");

        var targetBounds = new AABB(2.0, 0.0, 0.0, 3.0, 1.0, 1.0);
        var traversed = new List<(BlockPos Position, int Step)>();
        bool completed = BlockGetter.ForEachBlockIntersectedBetween(
            new Vec3(0.0, 0.5, 0.5),
            new Vec3(2.0, 0.5, 0.5),
            targetBounds,
            (position, step) =>
            {
                traversed.Add((position.Immutable(), step));
                return true;
            });

        var expectedVolume = new HashSet<(int X, int Y, int Z)>();
        for (int x = 0; x <= 3; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                for (int z = 0; z <= 1; z++)
                    expectedVolume.Add((x, y, z));
            }
        }

        var actualVolume = traversed
            .Select(entry => (
                entry.Position.GetX(),
                entry.Position.GetY(),
                entry.Position.GetZ()))
            .ToHashSet();
        Require(
            completed &&
            actualVolume.SetEquals(expectedVolume) &&
            traversed.Count == expectedVolume.Count,
            "Swept-box traversal must visit every crossed block exactly once across the complete path.");
        Require(
            traversed.Where(entry => entry.Position.GetX() <= 1).All(entry => entry.Step == 0) &&
            traversed.Where(entry => entry.Position.GetX() == 2).All(entry => entry.Step > 0) &&
            traversed.Any(entry => entry.Position.GetX() == 3 && entry.Step > 0),
            "Swept-box traversal must distinguish the initial bounds, intermediate collision steps and final bounds.");

        var reverseTraversed = new HashSet<(int X, int Y, int Z)>();
        bool reverseCompleted = BlockGetter.ForEachBlockIntersectedBetween(
            new Vec3(2.0, 0.5, 0.5),
            new Vec3(0.0, 0.5, 0.5),
            new AABB(0.0, 0.0, 0.0, 1.0, 1.0, 1.0),
            (position, _) =>
            {
                reverseTraversed.Add((position.GetX(), position.GetY(), position.GetZ()));
                return true;
            });
        Require(
            reverseCompleted && reverseTraversed.SetEquals(expectedVolume),
            "Swept-box traversal must visit the same covered volume for negative movement.");

        var stationary = new List<BlockPos>();
        bool stationaryCompleted = BlockGetter.ForEachBlockIntersectedBetween(
            Vec3.ZERO,
            Vec3.ZERO,
            targetBounds,
            (position, step) =>
            {
                Require(step == 0, "Stationary swept-box traversal must report step zero.");
                stationary.Add(position.Immutable());
                return true;
            });
        Require(
            stationaryCompleted && stationary.Count == 8 &&
            stationary.Select(position => (position.GetX(), position.GetY(), position.GetZ())).ToHashSet()
                .SetEquals(expectedVolume.Where(position => position.X >= 2 && position.X <= 3)),
            "A stationary swept box must enumerate the target bounds once.");

        int calls = 0;
        bool stopped = BlockGetter.ForEachBlockIntersectedBetween(
            new Vec3(0.0, 0.5, 0.5),
            new Vec3(2.0, 0.5, 0.5),
            targetBounds,
            (_, _) => ++calls < 2);
        Require(
            !stopped && calls == 2,
            "Swept-box traversal must stop immediately when the visitor returns false.");
    }

    private static void CheckBlockGetterLineQueries()
    {
        LevelHeightAccessor height = LevelHeightAccessor.Create(-64, 384);
        var chunk = new TestChunkAccess(
            new ChunkPos(0, 0),
            height,
            PalettedContainerFactory.CreateBuiltIn());
        BlockPos targetPosition = new(2, 7, 4);
        chunk.GetSection(chunk.GetSectionIndex(targetPosition.GetY()))
            .SetBlockState(2, 7, 4, Blocks.STONE.DefaultState);

        var from = new Vec3(0.5, 7.5, 4.5);
        var to = new Vec3(4.5, 7.5, 4.5);
        BlockGetter getter = chunk;
        BlockHitResult hit = getter.IsBlockInLine(
            new ClipBlockStateContext(from, to, state => ReferenceEquals(state, Blocks.STONE.DefaultState)));

        Require(
            hit.GetHitType() == HitResult.Type.BLOCK &&
            hit.GetLocation().Equals(to) &&
            hit.GetDirection() == Direction.WEST &&
            hit.GetBlockPos().Equals(BlockPos.Containing(to.X, to.Y, to.Z)),
            "BlockGetter line queries must return the endpoint, opposite ray direction and endpoint block position for a matching state.");

        BlockHitResult miss = getter.IsBlockInLine(
            new ClipBlockStateContext(from, to, state => false));
        Require(
            miss.GetHitType() == HitResult.Type.MISS &&
            miss.GetLocation().Equals(to) &&
            miss.GetDirection() == Direction.WEST,
            "BlockGetter line queries must return a miss result when no state matches the predicate.");

        BlockHitResult? shapeHit = getter.ClipWithInteractionOverride(
            from,
            to,
            targetPosition,
            Blocks.STONE.DefaultState.GetCollisionShape(chunk, targetPosition),
            Blocks.STONE.DefaultState);
        Require(
            shapeHit is not null &&
            shapeHit.GetBlockPos().Equals(targetPosition),
            "ClipWithInteractionOverride must preserve the collision-shape hit when the interaction shape is empty.");
    }

    private static void CheckBlockGetterClip(BlockState fluidState)
    {
        LevelHeightAccessor height = LevelHeightAccessor.Create(-64, 384);
        var chunk = new TestChunkAccess(
            new ChunkPos(0, 0),
            height,
            PalettedContainerFactory.CreateBuiltIn());

        BlockPos fluidPosition = new(1, 7, 4);
        BlockPos stonePosition = new(3, 7, 4);
        var fluidBlock = new ClipFluidBlock(fluidState.GetFluidState());
        ChunkSection section = chunk.GetSection(chunk.GetSectionIndex(fluidPosition.GetY()));
        section.SetBlockState(1, 7, 4, fluidBlock.DefaultState);
        section.SetBlockState(3, 7, 4, Blocks.STONE.DefaultState);

        BlockGetter getter = chunk;
        var from = new Vec3(0.5, 7.05, 4.5);
        var to = new Vec3(4.5, 7.05, 4.5);

        BlockHitResult outlineHit = getter.Clip(new ClipContext(
            from, to, ClipContext.Block.Outline, ClipContext.Fluid.None));
        Require(
            outlineHit.GetHitType() == HitResult.Type.BLOCK &&
            outlineHit.GetBlockPos().Equals(fluidPosition),
            "Outline clipping must use a block's outline shape even when it has no collision.");

        BlockHitResult colliderHit = getter.Clip(new ClipContext(
            from, to, ClipContext.Block.Collider, ClipContext.Fluid.None));
        Require(
            colliderHit.GetHitType() == HitResult.Type.BLOCK &&
            colliderHit.GetBlockPos().Equals(stonePosition),
            "Collider clipping must skip a non-colliding block and hit the next collider.");

        BlockHitResult visualHit = getter.Clip(new ClipContext(
            from, to, ClipContext.Block.Visual, ClipContext.Fluid.None));
        Require(
            visualHit.GetHitType() == HitResult.Type.BLOCK &&
            visualHit.GetBlockPos().Equals(stonePosition),
            "The default visual shape must match the collision shape.");

        BlockHitResult anyFluidHit = getter.Clip(new ClipContext(
            from, to, ClipContext.Block.Collider, ClipContext.Fluid.Any));
        Require(
            anyFluidHit.GetHitType() == HitResult.Type.BLOCK &&
            anyFluidHit.GetBlockPos().Equals(fluidPosition) &&
            Math.Abs(anyFluidHit.GetLocation().X - 1.0) < 1.0E-6,
            "Any-fluid clipping must select the nearest fluid shape before a farther solid block.");

        BlockHitResult sourceOnlyHit = getter.Clip(new ClipContext(
            from, to, ClipContext.Block.Collider, ClipContext.Fluid.SourceOnly));
        Require(
            sourceOnlyHit.GetHitType() == HitResult.Type.BLOCK &&
            sourceOnlyHit.GetBlockPos().Equals(stonePosition),
            "Source-only clipping must ignore a non-source fluid state.");

        BlockHitResult waterHit = getter.Clip(new ClipContext(
            from, to, ClipContext.Block.Collider, ClipContext.Fluid.Water));
        Require(
            waterHit.GetHitType() == HitResult.Type.BLOCK &&
            waterHit.GetBlockPos().Equals(stonePosition),
            "Water clipping must ignore fluid types not bound to the water tag.");

        BlockHitResult miss = getter.Clip(new ClipContext(
            from, to, ClipContext.Block.Collider, ClipContext.Fluid.None));
        Require(
            miss.GetHitType() == HitResult.Type.BLOCK &&
            miss.GetBlockPos().Equals(stonePosition),
            "Disabling fluid clipping must leave the nearest block collision as the result.");
    }

    private static void CheckFluidStateGeometry(
        BlockState fluidBlockState,
        FluidState sourceFluidState)
    {
        LevelHeightAccessor height = LevelHeightAccessor.Create(-64, 384);
        var chunk = new TestChunkAccess(
            new ChunkPos(0, 0),
            height,
            PalettedContainerFactory.CreateBuiltIn());
        BlockGetter getter = chunk;

        BlockPos fluidPosition = new(1, 7, 4);
        BlockPos aboveFluid = fluidPosition.Above();
        ChunkSection section = chunk.GetSection(chunk.GetSectionIndex(aboveFluid.GetY()));
        section.SetBlockState(1, aboveFluid.GetY() & 15, 4, Blocks.STONE.DefaultState);

        Require(
            Blocks.STONE.DefaultState.IsCollisionShapeFullBlock(getter, aboveFluid),
            "A full static collision shape must be cached as a full block.");
        Require(
            !Blocks.AIR.DefaultState.IsCollisionShapeFullBlock(getter, new BlockPos(0, 8, 0)),
            "An empty static collision shape must be cached as not full.");
        Require(
            Blocks.STONE.DefaultState.IsFaceSturdy(getter, aboveFluid, Direction.DOWN),
            "A full stone face must provide full support.");
        Require(
            Blocks.STONE.DefaultState.IsFaceSturdy(getter, aboveFluid, Direction.DOWN, SupportType.Center) &&
            Blocks.STONE.DefaultState.IsFaceSturdy(getter, aboveFluid, Direction.DOWN, SupportType.Rigid),
            "A full stone face must provide center and rigid support.");

        BlockPos airPosition = new(2, 8, 4);
        Require(
            !Blocks.AIR.DefaultState.IsFaceSturdy(getter, airPosition, Direction.DOWN) &&
            !Blocks.AIR.DefaultState.IsFaceSturdy(getter, airPosition, Direction.DOWN, SupportType.Center) &&
            !Blocks.AIR.DefaultState.IsFaceSturdy(getter, airPosition, Direction.DOWN, SupportType.Rigid),
            "Air must not provide full, center or rigid face support.");

        var dynamicSupportBlock = new DynamicSupportBlock();
        Require(
            dynamicSupportBlock.DefaultState.IsFaceSturdy(
                getter,
                new BlockPos(4, 8, 4),
                Direction.DOWN),
            "Dynamic block support must evaluate a full shape at the queried position.");
        Require(
            !dynamicSupportBlock.DefaultState.IsFaceSturdy(
                getter,
                new BlockPos(5, 8, 4),
                Direction.DOWN),
            "Dynamic block support must not reuse a cached face result at another position.");
        Require(
            dynamicSupportBlock.DefaultState.IsCollisionShapeFullBlock(getter, new BlockPos(4, 8, 4)) &&
            !dynamicSupportBlock.DefaultState.IsCollisionShapeFullBlock(getter, new BlockPos(5, 8, 4)),
            "Dynamic full-collision queries must evaluate the shape at each requested position.");
        Require(
            sourceFluidState.GetHeightForCamera(getter, fluidPosition) == 1.0f,
            "A source fluid directly below a sturdy upper face must use full camera height.");
        Require(
            Math.Abs(fluidBlockState.GetFluidState().GetHeightForCamera(getter, fluidPosition) - 1.0f / 9.0f) < 1.0E-6,
            "A non-source fluid must retain its contextual camera height beneath a sturdy face.");

        section.SetBlockState(1, aboveFluid.GetY() & 15, 4, Blocks.AIR.DefaultState);
        Require(
            Math.Abs(sourceFluidState.GetHeightForCamera(getter, fluidPosition) - 8.0f / 9.0f) < 1.0E-6,
            "A source fluid without a sturdy upper face must use its contextual height.");

        BlockPos surfaceAbove = new(10, 8, 10);
        ChunkSection surfaceSection = chunk.GetSection(chunk.GetSectionIndex(surfaceAbove.GetY()));
        for (int offsetX = -1; offsetX <= 1; offsetX++)
        {
            for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
            {
                BlockPos neighbor = surfaceAbove.Offset(offsetX, 0, offsetZ);
                surfaceSection.SetBlockState(
                    neighbor.GetX() & 15,
                    neighbor.GetY() & 15,
                    neighbor.GetZ() & 15,
                    fluidBlockState);
            }
        }

        FluidState fluid = fluidBlockState.GetFluidState();
        Require(
            !fluid.ShouldRenderBackwardUpFace(getter, surfaceAbove),
            "A surface surrounded by the same fluid must not render its backward upper face.");

        BlockPos opening = surfaceAbove.Offset(0, 0, 0);
        surfaceSection.SetBlockState(
            opening.GetX() & 15,
            opening.GetY() & 15,
            opening.GetZ() & 15,
            Blocks.AIR.DefaultState);
        Require(
            fluid.ShouldRenderBackwardUpFace(getter, surfaceAbove),
            "A surface with a non-solid opening to a different fluid state must render its backward upper face.");
    }

    private static void CheckChunkBiomeAccess()
    {
        var position = new ChunkPos(-2, 3);
        LevelHeightAccessor height = LevelHeightAccessor.Create(-64, 384);
        var chunk = new TestChunkAccess(position, height, PalettedContainerFactory.CreateBuiltIn());
        var resolver = new AlternatingBiomeResolver(
            Biomes.PLAINS.BuiltInRegistryHolder!,
            Biomes.DESERT.BuiltInRegistryHolder!);

        chunk.FillBiomesFromNoise(resolver);

        Require(
            resolver.CallCount == height.GetSectionsCount() * ChunkSection.BiomeCount,
            "Chunk biome filling must resolve all 64 quart cells in every vertical section.");
        Require(
            ReferenceEquals(chunk.GetHeightAccessorForGeneration(), chunk),
            "Chunk generation must use the chunk itself as its height accessor.");

        int quartMinX = QuartPos.FromBlock(position.GetMinBlockX());
        int quartMinZ = QuartPos.FromBlock(position.GetMinBlockZ());
        int quartY = QuartPos.FromSection(height.GetMinSectionY()) + 2;
        Holder<Biome> expected = resolver.GetNoiseBiome(quartMinX + 1, quartY, quartMinZ + 3);

        Require(
            ReferenceEquals(
                chunk.GetNoiseBiome(quartMinX + 1, quartY, quartMinZ + 3),
                expected),
            "Chunk biome lookup must map world quart coordinates into the correct section-local cell.");

        int quartMinY = QuartPos.FromBlock(height.GetMinY());
        int quartMaxY = unchecked(quartMinY + QuartPos.FromBlock(height.GetHeight()) - 1);
        Require(
            ReferenceEquals(
                chunk.GetNoiseBiome(quartMinX, int.MinValue, quartMinZ),
                chunk.GetNoiseBiome(quartMinX, quartMinY, quartMinZ)),
            "Chunk biome lookup must clamp quart Y below the build height.");
        Require(
            ReferenceEquals(
                chunk.GetNoiseBiome(quartMinX, int.MaxValue, quartMinZ),
                chunk.GetNoiseBiome(quartMinX, quartMaxY, quartMinZ)),
            "Chunk biome lookup must clamp quart Y above the build height.");

        Require(QuartPos.FromBlock(-1) == -1, "Block-to-quart conversion must floor negative coordinates.");
        Require(QuartPos.QuartLocal(-1) == 3, "Local quart coordinates must use the low two bits.");
        Require(QuartPos.ToBlock(-2) == -8, "Quart-to-block conversion must preserve negative coordinates.");
        Require(QuartPos.FromSection(-4) == -16, "Section-to-quart conversion must multiply by four.");
        Require(QuartPos.ToSection(-16) == -4, "Quart-to-section conversion must floor negative coordinates.");
    }

    private static void CheckChunkSkyLightSources(BlockState opaqueState)
    {
        LevelHeightAccessor height = LevelHeightAccessor.Create(-64, 384);
        var chunk = new TestChunkAccess(
            new ChunkPos(0, 0),
            height,
            PalettedContainerFactory.CreateBuiltIn());
        ChunkSkyLightSources sources = chunk.GetSkyLightSources();

        Require(
            sources.GetLowestSourceY(4, 7) == ChunkSkyLightSources.NegativeInfinity,
            "An empty chunk column must begin without a sky-light source boundary.");
        chunk.InitializeLightSources();
        Require(
            sources.GetHighestLowestSourceY() == ChunkSkyLightSources.NegativeInfinity,
            "An empty chunk must initialize every sky-light source column to negative infinity.");

        chunk.GetSection(chunk.GetSectionIndex(10)).SetBlockState(4, 10 & 15, 7, opaqueState);
        chunk.InitializeLightSources();
        Require(
            sources.GetLowestSourceY(4, 7) == 11,
            "Initial sky-light source calculation must locate the edge immediately above the highest opaque block.");
        Require(
            sources.GetLowestSourceY(5, 7) == ChunkSkyLightSources.NegativeInfinity,
            "Sky-light source boundaries must remain independent between columns.");
        Require(
            sources.GetHighestLowestSourceY() == 11,
            "The highest column sky-light boundary must track the maximum initialized source Y.");

        chunk.GetSection(chunk.GetSectionIndex(20)).SetBlockState(4, 20 & 15, 7, opaqueState);
        Require(
            !sources.Update(chunk, 4, 8, 7),
            "A block update below the current source boundary must not rescan the column.");
        Require(
            sources.Update(chunk, 4, 20, 7) && sources.GetLowestSourceY(4, 7) == 21,
            "Adding a new top opaque block must move the sky-light source boundary upward.");

        chunk.GetSection(chunk.GetSectionIndex(20)).SetBlockState(4, 20 & 15, 7, Blocks.AIR.DefaultState);
        Require(
            sources.Update(chunk, 4, 20, 7) && sources.GetLowestSourceY(4, 7) == 11,
            "Removing the top opaque block must rescan downward to the previous boundary.");

        chunk.GetSection(chunk.GetSectionIndex(10)).SetBlockState(4, 10 & 15, 7, Blocks.AIR.DefaultState);
        Require(
            sources.Update(chunk, 4, 10, 7) &&
            sources.GetLowestSourceY(4, 7) == ChunkSkyLightSources.NegativeInfinity,
            "Removing the final opaque block must clear the source boundary to negative infinity.");
        Require(
            sources.GetHighestLowestSourceY() == ChunkSkyLightSources.NegativeInfinity,
            "The chunk-wide highest source boundary must return to negative infinity after the final block is removed.");
    }

    private static void CheckChunkBlockScanning(BlockState lightEmittingState)
    {
        LevelHeightAccessor height = LevelHeightAccessor.Create(-64, 384);
        var position = new ChunkPos(-2, 3);
        var chunk = new TestChunkAccess(
            position,
            height,
            PalettedContainerFactory.CreateBuiltIn());

        BlockPos lowerPosition = position.GetBlockAt(2, -23, 3);
        BlockPos upperPosition = position.GetBlockAt(5, 31, 12);
        chunk.GetSection(chunk.GetSectionIndex(lowerPosition.GetY()))
            .SetBlockState(2, lowerPosition.GetY() & 15, 3, lightEmittingState);
        chunk.GetSection(chunk.GetSectionIndex(upperPosition.GetY()))
            .SetBlockState(5, upperPosition.GetY() & 15, 12, lightEmittingState);

        var found = new List<(BlockPos Position, BlockState State)>();
        chunk.FindBlocks(
            state => ReferenceEquals(state, lightEmittingState),
            (blockPosition, state) => found.Add((blockPosition.Immutable(), state)));

        Require(
            found.Count == 2 &&
            found[0].Position.Equals(lowerPosition) &&
            found[1].Position.Equals(upperPosition),
            "Chunk block scanning must traverse sections from minimum Y upward and report world coordinates.");
        Require(
            found.All(entry => ReferenceEquals(entry.State, lightEmittingState)),
            "Chunk block scanning must pass the exact matched block-state instances to the consumer.");

        var lightSources = new List<BlockPos>();
        chunk.FindBlockLightSources(
            (blockPosition, _) => lightSources.Add(blockPosition.Immutable()));
        Require(
            lightSources.Count == 2 &&
            lightSources[0].Equals(lowerPosition) &&
            lightSources[1].Equals(upperPosition),
            "Block-light source scanning must find states whose cached light emission is non-zero.");

        ChunkSection lowerSection = chunk.GetSection(chunk.GetSectionIndex(lowerPosition.GetY()));
        BlockPos lowerOrigin = SectionPos.Of(
            position,
            chunk.GetSectionYFromSectionIndex(chunk.GetSectionIndex(lowerPosition.GetY()))).Origin();
        int callbacks = 0;
        bool aborted = BlockScanUtils.FindBlocksInSection(
            lowerSection,
            lowerOrigin,
            0, 0, 0,
            15, 15, 15,
            state => ReferenceEquals(state, lightEmittingState),
            (_, _) =>
            {
                callbacks++;
                return Continuation.Abort;
            });
        Require(
            aborted && callbacks == 1,
            "Section block scanning must stop immediately when its consumer returns ABORT.");

        var alternatingResolver = new AlternatingBiomeResolver(
            Biomes.PLAINS.BuiltInRegistryHolder!,
            Biomes.DESERT.BuiltInRegistryHolder!);
        chunk.FillBiomesFromNoise(alternatingResolver);
        var biomes = new HashSet<Holder<Biome>>();
        chunk.CollectBiomesInPalette(biomes);
        Require(
            biomes.Count == 2 &&
            biomes.Contains(Biomes.PLAINS.BuiltInRegistryHolder!) &&
            biomes.Contains(Biomes.DESERT.BuiltInRegistryHolder!),
            "Chunk biome palette collection must aggregate distinct holders across every section.");
    }

    private static void CheckChunkPositionBounds()
    {
        int maximum = ChunkPyramid.MaxChunkCoordinateValue;
        var validBoundary = new ChunkPos(maximum, -maximum);
        var invalidBoundary = new ChunkPos(maximum + 1, 0);

        Require(
            maximum == 2_097_061 &&
            ChunkPos.IsValid(maximum, -maximum) &&
            validBoundary.IsValid(),
            "ChunkPos.IsValid must accept both inclusive axes at the ChunkPyramid safety boundary.");

        Require(
            !ChunkPos.IsValid(maximum + 1, 0) &&
            !ChunkPos.IsValid(0, -(maximum + 1)) &&
            !invalidBoundary.IsValid(),
            "ChunkPos.IsValid must reject coordinates immediately outside the ChunkPyramid safety boundary.");

        Require(
            ChunkPos.IsValid(int.MinValue, 0),
            "ChunkPos.IsValid must preserve Java Mth.absMax integer-overflow behavior for int.MinValue.");
        
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        ChunkPos source = new(-117, 284);
        JsonElement encoded = ChunkPos.CODEC.EncodeStart(ops, source).GetOrThrow();
        IReadOnlyList<JsonElement> coordinates = ops.GetList(encoded).GetOrThrow();
        DataResult<ChunkPos> decoded = ChunkPos.CODEC.Parse(ops, encoded);

        Require(
            coordinates.Count == 2 &&
            coordinates[0].GetInt32() == source.X &&
            coordinates[1].GetInt32() == source.Z &&
            decoded.GetOrThrow() == source &&
            ReferenceEquals(decoded.Lifecycle, Lifecycle.Stable()),
            "ChunkPos.CODEC must round-trip X/Z in order and expose stable lifecycle metadata.");

        Require(
            ChunkPos.CODEC.Parse(ops, ops.CreateList(new[] { ops.CreateInt(1) })).IsError &&
            ChunkPos.CODEC.Parse(ops, ops.CreateList(new[] { ops.CreateInt(1), ops.CreateInt(2), ops.CreateInt(3) })).IsError,
            "ChunkPos.CODEC must reject lists whose coordinate count is not exactly two.");
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

    private static void CheckHeightmapTypeCodec()
    {
        Heightmap.Types[] types =
        {
            Heightmap.Types.WORLD_SURFACE_WG,
            Heightmap.Types.WORLD_SURFACE,
            Heightmap.Types.OCEAN_FLOOR_WG,
            Heightmap.Types.OCEAN_FLOOR,
            Heightmap.Types.MOTION_BLOCKING,
            Heightmap.Types.MOTION_BLOCKING_NO_LEAVES
        };

        DynamicOps<JsonElement> normalOps = JsonOps.INSTANCE;
        DynamicOps<JsonElement> compressedOps = JsonOps.COMPRESSED;

        for (int index = 0; index < types.Length; index++)
        {
            Heightmap.Types type = types[index];
            JsonElement named = Heightmap.Types.CODEC.EncodeStart(normalOps, type).GetOrThrow();
            JsonElement compressed = Heightmap.Types.CODEC.EncodeStart(compressedOps, type).GetOrThrow();

            Require(
                named.GetString() == type.GetSerializedName() &&
                ReferenceEquals(Heightmap.Types.CODEC.Parse(normalOps, named).GetOrThrow(), type),
                "Heightmap.Types.CODEC must round-trip every serialized name in ordinary DynamicOps.");

            Require(
                compressed.GetInt32() == index &&
                ReferenceEquals(Heightmap.Types.CODEC.Parse(compressedOps, compressed).GetOrThrow(), type),
                "Heightmap.Types.CODEC must use enum ordinal IDs when DynamicOps requests compressed maps.");
        }

        DataResult<Heightmap.Types> unknownName =
            Heightmap.Types.CODEC.Parse(normalOps, normalOps.CreateString("missing"));
        DataResult<Heightmap.Types> unknownId =
            Heightmap.Types.CODEC.Parse(compressedOps, compressedOps.CreateInt(6));

        Require(
            unknownName.IsError && unknownName.ErrorMessage == "Unknown element name: missing" &&
            unknownId.IsError && unknownId.ErrorMessage == "Unknown element id: 6",
            "Heightmap.Types.CODEC must preserve the reference unknown-name and unknown-ID errors.");
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
            Heightmap.Types.MOTION_BLOCKING_NO_LEAVES
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

    private sealed class ClipFluidBlock : Block
    {
        private readonly FluidState _fluidState;

        public ClipFluidBlock(FluidState fluidState)
            : base(BlockProperties.Of().NoCollision(), createIntrusiveRegistryHolder: false)
        {
            _fluidState = fluidState ?? throw new ArgumentNullException(nameof(fluidState));
            InitializeStateCaches();
        }

        public override FluidState GetFluidState(BlockState state) => _fluidState;
    }

    private sealed class DynamicSupportBlock : Block
    {
        public DynamicSupportBlock()
            : base(BlockProperties.Of().DynamicShape(), createIntrusiveRegistryHolder: false)
        {
            InitializeStateCaches();
        }

        public override VoxelShape GetCollisionShape(
            BlockState state,
            BlockGetter level,
            BlockPos position) =>
            position.GetX() == 4 ? Shapes.Block() : Shapes.Empty();
    }

    private sealed class TestChunkAccess : ChunkAccess
    {
        private readonly ProtoChunkTicks<Block> _blockTicks = new();
        private readonly ProtoChunkTicks<Fluid> _fluidTicks = new();

        public override TickContainerAccess<Block> GetBlockTicks() => _blockTicks;

        public override TickContainerAccess<Fluid> GetFluidTicks() => _fluidTicks;

        public override PackedTicks GetTicksForSerialization(long currentTick) =>
            new(_blockTicks.Pack(currentTick), _fluidTicks.Pack(currentTick));

        public override ChunkStatus GetPersistedStatus() => ChunkStatus.EMPTY;

        public int LastSetBlockFlags { get; private set; }

        public override BlockState SetBlockState(BlockPos position, BlockState state, int flags)
        {
            ArgumentNullException.ThrowIfNull(position);
            ArgumentNullException.ThrowIfNull(state);
            LastSetBlockFlags = flags;

            int y = position.GetY();
            if (IsOutsideBuildHeight(y))
                return Blocks.VOID_AIR.DefaultState;

            return GetSection(GetSectionIndex(y)).SetBlockState(
                position.GetX() & SectionPos.SectionMask,
                y & SectionPos.SectionMask,
                position.GetZ() & SectionPos.SectionMask,
                state);
        }

        public TestChunkAccess(
            ChunkPos chunkPos,
            LevelHeightAccessor levelHeightAccessor,
            PalettedContainerFactory containerFactory,
            ChunkSection?[]? sections = null,
            long inhabitedTime = 0)
            : base(chunkPos, levelHeightAccessor, containerFactory, inhabitedTime, sections)
        {
        }

        public override BlockState GetBlockState(BlockPos position)
        {
            ArgumentNullException.ThrowIfNull(position);

            int x = position.GetX();
            int y = position.GetY();
            int z = position.GetZ();
            int sectionIndex = GetSectionIndex(y);

            if (sectionIndex >= 0 && sectionIndex < GetSections().Length)
            {
                ChunkSection section = GetSection(sectionIndex);
                if (!section.HasOnlyAir)
                {
                    return section.GetBlockState(
                        x & SectionPos.SectionMask,
                        y & SectionPos.SectionMask,
                        z & SectionPos.SectionMask);
                }
            }

            return Blocks.AIR.DefaultState;
        }

        public override FluidState GetFluidState(BlockPos position) =>
            GetBlockState(position).GetFluidState();
    }

    private sealed class AlternatingBiomeResolver : BiomeResolver
    {
        private readonly Holder<Biome> _plains;
        private readonly Holder<Biome> _desert;

        public int CallCount { get; private set; }

        public AlternatingBiomeResolver(Holder<Biome> plains, Holder<Biome> desert)
        {
            _plains = plains;
            _desert = desert;
        }

        public Holder<Biome> GetNoiseBiome(int quartX, int quartY, int quartZ)
        {
            CallCount++;
            return ((quartX + quartY + quartZ) & 1) == 0 ? _plains : _desert;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
