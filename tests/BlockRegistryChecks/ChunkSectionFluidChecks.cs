using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Fluids;
using Asteria.Core;
using Asteria.Core.Shapes;
using Asteria.World;
using Asteria.Content.Storage;

internal static class ChunkSectionFluidChecks
{
    public static void Run()
    {
        var factory = PalettedContainerFactory.CreateBuiltIn();
        var section = new ChunkSection(factory);

        Require(!section.HasFluid, "A newly created chunk section must have no fluid.");
        Require(!section.IsRandomlyTickingFluids, "A newly created chunk section must have no randomly ticking fluid.");

        section.SetBlockState(0, 0, 0, Blocks.WATER.DefaultState);
        Require(section.HasFluid, "Adding a water block state must increment the section fluid count.");

        section.SetBlockState(1, 0, 0, Blocks.LAVA.DefaultState);
        Require(section.HasFluid, "Adding another fluid state must preserve the non-empty fluid count.");
        Require(!section.IsRandomlyTickingFluids, "Still source fluids must not be counted as randomly ticking fluids.");

        ChunkSection copy = section.Copy();
        Require(copy.HasFluid, "Copying a section must preserve its fluid count.");
        copy.SetBlockState(0, 0, 0, Blocks.AIR.DefaultState);
        Require(copy.HasFluid, "Removing one of multiple fluid states must leave the copied section fluid-bearing.");
        copy.SetBlockState(1, 0, 0, Blocks.AIR.DefaultState);
        Require(!copy.HasFluid, "Removing the final fluid state must clear the copied section fluid count.");
        Require(section.HasFluid, "Changing a copy must not mutate the original section's fluid count.");

        section.SetBlockState(0, 0, 0, Blocks.STONE.DefaultState);
        section.SetBlockState(1, 0, 0, Blocks.STONE.DefaultState);
        Require(!section.HasFluid, "Replacing every fluid state with a non-fluid block must clear the section fluid count.");

        section.GetStates().Set(2, 0, 0, Blocks.WATER.DefaultState);
        Require(!section.HasFluid, "Direct palette writes must not implicitly update cached section fluid counts.");
        section.RecalculateBlockCounts();
        Require(section.HasFluid, "RecalculateBlockCounts must rebuild the fluid count after direct palette writes.");
        Require(!section.IsRandomlyTickingFluids, "Recalculation must preserve the non-ticking status of still source fluids.");

        var tickingFluid = new TickingFluid();
        var tickingBlock = new TickingFluidBlock(tickingFluid);
        section.SetBlockState(0, 0, 0, tickingBlock.DefaultState);
        Require(section.HasFluid, "A block exposing a non-empty fluid state must increment the section fluid count.");
        Require(section.IsRandomlyTickingFluids && section.IsRandomlyTicking,
            "A randomly ticking fluid state must increment both fluid and aggregate ticking gates.");

        ChunkSection tickingCopy = section.Copy();
        Require(tickingCopy.IsRandomlyTickingFluids,
            "Copying a section must preserve its randomly-ticking fluid count.");
        tickingCopy.SetBlockState(0, 0, 0, Blocks.AIR.DefaultState);
        Require(!tickingCopy.IsRandomlyTickingFluids && !tickingCopy.IsRandomlyTicking,
            "Removing the final randomly ticking fluid state must clear both ticking gates.");
        Require(section.IsRandomlyTickingFluids,
            "Removing a ticking fluid from a copy must not mutate the original section.");

        section.GetStates().Set(0, 0, 0, tickingBlock.DefaultState);
        section.SetBlockState(0, 0, 0, Blocks.AIR.DefaultState);
        Require(section.IsRandomlyTickingFluids,
            "A direct palette write followed by SetBlockState must preserve vanilla cached-counter behavior.");
        section.RecalculateBlockCounts();
        Require(!section.IsRandomlyTickingFluids,
            "RecalculateBlockCounts must rebuild the randomly-ticking fluid count after direct palette writes.");
    }

    private sealed class TickingFluid : Fluid
    {
        public TickingFluid() : base(createIntrusiveRegistryHolder: false)
        {
        }

        public override bool IsRandomlyTicking => true;
        public override bool IsSource(FluidState state) => true;
        public override int GetAmount(FluidState state) => FluidState.AmountFull;
        public override float GetHeight(FluidState state, BlockGetter level, BlockPos position) => 1.0f;
        public override float GetOwnHeight(FluidState state) => 1.0f;
        public override VoxelShape GetShape(FluidState state, BlockGetter level, BlockPos position) => Shapes.Block();
    }

    private sealed class TickingFluidBlock : Block
    {
        private readonly Fluid _fluid;

        public TickingFluidBlock(Fluid fluid)
            : base(BlockProperties.Of(), createIntrusiveRegistryHolder: false)
        {
            _fluid = fluid;
            InitializeStateCaches();
        }

        public override FluidState GetFluidState(BlockState state) => _fluid.DefaultFluidState;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
