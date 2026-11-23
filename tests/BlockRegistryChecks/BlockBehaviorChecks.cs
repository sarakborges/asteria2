using Asteria.Content.Blocks;
using BuiltInFluids = Asteria.Content.Fluids.Fluids;
using Asteria.Content.Fluids;
using Asteria.Core;
using Asteria.Content.Materials;
using Asteria.Core.Shapes;
using Asteria.World;
using Asteria.World.Pathfinding;

internal static class BlockBehaviorChecks
{
    private static List<string>? _constructorOrderEvents;

    private sealed class OverrideBlock : Block
    {
        public OverrideBlock()
            : base(BlockProperties.Of(), createIntrusiveRegistryHolder: false)
        {
            InitializeStateCaches();
        }

        public override bool IsPathfindable(BlockState state, PathComputationType type) =>
            type == PathComputationType.WATER;

        public override long GetSeed(BlockState state, BlockPos position) => 987654321L;

        public override bool SkipRendering(
            BlockState state,
            BlockState neighborState,
            Direction direction) =>
            ReferenceEquals(neighborState.Block, this) &&
            ReferenceEquals(direction, Direction.UP);
    }

    public static void Run()
    {
        CheckPathComputationTypes();
        CheckDefaultPathfindability();
        CheckPathfindabilityOverride();
        CheckSkipRendering();
        CheckFluidReplacement();
        CheckBlockSeed();
        CheckOffsetCacheValidation();
        CheckDefaultViewBlockingTracksSuffocationProperty();
        CheckDefaultDestroyTime();
        CheckFaceSupportSemantics();
        CheckStateCacheReinitialization();
        CheckDefaultFluidStateBeforeCacheInitialization();
        CheckStateConstructorEvaluationOrder();
    }


    private static void CheckDefaultFluidStateBeforeCacheInitialization()
    {
        var block = new InitialFluidStateBlock();

        Require(
            ReferenceEquals(block.DefaultState.GetFluidState(), BuiltInFluids.EMPTY.DefaultFluidState),
            "A newly constructed BlockState must expose the empty fluid state before initCache replaces it with the owning block's fluid state.");
    }

    private sealed class InitialFluidStateBlock : Block
    {
        public InitialFluidStateBlock()
            : base(
                BlockProperties.Of().LightLevel(static state =>
                {
                    if (!ReferenceEquals(state.GetFluidState(), BuiltInFluids.EMPTY.DefaultFluidState))
                    {
                        throw new InvalidOperationException(
                            "BlockState must expose BuiltInFluids.EMPTY while the state constructor evaluates its light-emission property.");
                    }

                    return 0;
                }),
                createIntrusiveRegistryHolder: false)
        {
            InitializeStateCaches();
        }
    }


    private static void CheckStateConstructorEvaluationOrder()
    {
        var events = new List<string>();
        var properties = BlockProperties.Of()
            .Air()
            .LightLevel(state =>
            {
                Require(!state.IsAir && state.DestroySpeed == 0.0f,
                    "Minecraft evaluates light emission before assigning the remaining BlockStateBase property snapshots.");
                events.Add("light");
                return 0;
            })
            .MapColor(state =>
            {
                Require(state.IsAir && state.DestroySpeed == 0.0f,
                    "Minecraft assigns isAir before evaluating map color, but assigns destroySpeed afterward.");
                events.Add("map-color");
                return MapColor.NONE;
            });

        _constructorOrderEvents = events;
        try
        {
            _ = new ConstructorOrderBlock(properties);
        }
        finally
        {
            _constructorOrderEvents = null;
        }

        Require(events.SequenceEqual(new[] { "light", "shape", "map-color" }),
            "BlockStateBase constructor callbacks must execute in Minecraft 26.3 order.");
    }

    private sealed class ConstructorOrderBlock : Block
    {
        public ConstructorOrderBlock(BlockProperties properties)
            : base(properties, createIntrusiveRegistryHolder: false)
        {
            InitializeStateCaches();
        }

        public override bool UseShapeForLightOcclusion(BlockState state)
        {
            _constructorOrderEvents?.Add("shape");
            return false;
        }
    }

    private static void CheckPathComputationTypes()
    {
        PathComputationType[] values = Enum.GetValues<PathComputationType>();
        Require(
            values.Length == 3 &&
            values[0] == PathComputationType.LAND &&
            values[1] == PathComputationType.WATER &&
            values[2] == PathComputationType.AIR,
            "PathComputationType must preserve the Minecraft 26.3 LAND/WATER/AIR enum and ordinal order.");
    }

