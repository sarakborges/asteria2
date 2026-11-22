using Asteria.Content.Blocks;
using Asteria.Content.Blocks.StateProperties;
using Asteria.Content.Chunks;
using Asteria.Content.Fluids;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Content.Storage;
using Asteria.Core;
using Asteria.Core.Shapes;
using Asteria.World;

internal static class FluidChecks
{
    private static readonly IntegerProperty LEVEL = IntegerProperty.Create("level", 1, 8);

    private static TestFluid? _tickFluid;

    public static BlockState WaterloggedBlockState { get; private set; } = null!;

    public static FluidState SourceFluidState { get; private set; } = null!;

    private sealed class FluidBlock : Block
    {
        private readonly FluidState _fluidState;

        public FluidBlock(FluidState fluidState)
            : base(BlockProperties.Of(), createIntrusiveRegistryHolder: false)
        {
            _fluidState = fluidState ?? throw new ArgumentNullException(nameof(fluidState));
            InitializeStateCaches();
        }

        public override FluidState GetFluidState(BlockState state) => _fluidState;
    }

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

        public override float GetHeight(FluidState state, BlockGetter level, BlockPos position) =>
            state.GetOwnHeight();

        public override float GetOwnHeight(FluidState state) =>
            GetAmount(state) / 9.0f;

