using System.Diagnostics;
using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Storage;
using Asteria.Core;

namespace Asteria.World;

public sealed class Heightmap
{
    private const int ColumnCount = 256;

    private readonly Chunk _chunk;
    private readonly Func<BlockState, bool> _isOpaque;
    private readonly PackedBitStorage _data;

    public Heightmap(Chunk chunk, Types type)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        _isOpaque = GetPredicate(type);
        _chunk = chunk;
        int heightBits = PackedBitStorage.RequiredBitsForValueCount(unchecked(chunk.GetHeight() + 1));
        _data = new PackedBitStorage(heightBits, ColumnCount);
    }

    private static Func<BlockState, bool> GetPredicate(Types type) =>
        type switch
        {
            Types.WorldSurfaceWorldgen or Types.WorldSurface =>
                static state => !state.IsAir,
            Types.OceanFloorWorldgen or Types.OceanFloor =>
                static state => state.Is(BlockTags.BlocksMotionInHeightmap),
            Types.MotionBlocking =>
                static state => state.Is(BlockTags.BlocksMotionInHeightmap) ||
                    !state.GetFluidState().IsEmpty(),
            Types.MotionBlockingNoLeaves =>
                static state => state.Is(BlockTags.BlocksMotionInHeightmapNoLeaves) ||
                    !state.GetFluidState().IsEmpty(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown heightmap type.")
        };

    public static void PrimeHeightmaps(Chunk chunk, IEnumerable<Types> types)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(types);

        var allHeightmaps = new List<Heightmap>();
        var seen = new HashSet<Types>();
        foreach (Types type in types)
        {
            if (seen.Add(type))
                allHeightmaps.Add(chunk.GetOrCreateHeightmapUnprimed(type));
        }

        if (allHeightmaps.Count == 0)
            return;

#pragma warning disable CS0618
        int highestSectionPosition = chunk.GetHighestSectionPosition() + 16;
#pragma warning restore CS0618
        var position = new BlockPos.MutableBlockPos();
        var remainingHeightmaps = new List<Heightmap>(allHeightmaps.Count);

        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                remainingHeightmaps.Clear();
                remainingHeightmaps.AddRange(allHeightmaps);

                for (int y = highestSectionPosition - 1; y >= chunk.GetMinY(); y--)
                {
                    position.Set(x, y, z);
                    BlockState state = chunk.GetBlockState(position);
                    if (!state.Is(global::Asteria.Content.Blocks.Blocks.AIR))
                    {
                        for (int index = 0; index < remainingHeightmaps.Count; index++)
                        {
                            Heightmap heightmap = remainingHeightmaps[index];
                            if (heightmap._isOpaque(state))
                            {
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
    }

    public bool Update(int localX, int localY, int localZ, BlockState state)
    {
        int firstAvailable = GetFirstAvailable(localX, localZ);
        if (localY <= firstAvailable - 2)
            return false;

        if (_isOpaque(state))
        {
            if (localY >= firstAvailable)
            {
                SetHeight(localX, localZ, unchecked(localY + 1));
                return true;
            }
        }
        else if (firstAvailable - 1 == localY)
        {
            var position = new BlockPos.MutableBlockPos();

            for (int y = localY - 1; y >= _chunk.GetMinY(); y--)
            {
                position.Set(localX, y, localZ);
                if (_isOpaque(_chunk.GetBlockState(position)))
                {
                    SetHeight(localX, localZ, unchecked(y + 1));
                    return true;
                }
            }

            SetHeight(localX, localZ, _chunk.GetMinY());
            return true;
        }

        return false;
    }

    public int GetFirstAvailable(int x, int z) =>
        unchecked(_data.Get(GetIndex(x, z)) + _chunk.GetMinY());

    public int GetHighestTaken(int x, int z) =>
        unchecked(GetFirstAvailable(x, z) - 1);

    public void SetRawData(Chunk chunk, Types type, ulong[] data)
    {
        ulong[] rawData = _data.GetRaw();
        if (rawData.Length == data.Length)
        {
            Array.Copy(data, rawData, data.Length);
        }
        else
        {
            Trace.TraceWarning(
                "Ignoring heightmap data for chunk {0}, size does not match; expected: {1}, got: {2}",
                chunk.GetPos(),
                rawData.Length,
                data.Length);
            PrimeHeightmaps(chunk, new[] { type });
        }
    }

    public ulong[] GetRawData() => _data.GetRaw();

    private void SetHeight(int x, int z, int height) =>
        _data.Set(
            GetIndex(x, z),
            unchecked(height - _chunk.GetMinY()));

    private static int GetIndex(int x, int z) =>
        unchecked(x + z * 16);

    public enum Types
    {
        WorldSurfaceWorldgen = 0,
        WorldSurface = 1,
        OceanFloorWorldgen = 2,
        OceanFloor = 3,
        MotionBlocking = 4,
        MotionBlockingNoLeaves = 5
    }
}

public static class HeightmapTypeExtensions
{
    private static readonly Heightmap.Types[] Values = Enum.GetValues<Heightmap.Types>();

    public static Heightmap.Types ById(int id) =>
        (uint)id < (uint)Values.Length ? Values[id] : Values[0];

    public static int GetId(this Heightmap.Types type) => (int)type;

    public static string GetSerializationKey(this Heightmap.Types type) =>
        type switch
        {
            Heightmap.Types.WorldSurfaceWorldgen => "WORLD_SURFACE_WG",
            Heightmap.Types.WorldSurface => "WORLD_SURFACE",
            Heightmap.Types.OceanFloorWorldgen => "OCEAN_FLOOR_WG",
            Heightmap.Types.OceanFloor => "OCEAN_FLOOR",
            Heightmap.Types.MotionBlocking => "MOTION_BLOCKING",
            Heightmap.Types.MotionBlockingNoLeaves => "MOTION_BLOCKING_NO_LEAVES",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown heightmap type.")
        };

    public static string GetSerializedName(this Heightmap.Types type) =>
        type.GetSerializationKey();

    public static bool SendToClient(this Heightmap.Types type) =>
        type switch
        {
            Heightmap.Types.WorldSurface or
            Heightmap.Types.MotionBlocking or
            Heightmap.Types.MotionBlockingNoLeaves => true,
            Heightmap.Types.WorldSurfaceWorldgen or
            Heightmap.Types.OceanFloorWorldgen or
            Heightmap.Types.OceanFloor => false,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown heightmap type.")
        };

    public static bool KeepAfterWorldgen(this Heightmap.Types type) =>
        type switch
        {
            Heightmap.Types.WorldSurfaceWorldgen or
            Heightmap.Types.OceanFloorWorldgen => false,
            Heightmap.Types.WorldSurface or
            Heightmap.Types.OceanFloor or
            Heightmap.Types.MotionBlocking or
            Heightmap.Types.MotionBlockingNoLeaves => true,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown heightmap type.")
        };
}
