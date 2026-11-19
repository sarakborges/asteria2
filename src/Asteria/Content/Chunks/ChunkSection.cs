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

    public ChunkSection(BlockStateIdMap stateIds, BlockState airState)
    {
        ArgumentNullException.ThrowIfNull(stateIds);
        ArgumentNullException.ThrowIfNull(airState);

        if (!airState.Block.Properties.IsAir)
        {
            throw new ArgumentException("The section default state must belong to an air block.", nameof(airState));
        }

        var strategy = PalettedContainerStrategy<BlockState>.CreateForBlockStates(stateIds);
        if (strategy.EntryCount != BlockCount)
        {
            throw new InvalidOperationException("A block-state strategy must create exactly 4096 entries per section.");
        }

        _states = new PalettedContainer<BlockState>(strategy, airState);
    }

    private ChunkSection(PalettedContainer<BlockState> states, ushort nonAirBlockCount)
    {
        _states = states;
        _nonAirBlockCount = nonAirBlockCount;
    }

    public BlockState GetBlockState(int x, int y, int z) =>
        _states.Get(GetIndex(x, y, z));

    public BlockState GetBlockState(int index) => _states.Get(index);

    public BlockState SetBlockState(int x, int y, int z, BlockState state) =>
        SetBlockState(x, y, z, state, true);

    public BlockState SetBlockState(int x, int y, int z, BlockState state, bool checkThreading)
    {
        ArgumentNullException.ThrowIfNull(state);
        int index = GetIndex(x, y, z);

        BlockState previous = checkThreading
            ? _states.GetAndSet(index, state)
            : _states.GetAndSetUnchecked(index, state);

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

    public static int GetIndex(int x, int y, int z)
    {
        if ((uint)x >= (uint)Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "Section X coordinate must be between 0 and 15.");
        }

        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y), y, "Section Y coordinate must be between 0 and 15.");
        }

        if ((uint)z >= (uint)Depth)
        {
            throw new ArgumentOutOfRangeException(nameof(z), z, "Section Z coordinate must be between 0 and 15.");
        }

        return (y << 8) | (z << 4) | x;
    }
}
