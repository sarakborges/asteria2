using Asteria.Content.Biomes;
using Asteria.Content.Chunks;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Content.Identifiers;
using Asteria.World.Attributes;
using Asteria.World.Attributes.Modifiers;

internal static class BiomeChecks
{
    public static void Run()
    {
        CheckBuiltInBiomeRegistry();
        CheckEnvironmentAttributeMap();
        CheckSpecialEffects();
        CheckTemperatureModifiers();
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

    private static void CheckEnvironmentAttributeMap()
    {
        var registry = new Registry<Biome>(
            ResourceLocation.Parse("asteria:biome_attribute_test"),
            allowIntrusiveHolders: true);

        EnvironmentAttributeMap.Builder builder = EnvironmentAttributeMap.CreateBuilder()
            .Modify(EnvironmentAttributes.FOG_END_DISTANCE, FloatModifier.MULTIPLY, 0.5F)
            .Set(EnvironmentAttributes.WATER_EVAPORATES, true);
        EnvironmentAttributeMap attributes = builder.Build();

        // Mutating the builder after Build must not affect the biome's attribute snapshot.
        builder.Set(EnvironmentAttributes.WATER_EVAPORATES, false);

        Biome biome = new(
            new Biome.ClimateSettings(true, 0.6F, Biome.TemperatureModifier.NONE, 0.7F),
            attributes,
            new BiomeSpecialEffects.Builder().WaterColor(4159204).Build(),
            registry);

        RegistryKey<Biome> key = RegistryKey<Biome>.Create(
            registry.Key,
            ResourceLocation.Parse("asteria:attribute_fixture"));
        registry.Register(key, biome, RegistrationInfo.BuiltIn);
        registry.BindAllTagsToEmpty();
        registry.Freeze();

        Require(registry.IsFrozen &&
                ReferenceEquals(registry.GetValue(key), biome) &&
                ReferenceEquals(registry.GetValue(key)?.BuiltInRegistryHolder, biome.BuiltInRegistryHolder),
            "A biome carrying environment attributes must retain its normal intrusive registry-holder lifecycle.");
        Require(ReferenceEquals(biome.Attributes, attributes) &&
                ReferenceEquals(biome.GetAttributes(), attributes),
            "Biome must expose the exact immutable EnvironmentAttributeMap supplied at construction.");
        Require(biome.GetAttributes().ApplyModifier(EnvironmentAttributes.FOG_END_DISTANCE, 1024.0F) == 512.0F,
            "Biome attributes must apply their typed modifier to the lower-priority environment value.");
        Require(biome.GetAttributes().ApplyModifier(EnvironmentAttributes.WATER_EVAPORATES, false),
            "Biome attributes must preserve boolean override values after the builder has changed.");
        Require(!biome.GetAttributes().ApplyModifier(EnvironmentAttributes.FAST_LAVA, false),
            "A biome without an entry for an attribute must leave the lower-priority value unchanged.");

        Require(ReferenceEquals(Biomes.PLAINS.GetAttributes(), EnvironmentAttributeMap.Empty) &&
                ReferenceEquals(Biomes.DESERT.GetAttributes(), EnvironmentAttributeMap.Empty),
            "Built-in biomes without explicit environment data must use the shared empty attribute map.");
        Require(BuiltInRegistries.Biome.Count == 2,
            "The isolated biome test registry must not add fixture content to the production biome registry.");
    }

    private static void CheckSpecialEffects()
    {
        var missingWaterColor = new BiomeSpecialEffects.Builder();
        RequireThrows<InvalidOperationException>(() => missingWaterColor.Build(),
            "BiomeSpecialEffects.Builder must reject a missing water color.");

        BiomeSpecialEffects effects = new BiomeSpecialEffects.Builder()
            .WaterColor(123456).FoliageColorOverride(0x00112233)
            .DryFoliageColorOverride(0x00445566).GrassColorOverride(0x00778899)
            .GrassColorModifier(GrassColorModifier.DARK_FOREST).Build();
        Require(effects.WaterColor == 123456 && effects.GetFoliageColor(1) == 0x00112233 &&
                effects.GetDryFoliageColor(2) == 0x00445566,
            "Biome special effects must retain water, foliage and dry-foliage colors.");
        Require(effects.GetGrassColor(4, 8, 3) == unchecked((int)0xFF4F5E51),
            "The explicit grass override must be selected as the base color before the dark-forest modifier.");

        BiomeSpecialEffects darkForest = new BiomeSpecialEffects.Builder().WaterColor(1)
            .GrassColorModifier(GrassColorModifier.DARK_FOREST).Build();
        Require(darkForest.GetGrassColor(0, 0, unchecked((int)0xFF55AA33)) == unchecked((int)0xFF3E6F1E),
            "Dark-forest grass modification must preserve reference integer arithmetic and opaque alpha.");

        BiomeSpecialEffects swamp = new BiomeSpecialEffects.Builder().WaterColor(1)
            .GrassColorModifier(GrassColorModifier.SWAMP).Build();
        Require(swamp.GetGrassColor(100, 200, 0) == -11766212,
            "Swamp grass color must use the dark branch for the fixed-seed noise sample.");
        Require(swamp.GetGrassColor(0, 0, 0) == -9801671,
            "Swamp grass color must use the light branch when the noise sample is zero.");
        Require(BiomeSpecialEffects.GetSerializedName(GrassColorModifier.DARK_FOREST) == "dark_forest" &&
                BiomeSpecialEffects.GetSerializedName(GrassColorModifier.SWAMP) == "swamp" &&
                BiomeSpecialEffects.GetSerializedName(GrassColorModifier.NONE) == "none",
            "Grass color modifier serialized names must match the reference.");
        Require(Biomes.PLAINS.SpecialEffects.WaterColor == 4159204 && Biomes.DESERT.SpecialEffects.WaterColor == 4159204,
            "Base overworld biomes must use the reference normal water color.");
    }
    private static void CheckTemperatureModifiers()
    {
        BlockPos origin = new(0, 0, 0);
        BlockPos warmPatch = new(1, 0, 1);
        BlockPos coldPatch = new(100, 0, 200);
        Require(Biome.TemperatureModifier.NONE.ModifyTemperature(origin, 0.8F) == 0.8F,
            "The NONE temperature modifier must preserve the biome base temperature.");
        Require(Biome.TemperatureModifier.FROZEN.ModifyTemperature(origin, 0.8F) == 0.2F,
            "The frozen modifier must produce the fixed cold patch at the origin.");
        Require(Biome.TemperatureModifier.FROZEN.ModifyTemperature(warmPatch, 0.8F) == 0.8F,
            "The frozen modifier must preserve base temperature outside a cold patch.");
        Require(Biome.TemperatureModifier.FROZEN.ModifyTemperature(coldPatch, 0.8F) == 0.2F,
            "The frozen modifier must reproduce the fixed-seed large- and small-scale noise combination.");

        Require(Biomes.PLAINS.GetHeightAdjustedTemperature(new BlockPos(8, 80, 16), 63) == 0.8F,
            "Height adjustment must not apply at sea level plus seventeen blocks.");
        Require(Math.Abs(Biomes.PLAINS.GetHeightAdjustedTemperature(new BlockPos(8, 81, 16), 63) - 0.80110264F) < 0.000001F,
            "Height adjustment must use the fixed-seed temperature noise above the snow level.");
        Require(Biomes.DESERT.GetPrecipitationAt(origin, 63) == Biome.Precipitation.NONE,
            "A biome without precipitation must never return rain or snow.");
        Require(Biomes.PLAINS.GetPrecipitationAt(origin, 63) == Biome.Precipitation.RAIN,
            "A warm precipitation-enabled biome must return rain.");
    }
    private static void CheckBiomeSectionPalette()
    {
        PalettedContainerFactory biomeFactory = PalettedContainerFactory.CreateBuiltIn();
        Require(
            biomeFactory.BiomeStrategy.BitsPerAxis == 2 &&
            biomeFactory.Strategy.EntryCount == ChunkSection.BiomeCount &&
            biomeFactory.Strategy.EntryCount == 64,
            "The biome palette must use a 4x4x4 quart grid with 64 entries.");

        ChunkSection section = new ChunkSection(PalettedContainerFactory.CreateBuiltIn());
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

    private static void RequireThrows<TException>(Action action, string message) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException(message);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
