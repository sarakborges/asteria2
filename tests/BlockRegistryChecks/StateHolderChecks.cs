using Asteria.Content.Blocks;
using Asteria.Content.Fluids;

internal static class StateHolderChecks
{
    public static void Run()
    {
        CheckBlockStateIdentity();
        CheckFluidStateIdentity();
    }

    private static void CheckBlockStateIdentity()
    {
        BlockState air = Blocks.AIR.DefaultState;
        BlockState stone = Blocks.STONE.DefaultState;

        Require(air.Equals(air), "A state holder must equal itself.");
        Require(!air.Equals(stone), "Distinct block-state instances must not compare equal by their contents.");

        var states = new HashSet<BlockState> { air, stone };
        Require(
            states.Count == 2,
            "Block-state hash collections must preserve distinct state instances using reference identity.");
    }

    private static void CheckFluidStateIdentity()
    {
        FluidState empty = Fluids.EMPTY.DefaultFluidState;
        Require(
            empty.Equals(empty),
            "FluidState must inherit the StateHolder reference-identity equality contract.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
