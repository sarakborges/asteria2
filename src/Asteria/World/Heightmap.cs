using System.Diagnostics;
using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Storage;
using Asteria.Core;

namespace Asteria.World;

public sealed class Heightmap
{
    private const int ColumnCount = 256;

    private static readonly Func<BlockState, bool> NotAir = static state => !state.IsAir;
    private static readonly Func<BlockState, bool> MaterialMotionBlocking =
        static state => state.Is(BlockTags.BlocksMotionInHeightmap);

    private readonly ChunkAccess _chunk;
    private readonly Func<BlockState, bool> _isOpaque;
    private readonly PackedBitStorage _data;

    public Heightmap(ChunkAccess chunk, Types type)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(type);

        _isOpaque = type.IsOpaque();
        _chunk = chunk;
        int heightBits = PackedBitStorage.RequiredBitsForValueCount(unchecked(chunk.GetHeight() + 1));
        _data = new PackedBitStorage(heightBits, ColumnCount);
    }

    public static void PrimeHeightmaps(ChunkAccess chunk, IReadOnlySet<Types> types)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(types);

        if (types.Count == 0)
            return;

        var allHeightmaps = new List<Heightmap>(types.Count);
        foreach (Types type in types)
            allHeightmaps.Add(chunk.GetOrCreateHeightmapUnprimed(type));

        var remainingHeightmaps = new List<Heightmap>(types.Count);
#pragma warning disable CS0618
        int highestSectionPosition = chunk.GetHighestSectionPosition() + 16;
#pragma warning restore CS0618
        var position = new BlockPos.MutableBlockPos();

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

    public void SetRawData(ChunkAccess chunk, Types type, ulong[] data)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(data);

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
            PrimeHeightmaps(chunk, new HashSet<Types> { type });
        }
    }

    public ulong[] GetRawData() => _data.GetRaw();

    private void SetHeight(int x, int z, int height) =>
        _data.Set(
            GetIndex(x, z),
            unchecked(height - _chunk.GetMinY()));

    private static int GetIndex(int x, int z) =>
        unchecked(x + z * 16);

    public sealed class Types
    {
        private readonly int _id;
        private readonly string _serializationKey;
        private readonly Usage _usage;
        private readonly Func<BlockState, bool> _isOpaque;

        private Types(
            int id,
            string serializationKey,
            Usage usage,
            Func<BlockState, bool> isOpaque)
        {
            _id = id;
            _serializationKey = serializationKey;
            _usage = usage;
            _isOpaque = isOpaque;
        }

        public static readonly Types WORLD_SURFACE_WG =
            new(0, "WORLD_SURFACE_WG", Usage.WORLDGEN, NotAir);
        public static readonly Types WORLD_SURFACE =
            new(1, "WORLD_SURFACE", Usage.CLIENT, NotAir);
        public static readonly Types OCEAN_FLOOR_WG =
            new(2, "OCEAN_FLOOR_WG", Usage.WORLDGEN, MaterialMotionBlocking);
        public static readonly Types OCEAN_FLOOR =
            new(3, "OCEAN_FLOOR", Usage.LIVE_WORLD, MaterialMotionBlocking);
        public static readonly Types MOTION_BLOCKING =
            new(
                4,
                "MOTION_BLOCKING",
                Usage.CLIENT,
                static state => state.Is(BlockTags.BlocksMotionInHeightmap) ||
                    !state.GetFluidState().IsEmpty());
        public static readonly Types MOTION_BLOCKING_NO_LEAVES =
            new(
                5,
                "MOTION_BLOCKING_NO_LEAVES",
                Usage.CLIENT,
                static state => state.Is(BlockTags.BlocksMotionInHeightmapNoLeaves) ||
                    !state.GetFluidState().IsEmpty());

        internal int Id => _id;

        public string GetSerializationKey() => _serializationKey;

        public bool SendToClient() => _usage == Usage.CLIENT;

        public bool KeepAfterWorldgen() => _usage != Usage.WORLDGEN;

        public Func<BlockState, bool> IsOpaque() => _isOpaque;

        public string GetSerializedName() => _serializationKey;
    }

    public enum Usage
    {
        WORLDGEN,
        LIVE_WORLD,
        CLIENT
    }
}
