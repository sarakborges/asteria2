using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;
using Godot;

namespace Asteria.Gameplay;

/// <summary>
/// Owns the active local chunk set and routes block reads and edits to chunk storage.
/// The initial finite arena intentionally uses a flat terrain layer while world generation
/// and chunk streaming are implemented.
/// </summary>
public sealed partial class VoxelWorld : Node3D
{
    public const int ChunkSize = 16;
    public const int ChunkCount = 3;
    public const int WorldSize = ChunkSize * ChunkCount;
    public const int WorldHeight = 32;
    public const int SurfaceHeight = 4;

    private readonly Chunk[,] _chunks = new Chunk[ChunkCount, ChunkCount];
    private readonly VoxelChunkView?[,] _views = new VoxelChunkView?[ChunkCount, ChunkCount];

    public override void _Ready()
    {
        BlockStateContainerFactory factory = BlockStateContainerFactory.CreateBuiltIn();

        for (int chunkX = 0; chunkX < ChunkCount; chunkX++)
        for (int chunkZ = 0; chunkZ < ChunkCount; chunkZ++)
        {
            _chunks[chunkX, chunkZ] = new Chunk(
                new ChunkPos(chunkX, chunkZ),
                LevelHeightAccessor.Create(0, WorldHeight),
                factory);
        }

        GenerateInitialTerrain();

        for (int chunkX = 0; chunkX < ChunkCount; chunkX++)
        for (int chunkZ = 0; chunkZ < ChunkCount; chunkZ++)
        {
            var view = new VoxelChunkView();
            view.Configure(this, _chunks[chunkX, chunkZ]);
            _views[chunkX, chunkZ] = view;
            AddChild(view);
            view.Rebuild();
        }
    }

    public BlockState GetBlockState(int x, int y, int z)
    {
        if (!IsInsideWorld(x, y, z))
            return Blocks.AIR.DefaultState;

        Chunk chunk = _chunks[x >> 4, z >> 4];
        int sectionIndex = chunk.GetSectionIndex(y);
        return chunk.GetSection(sectionIndex).GetBlockState(x & 15, y & 15, z & 15);
    }

    public bool SetBlockState(BlockPos position, BlockState state)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(state);

        int x = position.GetX();
        int y = position.GetY();
        int z = position.GetZ();
        if (!IsInsideWorld(x, y, z))
            return false;

        int chunkX = x >> 4;
        int chunkZ = z >> 4;
        Chunk chunk = _chunks[chunkX, chunkZ];
        ChunkSection section = chunk.GetSection(chunk.GetSectionIndex(y));
        BlockState previous = section.SetBlockState(x & 15, y & 15, z & 15, state);
        if (ReferenceEquals(previous, state))
            return false;

        RefreshChunkView(chunkX, chunkZ);

        if ((x & 15) == 0)
            RefreshChunkView(chunkX - 1, chunkZ);
        else if ((x & 15) == 15)
            RefreshChunkView(chunkX + 1, chunkZ);

        if ((z & 15) == 0)
            RefreshChunkView(chunkX, chunkZ - 1);
        else if ((z & 15) == 15)
            RefreshChunkView(chunkX, chunkZ + 1);

        return true;
    }

    private void GenerateInitialTerrain()
    {
        BlockState stone = Blocks.STONE.DefaultState;
        BlockState dirt = Blocks.DIRT.DefaultState;
        BlockState grass = Blocks.GRASS_BLOCK.DefaultState;

        for (int chunkX = 0; chunkX < ChunkCount; chunkX++)
        for (int chunkZ = 0; chunkZ < ChunkCount; chunkZ++)
        {
            Chunk chunk = _chunks[chunkX, chunkZ];

            for (int localX = 0; localX < ChunkSize; localX++)
            for (int localZ = 0; localZ < ChunkSize; localZ++)
            {
                ChunkSection section = chunk.GetSection(0);
                section.SetBlockState(localX, 0, localZ, stone);
                section.SetBlockState(localX, 1, localZ, stone);
                section.SetBlockState(localX, 2, localZ, dirt);
                section.SetBlockState(localX, 3, localZ, grass);
            }
        }
    }

    private static bool IsInsideWorld(int x, int y, int z) =>
        x >= 0 && x < WorldSize &&
        z >= 0 && z < WorldSize &&
        y >= 0 && y < WorldHeight;

    private void RefreshChunkView(int chunkX, int chunkZ)
    {
        if ((uint)chunkX >= ChunkCount || (uint)chunkZ >= ChunkCount)
            return;

        _views[chunkX, chunkZ]?.Rebuild();
    }
}
