using Asteria.Content.Biomes;
using Asteria.Content.Blocks;
using Asteria.Content.Registries;
using Asteria.Content.Storage;

namespace Asteria.Content.Chunks;

public sealed record PalettedContainerFactory(
    PalettedContainerStrategy<BlockState> BlockStatesStrategy,
    BlockState DefaultBlockState,
    PalettedContainerStrategy<Holder<Biome>> BiomeStrategy,
    Holder<Biome> DefaultBiome)
{
    public static PalettedContainerFactory CreateBuiltIn()
    {
        if (!RegistryBootstrap.IsInitialized)
            throw new InvalidOperationException("Paletted containers require initialized built-in registries.");

        return new PalettedContainerFactory(
            PalettedContainerStrategy<BlockState>.CreateForBlockStates(Block.BlockStateRegistry),
            Blocks.AIR.DefaultState,
            PalettedContainerStrategy<Holder<Biome>>.CreateForBiomes(
                BuiltInRegistries.Biome.AsHolderIdMap()),
            Biomes.PLAINS.BuiltInRegistryHolder);
    }

    public PalettedContainer<BlockState> CreateForBlockStates() =>
        new(DefaultBlockState, BlockStatesStrategy);

    public PalettedContainer<Holder<Biome>> CreateForBiomes() =>
        new(DefaultBiome, BiomeStrategy);
}
