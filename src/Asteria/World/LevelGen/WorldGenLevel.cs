using Asteria.Content.Biomes;
using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Fluids;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;

namespace Asteria.World.LevelGen;

/// <summary>
/// The block, biome and height access used by configured world-generation features.
/// OverworldWorld supplies unrestricted level access; WorldGenRegion supplies the
/// stage-specific chunk dependency and write-radius restrictions.
/// </summary>
public interface WorldGenLevel : BlockGetter
{
    long Seed { get; }

    ChunkAccess GetChunk(int chunkX, int chunkZ);

    void SetCurrentlyGenerating(string? description) { }

    Holder<Biome> GetBiome(BlockPos position);

    int GetHeight(Heightmap.Types type, int blockX, int blockZ);

    bool EnsureCanWrite(BlockPos position);

    bool SetBlock(BlockPos position, BlockState state, int updateFlags = 3);
}
