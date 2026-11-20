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
    private ushort _nonAirBlockCount;

    public int NonAirBlockCount => _nonAirBlockCount;
    public bool HasOnlyAir => _nonAirBlockCount == 0;
    public PaletteMode PaletteMode => _states.Mode;
    public int BitsPerEntry => _states.BitsPerEntry;
    public int PaletteSize => _states.PaletteSize;

    public ChunkSection(PalettedContainer<BlockState> states)
    {
        _states = states ?? throw new ArgumentNullException(nameof(states));
        if (_states.Size != BlockCount)
        {
            throw new ArgumentException(
                $"A block-state container must contain exactly {BlockCount} entries.",
                nameof(states));
        }

        RecalculateBlockCounts();
    }

    private ChunkSection(PalettedContainer<BlockState> states, ushort nonAirBlockCount)
    {
        _states = states;
        _nonAirBlockCount = nonAirBlockCount;
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

        if (!ReferenceEquals(previous, state))
        {
            if (previous.Block.Properties.IsAir && !state.Block.Properties.IsAir)
            {
                _nonAirBlockCount++;
            }
            else if (!previous.Block.Properties.IsAir && state.Block.Properties.IsAir)
            {
                _nonAirBlockCount--;
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
        _states.Count((state, count) =>
        {
            if (!state.Block.Properties.IsAir)
            {
                nonAirBlockCount += count;
            }
        });

        _nonAirBlockCount = checked((ushort)nonAirBlockCount);
    }

    public ChunkSection Copy() =>
        new(_states.Copy(), _nonAirBlockCount);

}
