using Asteria.Content.Blocks;
using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.Content.Storage;

namespace Asteria.Content.Chunks;

/// <summary>
/// Creates the paired block-state and biome palettes required by a local chunk section.
/// </summary>
public sealed class BlockStateContainerFactory
{
    public PalettedContainerStrategy<BlockState> BlockStatesStrategy { get; }

    public BlockState DefaultBlockState { get; }

    public BiomeContainerFactory BiomeContainers { get; }

    public BlockStateContainerFactory(
        PalettedContainerStrategy<BlockState> blockStatesStrategy,
        BlockState defaultBlockState,
        BiomeContainerFactory? biomeContainers = null)
    {
        BlockStatesStrategy = blockStatesStrategy ??
            throw new ArgumentNullException(nameof(blockStatesStrategy));
        DefaultBlockState = defaultBlockState ??
            throw new ArgumentNullException(nameof(defaultBlockState));
        BiomeContainers = biomeContainers ?? BiomeContainerFactory.CreateBuiltIn();
    }

    public static BlockStateContainerFactory CreateBuiltIn() =>
        new(
            PalettedContainerStrategy<BlockState>.CreateForBlockStates(Block.BlockStateRegistry),
            Asteria.Content.Blocks.Blocks.AIR.DefaultState);

    public PalettedContainer<BlockState> CreateForBlockStates() =>
        new(DefaultBlockState, BlockStatesStrategy);

    public PalettedContainer<Holder<Biome>> CreateForBiomes() =>
        BiomeContainers.CreateForBiomes();

    public ChunkSection CreateSection() =>
        new(CreateForBlockStates(), CreateForBiomes());
}
