using System.Diagnostics;
using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Storage;
using Asteria.Core;

namespace Asteria.World;

/// <summary>
/// Stores the first available world Y for each of a chunk's 16x16 columns.
/// </summary>
public sealed class Heightmap
{
    public const int ColumnCount = 16 * 16;

    private readonly Chunk _chunk;
    private readonly Func<BlockState, bool> _isOpaque;
    private readonly PackedBitStorage _data;

    public HeightmapType? Type { get; }
    public int BitsPerEntry => _data.BitsPerEntry;
    public int StorageLongCount => _data.LongCount;

    public Heightmap(Chunk chunk, Func<BlockState, bool> isOpaque)
        : this(chunk, isOpaque, null)
    {
    }

    public Heightmap(Chunk chunk, HeightmapType type)
        : this(chunk, GetPredicate(type), type)
    {
    }

    private Heightmap(Chunk chunk, Func<BlockState, bool> isOpaque, HeightmapType? type)
    {
        _chunk = chunk ?? throw new ArgumentNullException(nameof(chunk));
        _isOpaque = isOpaque ?? throw new ArgumentNullException(nameof(isOpaque));
        Type = type;

        int height = _chunk.GetHeight();
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(chunk), "Chunk height must be positive.");

        int bitsPerEntry = PackedBitStorage.RequiredBitsForValueCount(checked(height + 1));
        if (bitsPerEntry == 0)
            throw new ArgumentOutOfRangeException(nameof(chunk), "Chunk height must be positive.");

        _data = new PackedBitStorage(bitsPerEntry, ColumnCount);
    }

    private static Func<BlockState, bool> GetPredicate(HeightmapType type) =>
        type switch
        {
            HeightmapType.WorldSurfaceWorldgen or HeightmapType.WorldSurface =>
                static state => !state.IsAir,
            HeightmapType.OceanFloorWorldgen or HeightmapType.OceanFloor =>
                static state => state.Is(BlockTags.BlocksMotionInHeightmap),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown heightmap type.")
        };

    public static Heightmap CreateWorldSurface(Chunk chunk) =>
        new(chunk, HeightmapType.WorldSurface);

    public static Heightmap CreateOceanFloor(Chunk chunk) =>
        new(chunk, HeightmapType.OceanFloor);

    /// <summary>
    /// Primes several heightmaps together so each block column is scanned only once.
    /// All maps must belong to the same chunk, matching Heightmap.primeHeightmaps.
    /// </summary>
    public static void PrimeAll(IEnumerable<Heightmap> heightmaps)
    {
        ArgumentNullException.ThrowIfNull(heightmaps);

        Heightmap[] allHeightmaps = heightmaps.ToArray();
        if (allHeightmaps.Length == 0)
            return;

        Heightmap firstHeightmap = allHeightmaps[0]
            ?? throw new ArgumentException("Heightmap collection cannot contain null entries.", nameof(heightmaps));
        Chunk chunk = firstHeightmap._chunk;

        foreach (Heightmap? heightmap in allHeightmaps)
        {
            ArgumentNullException.ThrowIfNull(heightmap);
            if (!ReferenceEquals(heightmap._chunk, chunk))
            {
                throw new ArgumentException(
                    "All heightmaps in a priming pass must belong to the same chunk.",
                    nameof(heightmaps));
            }
        }

        int highestSectionPosition;
#pragma warning disable CS0618
        highestSectionPosition = chunk.GetHighestSectionPosition() + 16;
#pragma warning restore CS0618

        var position = new BlockPos.MutableBlockPos();
        var remainingHeightmaps = new List<Heightmap>(allHeightmaps.Length);
        int minY = chunk.GetMinY();

        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                remainingHeightmaps.Clear();
                remainingHeightmaps.AddRange(allHeightmaps);

                for (int y = highestSectionPosition - 1; y >= minY; y--)
                {
                    position.Set(x, y, z);
                    BlockState state = chunk.GetBlockState(position);

                    if (!state.Is(global::Asteria.Content.Blocks.Blocks.AIR))
                    {
                        for (int index = 0; index < remainingHeightmaps.Count; index++)
                        {
                            Heightmap heightmap = remainingHeightmaps[index];
                            if (!heightmap._isOpaque(state))
                                continue;

                            heightmap.SetHeight(x, z, unchecked(y + 1));
                            remainingHeightmaps.RemoveAt(index);
                            index--;
                        }
                    }

                    if (remainingHeightmaps.Count == 0)
                        break;
                }
            }
        }
    }

    /// <summary>Primes this heightmap using the shared multi-map scan.</summary>
    public void Prime() => PrimeAll(new[] { this });

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

    /// <summary>
    /// Loads packed data when its word count matches this heightmap. Incompatible data is
    /// discarded and this map is recomputed from the owning chunk, matching 26.3.
    /// </summary>
    public void SetRawData(Chunk chunk, HeightmapType type, ulong[] data)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(data);

        ulong[] rawData = _data.GetRaw();
        if (rawData.Length == data.Length)
        {
            Array.Copy(data, rawData, data.Length);
            return;
        }

        Trace.TraceWarning(
            "Ignoring heightmap data for chunk {0}: size mismatch, expected {1}, got {2}",
            chunk.GetPos(),
            rawData.Length,
            data.Length);
        PrimeAll(new[] { chunk.GetOrCreateHeightmapUnprimed(type) });
    }

    /// <summary>Returns the backing packed words, matching Heightmap.getRawData().</summary>
    public ulong[] GetRawData() => _data.GetRaw();

    private void SetHeight(int localX, int localZ, int height) =>
        _data.Set(
            GetIndex(localX, localZ),
            unchecked(height - _chunk.GetMinY()));

    private static int GetIndex(int localX, int localZ) =>
        unchecked(localX + localZ * 16);
}
