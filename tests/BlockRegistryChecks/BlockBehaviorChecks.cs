using Asteria.Content.Blocks;
using Asteria.Content.Fluids;
using Asteria.Core;
using Asteria.Core.Shapes;
using Asteria.World;
using Asteria.World.Pathfinding;

internal static class BlockBehaviorChecks
{
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
            air.CanBeReplaced(Fluids.EMPTY),
            "A block with the replaceable property must remain fluid-replaceable.");
        Require(
            !stone.CanBeReplaced(Fluids.EMPTY),
            "A solid, non-replaceable block must reject fluid replacement.");
        Require(
            !nonSolid.IsReplaceable && !nonSolid.IsSolid &&
            nonSolid.CanBeReplaced(Fluids.EMPTY),
            "Fluid replacement must allow a non-solid state even when its replaceable property is false.");
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
