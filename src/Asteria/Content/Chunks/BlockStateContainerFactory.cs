using Asteria.Content.Blocks;
using Asteria.Content.Storage;

namespace Asteria.Content.Chunks;

/// <summary>
/// Block-state portion of Minecraft's PalettedContainerFactory.
/// Biome palette creation remains unavailable until Asteria has a biome domain model.
/// </summary>
public sealed class BlockStateContainerFactory
{
    public PalettedContainerStrategy<BlockState> BlockStatesStrategy { get; }

    public BlockState DefaultBlockState { get; }

    public BlockStateContainerFactory(
        PalettedContainerStrategy<BlockState> blockStatesStrategy,
        BlockState defaultBlockState)
    {
        BlockStatesStrategy = blockStatesStrategy ??
            throw new ArgumentNullException(nameof(blockStatesStrategy));
        DefaultBlockState = defaultBlockState ??
            throw new ArgumentNullException(nameof(defaultBlockState));
    }

    public static BlockStateContainerFactory CreateBuiltIn() =>
        new(
            PalettedContainerStrategy<BlockState>.CreateForBlockStates(Block.BlockStateRegistry),
            Blocks.AIR.DefaultState);

    public PalettedContainer<BlockState> CreateForBlockStates() =>
        new(DefaultBlockState, BlockStatesStrategy);

    public ChunkSection CreateSection() =>
        new(CreateForBlockStates());
}
