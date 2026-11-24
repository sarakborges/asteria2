using Asteria.World.LevelGen.Materials;
using Asteria.Content.Blocks;
using Asteria.Content.Biomes;
using Asteria.Content.Chunks;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.Materials.Rules;
using Asteria.World.LevelGen.Materials.Conditions;

internal static class NoiseBasedChunkGeneratorChecks
{
    public static void Run()
    {
        BindVanillaTerrainHeightmapTags();
        try
        {
            CheckGlobalFluidPickerBoundaries();
            CheckPositiveDensityUsesDefaultBlock();
            CheckNegativeDensityMaterializesLavaWaterAndAir();
            CheckChunkCoordinateAndHeightmapMaterialization();
            CheckMaterialRuleSurfacePass();
            CheckBandlandsMaterialRuleUsesSeededClayBands();
            CheckMultiNoiseBiomeSourceFillsChunkPalettes();
            CheckPreliminarySurfaceCondition();
        }
        finally
        {
            RestoreEmptyBlockTags();
        }
    }

    private static void BindVanillaTerrainHeightmapTags()
    {
        Holder<Block>.Reference stone = Blocks.STONE.BuiltInRegistryHolder ??
            throw new InvalidOperationException("Built-in stone must have a registry holder.");

        KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>[] definitions =
        [
            new(BlockTags.BlocksMotionInHeightmap, new Holder<Block>[] { stone }),
            new(BlockTags.BlocksMotionInHeightmapNoLeaves, new Holder<Block>[] { stone }),
            new(BlockTags.Leaves, Array.Empty<Holder<Block>>()),
            new(BlockTags.FallDamageResetting, Array.Empty<Holder<Block>>()),
            new(BlockTags.CausesSuffocation, Array.Empty<Holder<Block>>())
        ];

        BuiltInRegistries.Block.PrepareTagReload(definitions).Apply();
    }

    private static void RestoreEmptyBlockTags()
    {
        KeyValuePair<TagKey<Block>, IEnumerable<Holder<Block>>>[] definitions =
        [
            new(BlockTags.BlocksMotionInHeightmap, Array.Empty<Holder<Block>>()),
            new(BlockTags.BlocksMotionInHeightmapNoLeaves, Array.Empty<Holder<Block>>()),
            new(BlockTags.Leaves, Array.Empty<Holder<Block>>()),
            new(BlockTags.FallDamageResetting, Array.Empty<Holder<Block>>()),
            new(BlockTags.CausesSuffocation, Array.Empty<Holder<Block>>())
        ];

        BuiltInRegistries.Block.PrepareTagReload(definitions).Apply();
    }

    private static void CheckGlobalFluidPickerBoundaries()
    {
        NoiseGeneratorSettings settings = CreateSettings(new ConstantFunction(-1.0f), 63);
        Aquifer.FluidPicker picker = NoiseBasedChunkGenerator.CreateFluidPicker(settings);

        Require(ReferenceEquals(picker(0, -55, 0).At(-55), Blocks.LAVA.DefaultState),
            "The global fluid picker must return lava below Y=-54.");
        Require(ReferenceEquals(picker(0, -54, 0).At(-54), Blocks.WATER.DefaultState),
            "The global fluid picker must switch to the configured fluid at Y=-54.");
        Require(
            ReferenceEquals(picker(0, 62, 0).At(62), Blocks.WATER.DefaultState) &&
            ReferenceEquals(picker(0, 63, 0).At(63), Blocks.AIR.DefaultState),
            "The default fluid must fill strictly below sea level and be air at the exact sea-level boundary.");

        Aquifer.FluidPicker lowerSeaPicker =
            NoiseBasedChunkGenerator.CreateFluidPicker(CreateSettings(new ConstantFunction(-1.0f), -60));
        Require(
            ReferenceEquals(lowerSeaPicker(0, -61, 0).At(-61), Blocks.LAVA.DefaultState) &&
            ReferenceEquals(lowerSeaPicker(0, -60, 0).At(-60), Blocks.AIR.DefaultState),
            "When sea level is below -54, the lava cutoff must follow the lower sea level.");
    }