    private static void CheckDefaultPathfindability()
    {
        BlockState air = Blocks.AIR.DefaultState;
        BlockState stone = Blocks.STONE.DefaultState;

        Require(
            air.IsPathfindable(PathComputationType.LAND) &&
            air.IsPathfindable(PathComputationType.AIR),
            "The default pathfindability rules must allow LAND and AIR through a non-full collision shape.");

        Require(
            !stone.IsPathfindable(PathComputationType.LAND) &&
            !stone.IsPathfindable(PathComputationType.AIR),
            "The default pathfindability rules must reject LAND and AIR through a full collision shape.");

        Require(
            !air.IsPathfindable(PathComputationType.WATER) &&
            !stone.IsPathfindable(PathComputationType.WATER),
            "The WATER pathfindability rule must depend on the fluid state's water-tag membership, not block replaceability.");

        Require(
            !air.IsPathfindable((PathComputationType)int.MaxValue),
            "Unknown path computation values must use the default false branch.");
    }

    private static void CheckPathfindabilityOverride()
    {
        var block = new OverrideBlock();
        BlockState state = block.DefaultState;

        Require(
            !state.IsPathfindable(PathComputationType.LAND) &&
            state.IsPathfindable(PathComputationType.WATER) &&
            !state.IsPathfindable(PathComputationType.AIR),
            "BlockState.IsPathfindable must delegate to the owning block override.");
    }

    private static void CheckSkipRendering()
    {
        BlockState stone = Blocks.STONE.DefaultState;
        Require(
            !stone.SkipRendering(stone, Direction.UP) &&
            !stone.SkipRendering(stone, Direction.DOWN),
            "The default Block.skipRendering implementation must return false.");

        var block = new OverrideBlock();
        BlockState state = block.DefaultState;

        Require(
            state.SkipRendering(state, Direction.UP),
            "BlockState.SkipRendering must delegate the state and direction to its owning block.");
        Require(
            !state.SkipRendering(Blocks.STONE.DefaultState, Direction.UP) &&
            !state.SkipRendering(state, Direction.DOWN),
            "Skip-rendering overrides must receive the neighbor state and exact face direction.");
    }

    private static void CheckFluidReplacement()
    {
        BlockState air = Blocks.AIR.DefaultState;
        BlockState stone = Blocks.STONE.DefaultState;
        var noCollisionBlock = new CollisionTestBlock();
        BlockState nonSolid = noCollisionBlock.DefaultState;

        Require(
            air.CanBeReplaced(BuiltInFluids.EMPTY),
            "A block with the replaceable property must remain fluid-replaceable.");
        Require(
            !stone.CanBeReplaced(BuiltInFluids.EMPTY),
            "A solid, non-replaceable block must reject fluid replacement.");
        Require(
            !nonSolid.IsReplaceable && !nonSolid.IsSolid &&
            nonSolid.CanBeReplaced(BuiltInFluids.EMPTY),
            "Fluid replacement must allow a non-solid state even when its replaceable property is false.");
    }


    private static void CheckStateCacheReinitialization()
    {
        var block = new ReinitializableShapeBlock();
        BlockState state = block.DefaultState;

        Require(
            state.IsCollisionShapeFullBlock(EmptyBlockGetter.Instance, BlockPos.Zero) &&
            state.IsSolidRender &&
            state.IsSolid &&
            state.IsFaceSturdy(EmptyBlockGetter.Instance, BlockPos.Zero, Direction.UP),
            "The initial static block shape must populate all derived collision, solid-render and face-support caches.");

        block.Shape = Shapes.Empty();
        block.ReinitializeStateCaches();

        Require(
            state.GetCollisionShape(EmptyBlockGetter.Instance, BlockPos.Zero).IsEmpty() &&
            !state.IsCollisionShapeFullBlock(EmptyBlockGetter.Instance, BlockPos.Zero) &&
            !state.IsSolidRender &&
            !state.IsSolid &&
            !state.IsFaceSturdy(EmptyBlockGetter.Instance, BlockPos.Zero, Direction.UP) &&
            state.OcclusionShape.IsEmpty() &&
            state.PropagatesSkylightDown,
            "BlockState.initCache must recompute its cached geometry and derived flags when initialized again.");
    }

    private sealed class ReinitializableShapeBlock : Block
    {
        public VoxelShape Shape { get; set; } = Shapes.Block();

        public ReinitializableShapeBlock()
            : base(BlockProperties.Of(), createIntrusiveRegistryHolder: false)
        {
            InitializeStateCaches();
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos position) =>
            Shape;

        public void ReinitializeStateCaches() =>
            InitializeStateCaches();
    }

