using Asteria.Content.Biomes;
using BuiltInBiomes = Asteria.Content.Biomes.Biomes;
using Asteria.Content.Registries;
using Asteria.Content.Storage;

namespace Asteria.Content.Chunks;

/// <summary>
/// Creates 4x4x4 biome palettes using the biome registry's stable holder IDs.
/// Must be created after built-in registries are frozen so its global ID width is final.
/// </summary>
public sealed class BiomeContainerFactory
{
    public Holder<Biome> DefaultBiome { get; }
    public IIdMap<Holder<Biome>> GlobalIds { get; }
    public PalettedContainerStrategy<Holder<Biome>> Strategy { get; }

    public BiomeContainerFactory(
        IIdMap<Holder<Biome>> globalIds,
        Holder<Biome> defaultBiome)
    {
        GlobalIds = globalIds ?? throw new ArgumentNullException(nameof(globalIds));
        DefaultBiome = defaultBiome ?? throw new ArgumentNullException(nameof(defaultBiome));
        Strategy = PalettedContainerStrategy<Holder<Biome>>.CreateForBiomes(GlobalIds);
    }

    public static BiomeContainerFactory CreateBuiltIn()
    {
        if (!RegistryBootstrap.IsInitialized)
            throw new InvalidOperationException("Biome containers require initialized built-in registries.");

        return new BiomeContainerFactory(
            BuiltInRegistries.Biome.AsHolderIdMap(),
            BuiltInBiomes.PLAINS.BuiltInRegistryHolder);
    }

    public PalettedContainer<Holder<Biome>> CreateForBiomes() =>
        new(DefaultBiome, Strategy);
}