    private static void CheckPositiveDensityUsesDefaultBlock()
    {
        NoiseGeneratorSettings settings = CreateSettings(new ConstantFunction(0.25f), 0);
        ChunkPos chunkPos = new(0, 0);
        ProtoChunk chunk = CreateChunk(chunkPos, -16, 48);
        new NoiseBasedChunkGenerator(settings, CreateRandomState(settings)).FillFromNoise(chunk);

        Require(ReferenceEquals(chunk.GetBlockState(new BlockPos(2, 0, 3)), Blocks.STONE.DefaultState),
            "Positive final_density must materialize the configured default solid block.");
        Require(
            chunk.GetHeight(Heightmap.Types.WORLD_SURFACE_WG, 2, 3) == 31 &&
            chunk.GetHeight(Heightmap.Types.OCEAN_FLOOR_WG, 2, 3) == 31,
            "ProtoChunk writes must maintain both world-generation heightmaps.");
        Require(
            chunk.GetHeightmaps().Count == 6 &&
            chunk.GetHeight(Heightmap.Types.WORLD_SURFACE, 2, 3) == 31 &&
            chunk.GetHeight(Heightmap.Types.OCEAN_FLOOR, 2, 3) == 31 &&
            chunk.GetHeight(Heightmap.Types.MOTION_BLOCKING, 2, 3) == 31 &&
            chunk.GetHeight(Heightmap.Types.MOTION_BLOCKING_NO_LEAVES, 2, 3) == 31,
            "After density filling, all four final heightmaps must be primed alongside the two world-generation maps.");
    }

    private static void CheckNegativeDensityMaterializesLavaWaterAndAir()
    {
        NoiseGeneratorSettings settings = CreateSettings(new ConstantFunction(-1.0f), 0);
        ProtoChunk chunk = CreateChunk(new ChunkPos(0, 0), -64, 96);
        new NoiseBasedChunkGenerator(settings, CreateRandomState(settings)).FillFromNoise(chunk);

        Require(ReferenceEquals(chunk.GetBlockState(new BlockPos(1, -55, 2)), Blocks.LAVA.DefaultState),
            "Negative final_density below the lava cutoff must materialize lava.");
        Require(
            ReferenceEquals(chunk.GetBlockState(new BlockPos(1, -54, 2)), Blocks.WATER.DefaultState) &&
            ReferenceEquals(chunk.GetBlockState(new BlockPos(1, -1, 2)), Blocks.WATER.DefaultState),
            "Negative final_density between lava cutoff and sea level must materialize the configured fluid.");
        Require(ReferenceEquals(chunk.GetBlockState(new BlockPos(1, 0, 2)), Blocks.AIR.DefaultState),
            "A fluid status at its exact sea level must leave the block as air.");
        Require(
            chunk.GetFluidState(new BlockPos(1, -55, 2)).IsSource() &&
            chunk.GetFluidState(new BlockPos(1, -1, 2)).IsSource(),
            "Materialized lava and water blocks must expose their registered source fluid states.");
        Require(
            chunk.GetHeight(Heightmap.Types.WORLD_SURFACE_WG, 1, 2) == -1 &&
            chunk.GetHeight(Heightmap.Types.OCEAN_FLOOR_WG, 1, 2) == -65,
            "World-surface heightmaps must count the top fluid while ocean-floor maps ignore non-motion-blocking fluid.");
    }

    private static void CheckChunkCoordinateAndHeightmapMaterialization()
    {
        NoiseGeneratorSettings settings = CreateSettings(new ConstantFunction(1.0f), 0);
        ChunkPos chunkPos = new(2, -3);
        ProtoChunk chunk = CreateChunk(chunkPos, -16, 48);
        new NoiseBasedChunkGenerator(settings, CreateRandomState(settings)).FillFromNoise(chunk);

        int worldX = chunkPos.GetMinBlockX() + 7;
        int worldZ = chunkPos.GetMinBlockZ() + 11;
        Require(ReferenceEquals(chunk.GetBlockState(new BlockPos(worldX, 17, worldZ)), Blocks.STONE.DefaultState),
            "Density volume indices must map to the target chunk's world coordinates.");
        Require(chunk.GetHeight(Heightmap.Types.WORLD_SURFACE_WG, worldX, worldZ) == 31,
            "Chunk heightmap lookup must accept world-space X/Z for non-origin chunks.");
    }