    private static void CheckFaceSupportSemantics()
    {
        SupportType[] supportTypes = Enum.GetValues<SupportType>();
        Require(
            supportTypes.Length == 3 &&
            supportTypes[0] == SupportType.Full &&
            supportTypes[1] == SupportType.Center &&
            supportTypes[2] == SupportType.Rigid,
            "SupportType must preserve the Minecraft FULL/CENTER/RIGID enum order.");

        BlockState air = Blocks.AIR.DefaultState;
        BlockState stone = Blocks.STONE.DefaultState;
        BlockState dynamic = new DynamicSupportBlock().DefaultState;

        foreach (Direction direction in Direction.GetValues())
        {
            foreach (SupportType supportType in supportTypes)
            {
                Require(
                    stone.IsFaceSturdy(EmptyBlockGetter.Instance, BlockPos.Zero, direction, supportType),
                    $"A full cube must support every face for {supportType}.");
                Require(
                    !air.IsFaceSturdy(EmptyBlockGetter.Instance, BlockPos.Zero, direction, supportType),
                    $"An empty shape must not support any face for {supportType}.");
                Require(
                    dynamic.IsFaceSturdy(EmptyBlockGetter.Instance, BlockPos.Zero, direction, supportType),
                    $"Dynamic-shape blocks must evaluate face support without the static cache for {supportType}.");
            }
        }
    }

    private sealed class DynamicSupportBlock : Block
    {
        public DynamicSupportBlock()
            : base(BlockProperties.Of().DynamicShape(), createIntrusiveRegistryHolder: false)
        {
            InitializeStateCaches();
        }
    }

    private static void CheckDefaultDestroyTime()
    {
        var defaultBlock = new DefaultDestroyTimeBlock(BlockProperties.Of());
        Require(
            defaultBlock.GetDefaultDestroyTime() == 0.0f,
            "BlockBehaviour.defaultDestroyTime must expose the default destroy-time value.");

        var properties = BlockProperties.Of().DestroyTime(1.25f);
        var block = new DefaultDestroyTimeBlock(properties);
        Require(
            block.GetDefaultDestroyTime() == 1.25f &&
            block.DefaultState.DestroySpeed == 1.25f,
            "The block default destroy time and initial state destroy speed must both use the configured property.");

        properties.DestroyTime(3.5f);
        Require(
            block.GetDefaultDestroyTime() == 3.5f,
            "BlockBehaviour.defaultDestroyTime must read the retained Properties builder rather than a construction-time copy.");
        Require(
            block.DefaultState.DestroySpeed == 1.25f,
            "An existing BlockState must retain its destroy-speed snapshot when the builder is later mutated.");
    }

    private sealed class DefaultDestroyTimeBlock : Block
    {
        public DefaultDestroyTimeBlock(BlockProperties properties)
            : base(properties, createIntrusiveRegistryHolder: false)
        {
            InitializeStateCaches();
        }
    }

    private static void CheckDefaultViewBlockingTracksSuffocationProperty()
    {
        var properties = BlockProperties.Of();
        var block = new OffsetCacheValidationBlock(properties);
        BlockState state = block.DefaultState;
        var nearPlane = new AABB(0.0, 0.0, 0.0, 1.0, 1.0, 1.0);

        Require(
            !state.IsViewBlocking(EmptyBlockGetter.Instance, BlockPos.Zero, nearPlane),
            "The default view-blocking predicate must initially delegate to the default suffocation predicate.");

        properties.IsSuffocating(static (_, _, _) => true);

        Require(
            state.IsViewBlocking(EmptyBlockGetter.Instance, BlockPos.Zero, nearPlane),
            "The default view-blocking predicate must retain the vanilla closure over BlockProperties and observe a later suffocation-predicate replacement.");
    }

    private static void CheckOffsetCacheValidation()
    {
        InvalidOperationException? exception = null;
        try
        {
            _ = new OffsetCacheValidationBlock(
                BlockProperties.Of().OffsetType(OffsetType.XZ));
        }
        catch (InvalidOperationException caught)
        {
            exception = caught;
        }

        Require(
            exception?.Message ==
                "null has a collision shape and an offset type, but is not marked as dynamicShape in its properties.",
            "A non-dynamic block with a non-empty collision shape and an offset function must fail cache initialization with the vanilla diagnostic.");

        _ = new OffsetCacheValidationBlock(
            BlockProperties.Of().OffsetType(OffsetType.XZ).DynamicShape());
    }

    private static void CheckBlockSeed()
    {
        var position = new BlockPos(-17, 82, 35);
        Require(
            Blocks.STONE.DefaultState.GetSeed(position) ==
            Mth.GetSeed(position.GetX(), position.GetY(), position.GetZ()),
            "The default Block.getSeed implementation must delegate to Mth.getSeed(BlockPos).");

        var block = new OverrideBlock();
        Require(
            block.DefaultState.GetSeed(position) == 987654321L,
            "BlockState.GetSeed must dispatch to the owning block's override.");
    }

    private sealed class OffsetCacheValidationBlock : Block
    {
        public OffsetCacheValidationBlock(BlockProperties properties)
            : base(properties, createIntrusiveRegistryHolder: false)
        {
            InitializeStateCaches();
        }
    }

    private sealed class CollisionTestBlock : Block
    {
        public CollisionTestBlock()
            : base(
                BlockProperties.Of().NoCollision().OffsetType(OffsetType.XZ),
                createIntrusiveRegistryHolder: false)
        {
            InitializeStateCaches();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
