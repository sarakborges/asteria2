using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Storage;
using Asteria.Core;

namespace Asteria.World;

/// <summary>
/// Stores the first available world Y for each of a chunk's 16x16 columns.
/// The predicate is supplied by the heightmap type; only the world-surface predicate
/// can be defined from Asteria's current block-state domain.
/// </summary>
public sealed class Heightmap
{
    public const int ColumnCount = 16 * 16;

    private readonly Chunk _chunk;
    private readonly Func<BlockState, bool> _isOpaque;
    private readonly PackedBitStorage _data;

    public int BitsPerEntry => _data.BitsPerEntry;
    public int StorageLongCount => _data.LongCount;

    public Heightmap(Chunk chunk, Func<BlockState, bool> isOpaque)
    {
        _chunk = chunk ?? throw new ArgumentNullException(nameof(chunk));
        _isOpaque = isOpaque ?? throw new ArgumentNullException(nameof(isOpaque));

        int height = _chunk.GetHeight();
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(chunk), "Chunk height must be positive.");

        int bitsPerEntry = PackedBitStorage.RequiredBitsForValueCount(checked(height + 1));
        if (bitsPerEntry == 0)
            throw new ArgumentOutOfRangeException(nameof(chunk), "Chunk height must be positive.");

        _data = new PackedBitStorage(bitsPerEntry, ColumnCount);
    }

    /// <summary>
    /// Creates the world-surface variant, whose reference predicate is simply non-air.
    /// The world-generation and live-world variants share this predicate in the reference.
    /// </summary>
    public static Heightmap CreateWorldSurface(Chunk chunk) =>
        new(chunk, static state => !state.IsAir);

    /// <summary>
    /// Scans each column downward from the highest non-empty section and primes its first available Y.
    /// The caller should prime a newly created map before relying on its values.
    /// </summary>
    public void Prime()
    {
        int highestSectionPosition;
#pragma warning disable CS0618
        highestSectionPosition = _chunk.GetHighestSectionPosition() + 16;
#pragma warning restore CS0618

        var position = new BlockPos.MutableBlockPos();
        int minY = _chunk.GetMinY();

        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                for (int y = highestSectionPosition - 1; y >= minY; y--)
                {
                    position.Set(x, y, z);
                    BlockState state = _chunk.GetBlockState(position);
                    if (!state.IsAir && _isOpaque(state))
                    {
                        SetHeight(x, z, unchecked(y + 1));
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Applies one block-state change to a column. blockY is world Y; X and Z are section-local.
    /// Returns true when the stored column height changes.
    /// </summary>
    public bool Update(int localX, int blockY, int localZ, BlockState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        int firstAvailable = GetFirstAvailable(localX, localZ);
        if (blockY <= firstAvailable - 2)
            return false;

        if (_isOpaque(state))
        {
            if (blockY >= firstAvailable)
            {
                SetHeight(localX, localZ, unchecked(blockY + 1));
                return true;
            }
        }
        else if (firstAvailable - 1 == blockY)
        {
            var position = new BlockPos.MutableBlockPos();

            for (int y = blockY - 1; y >= _chunk.GetMinY(); y--)
            {
                position.Set(localX, y, localZ);
                if (_isOpaque(_chunk.GetBlockState(position)))
                {
                    SetHeight(localX, localZ, unchecked(y + 1));
                    return true;
                }

                if (y == int.MinValue)
                    break;
            }

            SetHeight(localX, localZ, _chunk.GetMinY());
            return true;
        }

        return false;
    }

    public int GetFirstAvailable(int localX, int localZ) =>
        unchecked(_data.Get(GetIndex(localX, localZ)) + _chunk.GetMinY());

    public int GetHighestTaken(int localX, int localZ) =>
        unchecked(GetFirstAvailable(localX, localZ) - 1);

    /// <summary>Returns the backing packed words, matching Heightmap.getRawData().</summary>
    public ulong[] GetRawData() => _data.GetRaw();

    private void SetHeight(int localX, int localZ, int height) =>
        _data.Set(
            GetIndex(localX, localZ),
            unchecked(height - _chunk.GetMinY()));

    private static int GetIndex(int localX, int localZ) =>
        unchecked(localX + localZ * 16);
}