    private static void CheckMaterialRuleSurfacePass()
    {
        NoiseGeneratorSettings settings = CreateSettings(
            new ConstantFunction(0.25f),
            0,
            new BlockRule(Blocks.DIRT.DefaultState));
        var randomState = CreateRandomState(settings);
        var generator = new NoiseBasedChunkGenerator(settings, randomState);
        ProtoChunk chunk = CreateChunk(new ChunkPos(0, 0), -16, 48);

        generator.BuildTerrain(
            chunk,
            _ => Holder<Biome>.Direct(Biomes.PLAINS));

        Require(
            ReferenceEquals(chunk.GetBlockState(new BlockPos(7, 0, 9)), Blocks.DIRT.DefaultState),
            "BuildTerrain must apply the configured material rule after density/aquifer filling.");
        Require(
            ReferenceEquals(randomState.GetOrCreateMaterialSystem(settings), randomState.GetOrCreateMaterialSystem(settings)),
            "A RandomState must retain one MaterialSystem instance for its world-generation settings.");
    }

    private static void CheckPreliminarySurfaceCondition()
    {
        MaterialRule rule = new ConditionRule(
            AbovePreliminarySurfaceCondition.INSTANCE,
            new BlockRule(Blocks.DIRT.DefaultState));
        NoiseGeneratorSettings settings = CreateSettings(new ConstantFunction(0.25f), 0, rule);
        var generator = new NoiseBasedChunkGenerator(settings, CreateRandomState(settings));
        ProtoChunk chunk = CreateChunk(new ChunkPos(0, 0), -16, 48);

        generator.BuildTerrain(
            chunk,
            _ => Holder<Biome>.Direct(Biomes.PLAINS));

        Require(
            ReferenceEquals(chunk.GetBlockState(new BlockPos(5, 0, 6)), Blocks.DIRT.DefaultState),
            "The material context must evaluate preliminary-surface conditions using the chunk-sampled density volume.");
        Require(
            ReferenceEquals(chunk.GetBlockState(new BlockPos(5, -16, 6)), Blocks.STONE.DefaultState),
            "Blocks below the preliminary-surface threshold must retain their original material.");
    }

    private static void CheckBandlandsMaterialRuleUsesSeededClayBands()
    {
        NoiseGeneratorSettings settings = CreateSettings(
            new ConstantFunction(0.25f),
            seaLevel: 0,
            materialRule: BandlandsRule.INSTANCE);
        RandomState randomState = CreateRandomState(settings);
        var generator = new NoiseBasedChunkGenerator(settings, randomState);
        ProtoChunk chunk = CreateChunk(new ChunkPos(0, 0), -16, 48);

        generator.BuildTerrain(
            chunk,
            _ => Holder<Biome>.Direct(Biomes.PLAINS));

        MaterialSystem materialSystem = randomState.GetOrCreateMaterialSystem(settings);
        int x = 7;
        int z = 9;
        BlockState expected = materialSystem.GetBand(x, 0, z);
        Require(
            ReferenceEquals(chunk.GetBlockState(new BlockPos(x, 0, z)), expected),
            "BandlandsRule must use the seeded material system's clay-band state at the current world position.");
        Require(
            ReferenceEquals(materialSystem.GetBand(x, 0, z), materialSystem.GetBand(x, 192, z)) &&
            ReferenceEquals(materialSystem.GetBand(x, 0, z), materialSystem.GetBand(x, 192, z)),
            "Clay bands must repeat with the vanilla 192-block vertical period.");

        BlockState[] generatedBands = MaterialSystem.GenerateClayBands(
            new XoroshiroRandomSource(132456789L)
                .ForkPositional()
                .FromHashOf("minecraft:clay_bands"));
        Require(
            generatedBands.Length == 192 &&
            generatedBands.Any(state => ReferenceEquals(state, Blocks.YELLOW_TERRACOTTA.DefaultState)) &&
            generatedBands.Any(state => ReferenceEquals(state, Blocks.BROWN_TERRACOTTA.DefaultState)) &&
            generatedBands.Any(state => ReferenceEquals(state, Blocks.RED_TERRACOTTA.DefaultState)) &&
            generatedBands.Any(state => ReferenceEquals(state, Blocks.WHITE_TERRACOTTA.DefaultState)) &&
            generatedBands.Any(state => ReferenceEquals(state, Blocks.LIGHT_GRAY_TERRACOTTA.DefaultState)),
            "Clay-band generation must create the five colored strata alongside orange and base terracotta.");
    }

