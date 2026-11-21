using Asteria.Content.Blocks.StateProperties;
using Asteria.Content.Fluids;
using Asteria.Content.Registries;

internal static class FluidChecks
{
    private static readonly IntegerProperty LEVEL = IntegerProperty.Create("level", 1, 8);

    private sealed class TestFluid : Fluid
    {
        public TestFluid() : base(createIntrusiveRegistryHolder: false)
        {
        }

        protected override void CreateFluidStateDefinition(
            StateDefinitionBuilder<Fluid, FluidState> builder) =>
            builder.Add(LEVEL);

        public override bool IsRandomlyTicking => true;

        public override bool IsSource(FluidState state) =>
            state.GetValue(LEVEL) == FluidState.AmountFull;

        public override int GetAmount(FluidState state) =>
            state.GetValue(LEVEL);

        public override float GetOwnHeight(FluidState state) =>
            GetAmount(state) / 9.0f;
    }

    public static void Run()
    {
        Require(BuiltInRegistries.Fluid.Count == 1, "The built-in fluid registry must contain the empty fluid.");
        Require(
            ReferenceEquals(BuiltInRegistries.Fluid.GetValue(FluidIds.EMPTY), Fluids.EMPTY),
            "The fluid registry must resolve the empty fluid by its built-in identifier.");
        Require(
            ReferenceEquals(Fluids.EMPTY.BuiltInRegistryHolder?.Value, Fluids.EMPTY),
            "The empty fluid must retain its intrusive registry holder.");
        Require(
            Fluid.FluidStateRegistry.Count == 1,
            "The empty fluid contributes exactly one singleton state.");
        Require(
            Fluid.FluidStateRegistry.GetId(Fluids.EMPTY.DefaultFluidState) == 0,
            "The empty fluid state must receive the first fluid-state ID.");

        FluidState empty = Fluids.EMPTY.DefaultFluidState;
        Require(empty.IsEmpty(), "The empty fluid state must report empty.");
        Require(!empty.IsSource(), "The empty fluid state must not report a source.");
        Require(!empty.IsSourceOfType(Fluids.EMPTY), "An empty fluid state is not a source of its own type.");
        Require(!empty.IsRandomlyTicking(), "The empty fluid must not randomly tick.");
        Require(empty.GetAmount() == 0, "The empty fluid amount must be zero.");
        Require(empty.GetOwnHeight() == 0.0f, "The empty fluid height must be zero.");
        Require(!empty.IsFull(), "The empty fluid state must not be full.");

        var fluid = new TestFluid();
        Require(
            fluid.StateDefinition.PossibleStates.Count == 8,
            "Fluid state definitions must materialize every integer-property combination.");

        FluidState defaultState = fluid.DefaultFluidState;
        Require(defaultState.GetValue(LEVEL) == 1, "Fluid default state must select the first property value.");
        FluidState full = defaultState.SetValue(LEVEL, FluidState.AmountFull);
        Require(full.GetValue(LEVEL) == FluidState.AmountFull, "Fluid state transitions must update the requested property.");
        Require(
            ReferenceEquals(full, defaultState.SetValue(LEVEL, FluidState.AmountFull)),
            "Equivalent fluid transitions must resolve to the same precomputed state instance.");
        Require(full.IsSource() && full.IsSourceOfType(fluid), "FluidState source checks must delegate to the owning fluid.");
        Require(full.IsFull() && full.GetAmount() == 8, "A fluid amount of eight must be full.");
        Require(full.GetOwnHeight() == 8.0f / 9.0f, "Fluid own height must delegate to the owning fluid.");
        Require(full.IsRandomlyTicking(), "Fluid-state random ticking must delegate to the owning fluid.");
        Require(!full.IsEmpty(), "A non-empty fluid type must not report an empty state.");

        FluidState flowing = full.SetValue(LEVEL, 7);
        Require(!flowing.IsSource(), "Non-source fluid states must not report a source.");
        Require(!flowing.IsFull(), "An amount below eight must not be full.");
        Require(
            !flowing.IsSourceOfType(Fluids.EMPTY),
            "A fluid state must reject source checks against another fluid type.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
