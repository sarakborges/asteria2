using System.Reflection;
using Asteria.Content.Blocks;
using Asteria.Content.Fluids;

internal static class StateHolderChecks
{
    public static void Run()
    {
        CheckDefinitionConstructionVisibility();
        CheckBlockStateIdentity();
        CheckBlockTypeHolder();
        CheckFluidStateIdentity();
    }


    private static void CheckDefinitionConstructionVisibility()
    {
        Type definitionType = typeof(StateDefinition<,>);
        Require(
            definitionType.GetConstructors(BindingFlags.Instance | BindingFlags.Public).Length == 0,
            "StateDefinition must not expose a public constructor that bypasses StateDefinitionBuilder property validation.");

        Require(
            definitionType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(constructor => constructor.IsFamilyOrAssembly),
            "StateDefinition construction must remain available to in-assembly builders and derived extension types, matching the protected vanilla constructor.");
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

    private static void CheckBlockTypeHolder()
    {
        Block block = Blocks.STONE;
        BlockState state = block.DefaultState;

        Require(
            ReferenceEquals(state.TypeHolder, block.BuiltInRegistryHolder),
            "BlockStateBase.typeHolder must return the owning block's existing built-in registry holder.");
        Require(
            ReferenceEquals(state.TypeHolder.Value, block),
            "The block-state type holder must resolve to the exact owning block instance.");
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