    private static void CheckMultiNoiseBiomeSourceFillsChunkPalettes()
    {
        Climate.ParameterPoint plainsPoint = Climate.Parameters(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f);
        Climate.ParameterPoint desertPoint = Climate.Parameters(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.0f);
        var parameters = new Climate.ParameterList<Holder<Biome>>(
        [
            new Pair<Climate.ParameterPoint, Holder<Biome>>(plainsPoint, Biomes.PLAINS.BuiltInRegistryHolder),
            new Pair<Climate.ParameterPoint, Holder<Biome>>(desertPoint, Biomes.DESERT.BuiltInRegistryHolder)
        ]);
        var biomeSource = MultiNoiseBiomeSource.CreateFromList(parameters);

        Require(
            ReferenceEquals(
                biomeSource.GetNoiseBiome(Climate.Target(0, 0, 0, 0, 0, 0)),
                Biomes.PLAINS.BuiltInRegistryHolder) &&
            ReferenceEquals(
                biomeSource.GetNoiseBiome(Climate.Target(1, 1, 1, 1, 1, 1)),
                Biomes.DESERT.BuiltInRegistryHolder),
            "MultiNoiseBiomeSource must resolve the nearest seven-dimensional climate parameter entry.");
        Require(
            biomeSource.PossibleBiomes.Count == 2,
            "MultiNoiseBiomeSource must expose its distinct possible biome holders.");

        NoiseGeneratorSettings settings = CreateSettings(
            new ConstantFunction(0.25f),
            seaLevel: 0,
            climateTemperature: 1.0f);
        var generator = new NoiseBasedChunkGenerator(settings, CreateRandomState(settings));
        ProtoChunk chunk = CreateChunk(new ChunkPos(0, 0), -16, 48);

        generator.BuildTerrain(chunk, biomeSource);

        foreach (ChunkSection section in chunk.GetSections())
        {
            for (int x = 0; x < ChunkSection.BiomeContainerWidth; x++)
            for (int y = 0; y < ChunkSection.BiomeContainerWidth; y++)
            for (int z = 0; z < ChunkSection.BiomeContainerWidth; z++)
            {
                Require(
                    ReferenceEquals(section.GetNoiseBiome(x, y, z), Biomes.DESERT.BuiltInRegistryHolder),
                    "BuildTerrain must fill every quart biome cell from the chunk-bound climate sampler.");
            }
        }
    }

    private static ProtoChunk CreateChunk(ChunkPos position, int minY, int height) =>
        new(position, LevelHeightAccessor.Create(minY, height), PalettedContainerFactory.CreateBuiltIn());

    private static NoiseGeneratorSettings CreateSettings(
        DensityFunction finalDensity,
        int seaLevel,
        MaterialRule? materialRule = null,
        float climateTemperature = 0.0f)
    {
        DensityFunction climate = new ConstantFunction(climateTemperature);
        var router = new NoiseRouter(
            climate, climate, climate, climate,
            climate, climate, climate, finalDensity);

        return new NoiseGeneratorSettings(
            NoiseSettings.Create(-64, 96),
            Blocks.STONE.DefaultState,
            Blocks.WATER.DefaultState,
            router,
            Holder<MaterialRule>.Direct(materialRule ?? new BlockRule(Blocks.STONE.DefaultState)),
            Array.Empty<SpawnTargetPoint>(),
            seaLevel,
            Aquifers: null,
            UseLegacyRandomSource: false,
            DebugFunctions: NoiseGeneratorDebugFunctions.EMPTY);
    }

    private static RandomState CreateRandomState(NoiseGeneratorSettings settings) =>
        RandomState.Create(
            BuiltInRegistries.Noise,
            132456789L,
            settings.UseLegacyRandomSource,
            settings.NoiseRouter);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