        public override VoxelShape GetShape(FluidState state, BlockGetter level, BlockPos position) =>
            Shapes.Box(0.0, 0.0, 0.0, 1.0, state.GetHeight(level, position), 1.0);
    }

    public static void Run()
    {
        Require(BuiltInRegistries.Fluid.Count == 1, "The built-in fluid registry must contain the empty fluid.");
        Require(
            FluidTags.Water.Registry == BuiltInRegistries.Fluid.Key &&
            FluidTags.Water.Location == ResourceLocation.Parse("asteria:water"),
            "The water tag must target the built-in fluid registry using its canonical namespace and path.");
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
        Require(!empty.Fluid.Is(FluidTags.Water), "The empty fluid must not be a member of the water tag by default.");
        Require(!empty.IsSource(), "The empty fluid state must not report a source.");
        Require(!empty.IsSourceOfType(Fluids.EMPTY), "An empty fluid state is not a source of its own type.");
        Require(!empty.IsRandomlyTicking(), "The empty fluid must not randomly tick.");
        Require(empty.GetAmount() == 0, "The empty fluid amount must be zero.");
        Require(empty.GetOwnHeight() == 0.0f, "The empty fluid height must be zero.");
        Require(
            empty.GetHeight(EmptyBlockGetter.Instance, BlockPos.Zero) == 0.0f,
            "The empty fluid contextual height must be zero.");
        Require(
            empty.GetShape(EmptyBlockGetter.Instance, BlockPos.Zero).IsEmpty(),
            "The empty fluid shape must be empty.");
        Require(
            empty.GetAABB(EmptyBlockGetter.Instance, BlockPos.Zero) is null,
            "An empty fluid must not have a bounding box.");
        Require(!empty.IsFull(), "The empty fluid state must not be full.");

        var fluid = new TestFluid();
        _tickFluid = fluid;
        Require(
            fluid.StateDefinition.PossibleStates.Count == 8,
            "Fluid state definitions must materialize every integer-property combination.");

        FluidState defaultState = fluid.DefaultFluidState;
        Require(defaultState.GetValue(LEVEL) == 1, "Fluid default state must select the first property value.");
        FluidState full = defaultState.SetValue(LEVEL, FluidState.AmountFull);
        SourceFluidState = full;
        Require(full.GetValue(LEVEL) == FluidState.AmountFull, "Fluid state transitions must update the requested property.");
        Require(
            ReferenceEquals(full, defaultState.SetValue(LEVEL, FluidState.AmountFull)),
            "Equivalent fluid transitions must resolve to the same precomputed state instance.");
        Require(full.IsSource() && full.IsSourceOfType(fluid), "FluidState source checks must delegate to the owning fluid.");
        Require(full.IsFull() && full.GetAmount() == 8, "A fluid amount of eight must be full.");
        Require(full.GetOwnHeight() == 8.0f / 9.0f, "Fluid own height must delegate to the owning fluid.");
        Require(
            full.GetHeight(EmptyBlockGetter.Instance, BlockPos.Zero) == 8.0f / 9.0f,
            "Fluid contextual height must delegate to the owning fluid.");
        Require(
            Math.Abs(full.GetShape(EmptyBlockGetter.Instance, BlockPos.Zero).Max(Direction.Axis.Y) - 8.0 / 9.0) < 1.0E-6,
            "Fluid shape must use the contextual fluid height.");
        var fluidPosition = new BlockPos(4, 5, -2);
        AABB? fluidBounds = full.GetAABB(EmptyBlockGetter.Instance, fluidPosition);
        Require(
            fluidBounds is not null &&
            fluidBounds.MinX == 4.0 && fluidBounds.MinY == 5.0 && fluidBounds.MinZ == -2.0 &&
            fluidBounds.MaxX == 5.0 && fluidBounds.MaxZ == -1.0 &&
            Math.Abs(fluidBounds.MaxY - (5.0 + 8.0 / 9.0)) < 1.0E-6,
            "A fluid bounding box must cover the block footprint up to its contextual height.");
        Require(full.IsRandomlyTicking(), "Fluid-state random ticking must delegate to the owning fluid.");
        Require(!full.IsEmpty(), "A non-empty fluid type must not report an empty state.");

        FluidState flowing = full.SetValue(LEVEL, 7);
        Require(!flowing.IsSource(), "Non-source fluid states must not report a source.");
        Require(!flowing.IsFull(), "An amount below eight must not be full.");
        Require(
            !flowing.IsSourceOfType(Fluids.EMPTY),
            "A fluid state must reject source checks against another fluid type.");

        WaterloggedBlockState = new FluidBlock(defaultState).DefaultState;
    }

    public static void CheckChunkSectionIntegration()
    {
        TestFluid fluid = _tickFluid ?? throw new InvalidOperationException("Fluid checks must run first.");
        BlockState blockState = WaterloggedBlockState;
        PalettedContainerFactory factory = PalettedContainerFactory.CreateBuiltIn();
        var section = new ChunkSection(factory.CreateForBlockStates(), factory.CreateForBiomes());

        section.SetBlockState(1, 2, 3, blockState);
        Require(
            section.HasFluid,
            "Adding a non-air fluid block must mark the section as containing fluid.");
        Require(
            section.IsRandomlyTickingFluids && section.IsRandomlyTicking,
            "Randomly ticking fluid states must set the fluid and aggregate ticking flags.");
        Require(
            ReferenceEquals(section.GetFluidState(1, 2, 3), fluid.DefaultFluidState),
            "Section fluid lookup must delegate through the stored block state.");

        ChunkSection copy = section.Copy();
        Require(
            copy.HasFluid && copy.IsRandomlyTickingFluids,
            "Copy must preserve fluid and ticking-fluid state.");
        copy.SetBlockState(1, 2, 3, Blocks.AIR.DefaultState);
        Require(
            !copy.HasFluid && !copy.IsRandomlyTickingFluids && !copy.IsRandomlyTicking,
            "Replacing a fluid block with air must clear fluid and aggregate ticking state independently of the source section.");
        Require(
            section.HasFluid && section.IsRandomlyTickingFluids,
            "Mutating a copied section must not change the original fluid state.");

        section.SetBlockState(1, 2, 3, Blocks.AIR.DefaultState);
        Require(
            !section.HasFluid && !section.IsRandomlyTickingFluids,
            "Removing the last fluid block must clear the section fluid state.");

        PalettedContainer<BlockState> prefilled = factory.CreateForBlockStates();
        prefilled.Set(4, 5, 6, blockState);
        var prefilledSection = new ChunkSection(prefilled, factory.CreateForBiomes());
        Require(
            prefilledSection.HasFluid && prefilledSection.IsRandomlyTickingFluids,
            "The section constructor must rebuild fluid and random-ticking flags from prefilled states.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
