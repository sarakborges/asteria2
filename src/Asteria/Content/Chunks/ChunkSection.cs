using Asteria.Content.Blocks;
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
    private short _tickingBlockCount;

    public int NonAirBlockCount => _nonAirBlockCount;
    public int TickingBlockCount => _tickingBlockCount;
    public bool HasOnlyAir => _nonAirBlockCount == 0;
    public bool IsRandomlyTickingBlocks => _tickingBlockCount > 0;
    public bool IsRandomlyTicking => IsRandomlyTickingBlocks;
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
        short tickingBlockCount)
    {
        _states = states;
        _nonAirBlockCount = nonAirBlockCount;
        _tickingBlockCount = tickingBlockCount;
    }

    public BlockState GetBlockState(int x, int y, int z) =>
        _states.Get(x, y, z);

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
        }

        if (!state.IsAir)
        {
            _nonAirBlockCount = unchecked((short)(_nonAirBlockCount + 1));
            if (state.IsRandomlyTicking)
            {
                _tickingBlockCount = unchecked((short)(_tickingBlockCount + 1));
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
        int tickingBlockCount = 0;
        _states.Count((state, count) =>
        {
            if (!state.IsAir)
            {
                nonAirBlockCount += count;
                if (state.IsRandomlyTicking)
                {
                    tickingBlockCount += count;
                }
            }
        });

        _nonAirBlockCount = unchecked((short)nonAirBlockCount);
        _tickingBlockCount = unchecked((short)tickingBlockCount);
    }

    public ChunkSection Copy() =>
        new(_states.Copy(), _nonAirBlockCount, _tickingBlockCount);

}
