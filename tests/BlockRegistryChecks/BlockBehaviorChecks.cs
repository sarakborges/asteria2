using Asteria.Content.Blocks;
using Asteria.Core;
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
