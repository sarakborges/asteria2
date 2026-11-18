using Asteria.Content.Blocks;
using Asteria.Content.Storage;

namespace Asteria.Content.Chunks;

/// <summary>
/// A 16x16x16 block-state section, stored using a Minecraft-style paletted container.
/// The Y axis is the most significant coordinate in the linear index.
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

        if (!airState.Block.IsAir)
        {
            throw new ArgumentException("The section default state must belong to an air block.", nameof(airState));
        }

        _states = new PalettedContainer<BlockState>(
            stateIds,
            airState,
            BlockCount);
    }

    public BlockState GetBlockState(int x, int y, int z) =>
        _states.Get(GetIndex(x, y, z));

    public BlockState GetBlockState(int index) => _states.Get(index);

    /// <summary>
    /// Replaces a block state and returns the previous state.
    /// </summary>
    public BlockState SetBlockState(int x, int y, int z, BlockState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        int index = GetIndex(x, y, z);
        BlockState previous = _states.Set(index, state);

        if (ReferenceEquals(previous, state))
        {
            return previous;
        }

        if (previous.Block.IsAir && !state.Block.IsAir)
        {
            _nonAirBlockCount++;
        }
        else if (!previous.Block.IsAir && state.Block.IsAir)
        {
            _nonAirBlockCount--;
        }

        return previous;
    }

    public bool MaybeHas(Func<BlockState, bool> predicate) =>
        _states.MaybeHas(predicate);

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

        return x | (z << 4) | (y << 8);
    }
}
