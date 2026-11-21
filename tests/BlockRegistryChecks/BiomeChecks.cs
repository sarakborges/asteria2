using Asteria.Content.Biomes;
using Asteria.Content.Chunks;
using Asteria.Content.Registries;

internal static class BiomeChecks
{
    public static void Run()
    {
        CheckBuiltInBiomeRegistry();
        CheckBiomeSectionPalette();
    }

    private static void CheckBuiltInBiomeRegistry()
    {
        Require(BuiltInRegistries.Biome.Count == 2,
            "The initial local biome registry must contain plains and desert.");
        Require(BuiltInRegistries.Biome.DefaultKey == Asteria.Content.Identifiers.ResourceLocation.Parse("asteria:plains"),
            "The biome registry must default to plains.");
        Require(
            ReferenceEquals(
                BuiltInRegistries.Biome.GetValue(
                    Asteria.Content.Identifiers.ResourceLocation.Parse("asteria:plains")),
                Biomes.PLAINS),
            "The plains registry key must resolve to the built-in plains climate definition.");
        Require(
            ReferenceEquals(
                BuiltInRegistries.Biome.GetValue(
                    Asteria.Content.Identifiers.ResourceLocation.Parse("asteria:missing")),
                Biomes.PLAINS),
            "Unknown defaulted biome lookups must resolve to the plains default.");
        Require(
            ReferenceEquals(BuiltInRegistries.Biome.AsHolderIdMap().ById(0), Biomes.PLAINS.BuiltInRegistryHolder) &&
            ReferenceEquals(BuiltInRegistries.Biome.AsHolderIdMap().ById(1), Biomes.DESERT.BuiltInRegistryHolder),
            "Biome palette IDs must resolve to the stable holder references in registry order.");

        Require(
            Biomes.PLAINS.HasPrecipitation &&
            Biomes.PLAINS.GetBaseTemperature() == 0.8f &&
            Biomes.PLAINS.GetDownfall() == 0.4f &&
            Biomes.PLAINS.Climate.TemperatureModifier == Biome.TemperatureModifier.NONE,
            "Plains climate data must preserve its precipitation, temperature, modifier and downfall.");
        Require(
            !Biomes.DESERT.HasPrecipitation &&
            Biomes.DESERT.GetBaseTemperature() == 2.0f &&
            Biomes.DESERT.GetDownfall() == 0.0f,
            "Desert climate data must preserve its dry, hot climate.");
        Require(
            Biome.Precipitation.RAIN.GetSerializedName() == "rain" &&
            Biome.Precipitation.SNOW.GetSerializedName() == "snow" &&
            Biome.Precipitation.NONE.GetSerializedName() == "none" &&
            Biome.TemperatureModifier.FROZEN.GetSerializedName() == "frozen",
            "Biome climate enums must preserve their serialized names.");
    }

    private static void CheckBiomeSectionPalette()
    {
        BiomeContainerFactory biomeFactory = BiomeContainerFactory.CreateBuiltIn();
        Require(
            biomeFactory.Strategy.BitsPerAxis == 2 &&
            biomeFactory.Strategy.EntryCount == ChunkSection.BiomeCount &&
            biomeFactory.Strategy.EntryCount == 64,
            "The biome palette must use a 4x4x4 quart grid with 64 entries.");

        ChunkSection section = BlockStateContainerFactory.CreateBuiltIn().CreateSection();
        Require(section.GetBiomes().Size == 64,
            "LevelChunkSection biome storage must expose 64 quart cells.");
        Require(
            ReferenceEquals(section.GetNoiseBiome(0, 0, 0), Biomes.PLAINS.BuiltInRegistryHolder),
            "New sections must initialize all biome cells to the registry default holder.");

        section.FillBiomesFromNoise(
            new CheckerboardResolver(
                Biomes.PLAINS.BuiltInRegistryHolder,
                Biomes.DESERT.BuiltInRegistryHolder),
            quartMinX: -4,
            quartMinY: 8,
            quartMinZ: 20);

        Require(
            ReferenceEquals(section.GetNoiseBiome(1, 2, 3), Biomes.PLAINS.BuiltInRegistryHolder) &&
            ReferenceEquals(section.GetNoiseBiome(1, 2, 2), Biomes.DESERT.BuiltInRegistryHolder),
            "Biome resolution must use world quart coordinates while storing local 0..3 coordinates.");

        int plainsCount = 0;
        int desertCount = 0;
        section.GetBiomes().Count((holder, count) =>
        {
            if (ReferenceEquals(holder, Biomes.PLAINS.BuiltInRegistryHolder))
                plainsCount += count;
            else if (ReferenceEquals(holder, Biomes.DESERT.BuiltInRegistryHolder))
                desertCount += count;
        });
        Require(
            plainsCount == 32 && desertCount == 32,
            "Palette counting must cover all 64 biome cells exactly once.");

        ChunkSection copy = section.Copy();
        copy.FillBiomesFromNoise(
            new ConstantBiomeResolver(Biomes.DESERT.BuiltInRegistryHolder),
            0, 0, 0);
        Require(
            ReferenceEquals(copy.GetNoiseBiome(2, 2, 2), Biomes.DESERT.BuiltInRegistryHolder) &&
            ReferenceEquals(section.GetNoiseBiome(0, 0, 0), Biomes.PLAINS.BuiltInRegistryHolder),
            "A copied section must own an independent biome palette.");
        Require(section.HasOnlyAir && copy.HasOnlyAir,
            "Biome filling must not change block-state counts or section occupancy.");
    }

    private sealed class CheckerboardResolver : BiomeResolver
    {
        private readonly Holder<Biome> _plains;
        private readonly Holder<Biome> _desert;

        public CheckerboardResolver(Holder<Biome> plains, Holder<Biome> desert)
        {
            _plains = plains;
            _desert = desert;
        }

        public Holder<Biome> GetNoiseBiome(int quartX, int quartY, int quartZ) =>
            ((quartX + quartY + quartZ) & 1) == 0 ? _plains : _desert;
    }

    private sealed class ConstantBiomeResolver : BiomeResolver
    {
        private readonly Holder<Biome> _biome;

        public ConstantBiomeResolver(Holder<Biome> biome) => _biome = biome;

        public Holder<Biome> GetNoiseBiome(int quartX, int quartY, int quartZ) => _biome;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
