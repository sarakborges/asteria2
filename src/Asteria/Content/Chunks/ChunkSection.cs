using Asteria.Content.Blocks;
using Asteria.Content.Fluids;
using Asteria.Content.Storage;

namespace Asteria.Content.Chunks;

/// <summary>
/// A 16x16x16 section whose block states use the vanilla block-state palette strategy.
/// </summary>
public sealed class ChunkSection
{
    public const int Width = 16;
    public const int Height = 16;
    public const int Depth = 16;
    public const int BlockCount = Width * Height * Depth;

    private readonly PalettedContainer<BlockState> _states;
    private short _nonAirBlockCount;
    private short _fluidCount;
    private short _tickingBlockCount;
    private short _tickingFluidCount;

    public int NonAirBlockCount => _nonAirBlockCount;
    public int FluidCount => _fluidCount;
    public int TickingBlockCount => _tickingBlockCount;
    public int TickingFluidCount => _tickingFluidCount;
    public bool HasOnlyAir => _nonAirBlockCount == 0;
    public bool HasFluid => _fluidCount > 0;
    public bool IsRandomlyTickingBlocks => _tickingBlockCount > 0;
    public bool IsRandomlyTickingFluids => _tickingFluidCount > 0;
    public bool IsRandomlyTicking => IsRandomlyTickingBlocks || IsRandomlyTickingFluids;
    public PaletteMode PaletteMode => _states.Mode;
    public int BitsPerEntry => _states.BitsPerEntry;
    public int PaletteSize => _states.PaletteSize;

    public ChunkSection(PalettedContainer<BlockState> states)
    {
        _states = states ?? throw new ArgumentNullException(nameof(states));
        RecalculateBlockCounts();
    }

    private ChunkSection(
        PalettedContainer<BlockState> states,
        short nonAirBlockCount,
        short fluidCount,
        short tickingBlockCount,
        short tickingFluidCount)
    {
        _states = states;
        _nonAirBlockCount = nonAirBlockCount;
        _fluidCount = fluidCount;
        _tickingBlockCount = tickingBlockCount;
        _tickingFluidCount = tickingFluidCount;
    }

    public BlockState GetBlockState(int x, int y, int z) =>
        _states.Get(x, y, z);

    public FluidState GetFluidState(int x, int y, int z) =>
        GetBlockState(x, y, z).GetFluidState();

    public BlockState SetBlockState(int x, int y, int z, BlockState state) =>
        SetBlockState(x, y, z, state, true);

    public BlockState SetBlockState(int x, int y, int z, BlockState state, bool checkThreading)
    {
        ArgumentNullException.ThrowIfNull(state);

        BlockState previous = checkThreading
            ? _states.GetAndSet(x, y, z, state)
            : _states.GetAndSetUnchecked(x, y, z, state);

        if (!previous.IsAir)
        {
            _nonAirBlockCount = unchecked((short)(_nonAirBlockCount - 1));
            if (previous.IsRandomlyTicking)
            {
                _tickingBlockCount = unchecked((short)(_tickingBlockCount - 1));
            }

            FluidState previousFluid = previous.GetFluidState();
            if (!previousFluid.IsEmpty())
            {
                _fluidCount = unchecked((short)(_fluidCount - 1));
                if (previousFluid.IsRandomlyTicking())
                    _tickingFluidCount = unchecked((short)(_tickingFluidCount - 1));
            }
        }

        if (!state.IsAir)
        {
            _nonAirBlockCount = unchecked((short)(_nonAirBlockCount + 1));
            if (state.IsRandomlyTicking)
            {
                _tickingBlockCount = unchecked((short)(_tickingBlockCount + 1));
            }

            FluidState fluid = state.GetFluidState();
            if (!fluid.IsEmpty())
            {
                _fluidCount = unchecked((short)(_fluidCount + 1));
                if (fluid.IsRandomlyTicking())
                    _tickingFluidCount = unchecked((short)(_tickingFluidCount + 1));
            }
        }

        return previous;
    }

    public void Acquire() => _states.Acquire();

    public void Release() => _states.Release();

    public bool MaybeHas(Func<BlockState, bool> predicate) =>
        _states.MaybeHas(predicate);

    /// <summary>Returns the mutable block-state container, matching LevelChunkSection.getStates().</summary>
    public PalettedContainer<BlockState> GetStates() => _states;

    /// <summary>Rebuilds cached section counts from the block-state palette.</summary>
    public void RecalculateBlockCounts()
    {
        int nonAirBlockCount = 0;
        int fluidCount = 0;
        int tickingBlockCount = 0;
        int tickingFluidCount = 0;
        _states.Count((state, count) =>
        {
            if (!state.IsAir)
            {
                nonAirBlockCount += count;
                if (state.IsRandomlyTicking)
                    tickingBlockCount += count;

                FluidState fluid = state.GetFluidState();
                if (!fluid.IsEmpty())
                {
                    fluidCount += count;
                    if (fluid.IsRandomlyTicking())
                        tickingFluidCount += count;
                }
            }
        });

        _nonAirBlockCount = unchecked((short)nonAirBlockCount);
        _fluidCount = unchecked((short)fluidCount);
        _tickingBlockCount = unchecked((short)tickingBlockCount);
        _tickingFluidCount = unchecked((short)tickingFluidCount);
    }

    public ChunkSection Copy() =>
        new(_states.Copy(), _nonAirBlockCount, _fluidCount, _tickingBlockCount, _tickingFluidCount);

}
