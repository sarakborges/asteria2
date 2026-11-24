using Asteria.World.LevelGen.Materials;
using Asteria.Content.Blocks;
using Asteria.Content.Fluids;
using Asteria.Content.Biomes;
using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World;
using Asteria.World.LevelGen;
using Asteria.World.Level;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.Materials.Rules;
using Asteria.World.LevelGen.Materials.Conditions;

internal static class NoiseBasedChunkGeneratorChecks
{
    public static void Run()
    {
        CheckGlobalFluidPickerBoundaries();
        CheckPositiveDensityUsesDefaultBlock();
        CheckNegativeDensityMaterializesLavaWaterAndAir();
        CheckChunkCoordinateAndHeightmapMaterialization();
        CheckMaterialRuleSurfacePass();
        CheckCarversUseAquiferAndRespectMaskBounds();
        CheckBandlandsMaterialRuleUsesSeededClayBands();
        CheckOreVeinMaterialRuleEvaluation();
        CheckErodedBadlandsExtension();
        CheckFrozenOceanExtension();
        CheckMultiNoiseBiomeSourceFillsChunkPalettes();
        CheckBuiltInOverworldGeneratorFactory();
        CheckOverworldWorldChunkCache();
        CheckDefaultOverworldBuildHeight();
        CheckPreliminarySurfaceCondition();
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

    private static void CheckBuiltInOverworldGeneratorFactory()
    {
        const long seed = 0x4A535F323633L;
        NoiseBasedChunkGenerator generator = NoiseBasedChunkGenerator.CreateOverworld(seed);
        NoiseGeneratorSettings expectedSettings =
            BuiltInRegistries.NoiseGeneratorSettings.GetValueOrThrow(
                RegistryKey<NoiseGeneratorSettings>.Create(
                    BuiltInRegistries.NoiseGeneratorSettings.Key,
                    ResourceLocation.FromNamespaceAndPath("minecraft", "overworld")));

        Require(
            ReferenceEquals(generator.Settings, expectedSettings) &&
            generator.Seed == seed &&
            generator.GetMinY() == -64 &&
            generator.GetGenDepth() == 384 &&
            generator.GetSeaLevel() == 63,
            "CreateOverworld must compose the registered vanilla settings and the requested world seed.");

        ProtoChunk chunk = CreateChunk(new ChunkPos(0, 0), -64, 96);
        generator.BuildTerrain(chunk);

        Require(
            ReferenceEquals(chunk.GetPersistedStatus(), ChunkStatus.TERRAIN) &&
            chunk.GetHighestGeneratedStatus().IsOrAfter(ChunkStatus.BIOMES),
            "BuildTerrain must advance the proto chunk through BIOMES and TERRAIN after completing those stages.");

        Holder<Biome> generatedBiome = chunk.GetNoiseBiome(0, QuartPos.FromBlock(-64), 0);
        Require(
            generatedBiome.IsBound,
            "ProtoChunk.GetNoiseBiome must become available after BuildTerrain completes the BIOMES stage.");

        int solidSamples = 0;
        for (int y = -64; y < 32; y += 4)
        for (int z = 0; z < 16; z += 2)
        for (int x = 0; x < 16; x += 2)
        {
            BlockState state = chunk.GetBlockState(new BlockPos(x, y, z));
            if (!state.IsAir && state.GetFluidState().IsEmpty())
                solidSamples++;
        }

        Require(
            solidSamples > 0,
            "The registered Overworld settings and seed must generate solid terrain through the default BuildTerrain path.");

        int biomeCells = 0;
        foreach (ChunkSection section in chunk.GetSections())
        for (int quartY = 0; quartY < ChunkSection.BiomeContainerWidth; quartY++)
        for (int quartZ = 0; quartZ < ChunkSection.BiomeContainerWidth; quartZ++)
        for (int quartX = 0; quartX < ChunkSection.BiomeContainerWidth; quartX++)
        {
            Holder<Biome> biome = section.GetNoiseBiome(quartX, quartY, quartZ);
            Require(
                biome.IsBound,
                "The built-in Overworld generator must fill every biome palette cell with a registered biome holder.");
            biomeCells++;
        }

        Require(
            biomeCells == chunk.GetSections().Count() * 64,
            "The built-in Overworld generator must populate the complete quart-resolution biome palette for every generated section.");
    }

    private static void CheckOverworldWorldChunkCache()
    {
        const long seed = 0x4F5652574F524C44L;
        var world = new OverworldWorld(
            seed,
            LevelHeightAccessor.Create(-64, 96));

        Require(
            world.Seed == seed &&
            world.MinY == -64 &&
            world.Height == 96 &&
            world.LoadedChunkCount == 0,
            "OverworldWorld must retain its seed and configured build height and start with an empty chunk cache.");

        ChunkPos position = ChunkPos.Zero;
        Require(
            !world.TryGetChunk(position, out _),
            "TryGetChunk must not synthesize a chunk before it has been requested.");

        var results = new ProtoChunk[8];
        Parallel.For(0, results.Length, index =>
            results[index] = world.GetChunk(position));

        ProtoChunk generated = results[0];
        Require(
            results.All(chunk => ReferenceEquals(chunk, generated)) &&
            world.LoadedChunkCount == 1,
            "Concurrent requests for one chunk must serialize generation and return the single cached instance.");

        IReadOnlyList<ProtoChunk> initialSnapshot = world.GetLoadedChunks();
        Require(
            initialSnapshot.Count == 1 &&
            ReferenceEquals(initialSnapshot[0], generated),
            "GetLoadedChunks must return a read-only snapshot containing the cached chunk.");

        Require(
            generated.GetPersistedStatus().IsOrAfter(ChunkStatus.TERRAIN) &&
            generated.GetNoiseBiome(0, QuartPos.FromBlock(-64), 0).IsBound,
            "Chunks created by OverworldWorld must have terrain and registered biome data available.");

        Require(
            world.TryGetChunk(position, out ProtoChunk? cached) &&
            ReferenceEquals(cached, generated),
            "TryGetChunk must return the cached chunk without regenerating it.");

        BlockPos localPosition = new(3, -1, 4);
        Require(
            ReferenceEquals(
                world.GetBlockState(localPosition),
                generated.GetBlockState(localPosition)) &&
            ReferenceEquals(
                world.GetFluidState(localPosition),
                generated.GetFluidState(localPosition)) &&
            world.LoadedChunkCount == 1,
            "World block and fluid queries must delegate to the already cached chunk.");

        BlockState previousState = world.GetBlockState(localPosition);
        BlockState replacementState = ReferenceEquals(previousState, Blocks.DIRT.DefaultState)
            ? Blocks.STONE.DefaultState
            : Blocks.DIRT.DefaultState;
        generated.TryMarkSaved();
        BlockState returnedState = world.SetBlockState(localPosition, replacementState);
        Require(
            ReferenceEquals(returnedState, previousState) &&
            ReferenceEquals(world.GetBlockState(localPosition), replacementState) &&
            generated.IsUnsaved(),
            "World block writes must return the previous state, update the cached ProtoChunk and mark changed data unsaved.");

        Require(
            ReferenceEquals(
                world.SetBlockState(new BlockPos(0, 32, 0), Blocks.STONE.DefaultState),
                Blocks.VOID_AIR.DefaultState) &&
            world.LoadedChunkCount == 1,
            "World block writes outside build height must return VOID_AIR without generating or mutating chunks.");

        Holder<Biome> worldBiome = world.GetBiome(localPosition);
        Holder<Biome> chunkBiome = generated.GetNoiseBiome(
            QuartPos.FromBlock(localPosition.GetX()),
            QuartPos.FromBlock(localPosition.GetY()),
            QuartPos.FromBlock(localPosition.GetZ()));
        Require(
            worldBiome.IsBound &&
            ReferenceEquals(worldBiome, chunkBiome) &&
            world.LoadedChunkCount == 1,
            "World biome queries must return the registered holder from the cached chunk's quart-resolution palette.");

        Require(
            world.GetHeight(Heightmap.Types.WORLD_SURFACE, 3, 4) ==
                generated.GetHeight(Heightmap.Types.WORLD_SURFACE, 3, 4),
            "World height queries must return the cached chunk's world-surface heightmap value.");

        ChunkPos neighbor = new(1, 0);
        BlockPos neighborPosition = new(neighbor.GetMinBlockX() + 3, -1, 4);
        BlockState neighborState = world.GetBlockState(neighborPosition);
        Require(
            world.TryGetChunk(neighbor, out ProtoChunk? adjacent) &&
            adjacent is not null &&
            adjacent.GetPos() == neighbor &&
            ReferenceEquals(neighborState, adjacent.GetBlockState(neighborPosition)) &&
            world.LoadedChunkCount == 2,
            "A block query in another chunk must generate and cache only that target chunk.");

        IReadOnlyList<ProtoChunk> expandedSnapshot = world.GetLoadedChunks();
        Require(
            initialSnapshot.Count == 1 &&
            expandedSnapshot.Count == 2 &&
            expandedSnapshot[0].GetPos() == ChunkPos.Zero &&
            expandedSnapshot[1].GetPos() == neighbor,
            "Loaded chunk snapshots must remain stable and sort chunks by X then Z position.");

        BlockPos aboveWorld = new(0, 32, 0);
        Require(
            ReferenceEquals(world.GetBlockState(aboveWorld), Blocks.VOID_AIR.DefaultState) &&
            ReferenceEquals(world.GetFluidState(aboveWorld), Fluids.EMPTY.DefaultFluidState) &&
            world.LoadedChunkCount == 2,
            "Queries outside the world's vertical bounds must return void air/empty fluid without loading another chunk.");

        Require(
            world.UnloadChunk(position) &&
            !world.TryGetChunk(position, out _) &&
            world.LoadedChunkCount == 1 &&
            world.GetLoadedChunks().Count == 1 &&
            !world.UnloadChunk(position),
            "UnloadChunk must remove exactly the requested cache entry and report absent entries.");

        bool rejectedInvalidPosition = false;
        try
        {
            world.GetChunk(new ChunkPos(ChunkPyramid.MaxChunkCoordinateValue + 1, 0));
        }
        catch (ArgumentOutOfRangeException)
        {
            rejectedInvalidPosition = true;
        }

        Require(
            rejectedInvalidPosition &&
            world.LoadedChunkCount == 1,
            "OverworldWorld must reject chunk coordinates outside the supported generation bounds without changing its cache.");
    }

    private static void CheckDefaultOverworldBuildHeight()
    {
        const long seed = 0x4F5652574F524C44L;
        var world = new OverworldWorld(seed);
        Require(
            world.MinY == -64 &&
            world.Height == 384 &&
            world.GetMinY() == -64 &&
            world.MinY + world.Height - 1 == 319,
            "The default Overworld must use the complete Minecraft 26.3 build-height interval [-64, 319].");

        ProtoChunk chunk = world.GetChunk(ChunkPos.Zero);
        Require(
            chunk.GetSections().Length == 24 &&
            chunk.GetPersistedStatus().IsOrAfter(ChunkStatus.TERRAIN),
            "Default Overworld generation must materialize all 24 vertical sections through TERRAIN.");

        BlockPos bottom = new(3, world.GetMinY(), 4);
        BlockPos top = new(3, world.MinY + world.Height - 1, 4);
        Require(
            ReferenceEquals(world.GetBlockState(bottom), chunk.GetBlockState(bottom)) &&
            ReferenceEquals(world.GetBlockState(top), chunk.GetBlockState(top)),
            "World block queries must resolve both vertical extremes from a full-height generated chunk.");

        Holder<Biome> bottomBiome = world.GetBiome(bottom);
        Holder<Biome> topBiome = world.GetBiome(top);
        Require(
            bottomBiome.IsBound &&
            topBiome.IsBound &&
            ReferenceEquals(
                bottomBiome,
                chunk.GetNoiseBiome(
                    QuartPos.FromBlock(bottom.GetX()),
                    QuartPos.FromBlock(bottom.GetY()),
                    QuartPos.FromBlock(bottom.GetZ()))) &&
            ReferenceEquals(
                topBiome,
                chunk.GetNoiseBiome(
                    QuartPos.FromBlock(top.GetX()),
                    QuartPos.FromBlock(top.GetY()),
                    QuartPos.FromBlock(top.GetZ()))),
            "Full-height world biome queries must resolve the first and last quart cells without clamping errors.");

        Require(
            world.LoadedChunkCount == 1 &&
            world.GetLoadedChunks().Count == 1,
            "The full-height smoke test must retain exactly its one generated chunk.");
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

    private static void CheckCarversUseAquiferAndRespectMaskBounds()
    {
        Require(
            Blocks.BEDROCK.DefaultState.Is(BlockTags.Uncarvable) &&
            !Blocks.STONE.DefaultState.Is(BlockTags.Uncarvable),
            "The built-in uncarvable tag must include bedrock without marking ordinary stone as uncarvable.");

        const int minY = -64;
        const int height = 96;
        const int protectedMaxY = minY + height - 1 - 7;

        NoiseGeneratorSettings settings = CreateSettings(
            new ConstantFunction(0.25f),
            seaLevel: -100,
            materialRule: new BlockRule(Blocks.STONE.DefaultState),
            noiseHeight: height);
        var randomState = CreateRandomState(settings);
        var generator = new NoiseBasedChunkGenerator(settings, randomState);
        ProtoChunk chunk = CreateChunk(ChunkPos.Zero, minY, height);

        generator.BuildTerrain(
            chunk,
            _ => Holder<Biome>.Direct(Biomes.PLAINS));

        int carvedAirCount = 0;
        for (int y = minY; y <= chunk.GetMaxY(); y++)
        for (int x = 0; x < 16; x++)
        for (int z = 0; z < 16; z++)
        {
            BlockState state = chunk.GetBlockState(new BlockPos(x, y, z));
            if (state.IsAir)
            {
                Require(
                    y >= minY + 2 && y <= protectedMaxY,
                    "Carving must remain inside the vanilla vertical mask bounds and ellipsoid lower-exclusive edge.");
                carvedAirCount++;
            }

            if (y <= minY + 1 || y > protectedMaxY)
            {
                Require(
                    ReferenceEquals(state, Blocks.STONE.DefaultState),
                    "Carving must preserve the lower mask boundary and the seven protected top blocks.");
            }
        }

        Require(
            carvedAirCount > 0,
            "BuildTerrain must run biome carvers and fill the carved positions with the aquifer's air result.");
        Require(
            ReferenceEquals(chunk.GetPersistedStatus(), ChunkStatus.TERRAIN),
            "The chunk may reach TERRAIN only after the carving pass and mask application complete.");
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

    private static void CheckOreVeinMaterialRuleEvaluation()
    {
        static (ProtoChunk Chunk, RandomState RandomState) BuildChunkForRule(OreVeinRule rule)
        {
            NoiseGeneratorSettings settings = CreateSettings(
                new ConstantFunction(0.25F),
                seaLevel: 0,
                materialRule: rule);
            RandomState randomState = CreateRandomState(settings);
            var generator = new NoiseBasedChunkGenerator(settings, randomState);
            ProtoChunk chunk = CreateChunk(new ChunkPos(0, 0), -16, 48);
            generator.BuildTerrain(
                chunk,
                _ => Holder<Biome>.Direct(Biomes.PLAINS));
            return (chunk, randomState);
        }

        static void RequireColumnState(ProtoChunk chunk, Block block, string message)
        {
            for (int x = 0; x < 16; x++)
            for (int z = 0; z < 16; z++)
            {
                Require(
                    ReferenceEquals(chunk.GetBlockState(new BlockPos(x, 0, z)).Block, block),
                    message);
            }
        }

        OreVeinRule noVein = OreVeinRule.CreateCopper(
            new ConstantFunction(0.0F),
            new ConstantFunction(1.0F),
            new ConstantFunction(-1.0F));
        var noVeinResult = BuildChunkForRule(noVein);
        RequireColumnState(
            noVeinResult.Chunk,
            Blocks.STONE,
            "Non-positive vein density must return no material and preserve the existing block.");

        OreVeinRule copper = new(
            Blocks.COPPER_ORE.DefaultState,
            Blocks.RAW_COPPER_BLOCK.DefaultState,
            Blocks.GRANITE.DefaultState,
            0.0F,
            new ConstantFunction(1.0F),
            new ConstantFunction(1.0F),
            new ConstantFunction(-1.0F));
        var copperResult = BuildChunkForRule(copper);
        RequireColumnState(
            copperResult.Chunk,
            Blocks.COPPER_ORE,
            "Full density/richness with a negative filler gap and zero raw chance must place copper ore.");

        OreVeinRule rawCopper = copper with { RawOreChance = 1.0F };
        var rawCopperResult = BuildChunkForRule(rawCopper);
        RequireColumnState(
            rawCopperResult.Chunk,
            Blocks.RAW_COPPER_BLOCK,
            "A raw ore chance of one must place the corresponding raw block in every rich vein position.");

        OreVeinRule fillerFromRichness = copper with { Richness = new ConstantFunction(0.0F) };
        var fillerResult = BuildChunkForRule(fillerFromRichness);
        RequireColumnState(
            fillerResult.Chunk,
            Blocks.GRANITE,
            "Non-positive richness must select the vein filler block.");

        OreVeinRule fillerFromGap = copper with { FillerGap = new ConstantFunction(0.25F) };
        var gapResult = BuildChunkForRule(fillerFromGap);
        RequireColumnState(
            gapResult.Chunk,
            Blocks.GRANITE,
            "A positive filler-gap density must override otherwise full richness and select the filler block.");

        OreVeinRule probabilistic = new(
            Blocks.COPPER_ORE.DefaultState,
            Blocks.RAW_COPPER_BLOCK.DefaultState,
            Blocks.GRANITE.DefaultState,
            0.25F,
            new ConstantFunction(0.5F),
            new ConstantFunction(0.5F),
            new ConstantFunction(-1.0F));
        var probabilisticResult = BuildChunkForRule(probabilistic);
        PositionalRandomFactory randomFactory =
            probabilisticResult.RandomState.GetOrCreateRandomFactory(
                ResourceLocation.WithDefaultNamespace("ore"));

        int retainedStone = 0;
        int copperCount = 0;
        int rawCopperCount = 0;
        int graniteCount = 0;
        for (int x = 0; x < 16; x++)
        for (int z = 0; z < 16; z++)
        {
            IRandomSource random = randomFactory.At(x, 0, z);
            Block expected;
            if (random.NextFloat() > 0.5F)
            {
                expected = Blocks.STONE;
            }
            else if (random.NextFloat() < 0.5F)
            {
                expected = random.NextFloat() < 0.25F
                    ? Blocks.RAW_COPPER_BLOCK
                    : Blocks.COPPER_ORE;
            }
            else
            {
                expected = Blocks.GRANITE;
            }

            Block actual = probabilisticResult.Chunk
                .GetBlockState(new BlockPos(x, 0, z)).Block;
            Require(
                ReferenceEquals(actual, expected),
                "OreVeinRule must preserve vanilla random draw order and positional decisions for density, richness and raw ore.");
            if (ReferenceEquals(actual, Blocks.STONE))
                retainedStone++;
            else if (ReferenceEquals(actual, Blocks.COPPER_ORE))
                copperCount++;
            else if (ReferenceEquals(actual, Blocks.RAW_COPPER_BLOCK))
                rawCopperCount++;
            else if (ReferenceEquals(actual, Blocks.GRANITE))
                graniteCount++;
        }

        Require(
            retainedStone > 0 && copperCount > 0 && rawCopperCount > 0 && graniteCount > 0,
            "The fixed-seed probabilistic fixture must exercise every ore-vein output state.");
    }

    private static void CheckErodedBadlandsExtension()
    {
        DensityFunction terrainDensity =
            DensityFunctions.YClampedGradient(40, 41, 1.0F, -1.0F);
        NoiseGeneratorSettings settings = CreateSettings(
            terrainDensity,
            seaLevel: 0,
            noiseHeight: 256);
        RandomState randomState = CreateRandomState(settings);
        var generator = new NoiseBasedChunkGenerator(settings, randomState);
        ProtoChunk chunk = CreateChunk(new ChunkPos(0, 0), -64, 256);

        generator.BuildTerrain(
            chunk,
            _ => Holder<Biome>.Direct(Biomes.ERODED_BADLANDS));

        var badlandsSurface = randomState.GetOrCreateNoise(Noises.BADLANDS_SURFACE);
        var badlandsPillar = randomState.GetOrCreateNoise(Noises.BADLANDS_PILLAR);
        var badlandsRoof = randomState.GetOrCreateNoise(Noises.BADLANDS_PILLAR_ROOF);
        int expectedPillars = 0;

        for (int x = 0; x < 16; x++)
        for (int z = 0; z < 16; z++)
        {
            double pillarBuffer = Math.Min(
                Math.Abs(badlandsSurface.Get(x, 0.0, z) * 8.25),
                badlandsPillar.Get(x * 0.2, 0.0, z * 0.2) * 15.0);
            if (pillarBuffer <= 0.0)
                continue;

            double pillarFloor = Math.Abs(badlandsRoof.Get(x * 0.75, 0.0, z * 0.75) * 1.5);
            int expectedTop = Mth.Floor(64.0 + Math.Min(
                pillarBuffer * pillarBuffer * 2.5,
                Math.Ceiling(pillarFloor * 50.0) + 24.0));
            if (expectedTop < 41 || expectedTop > chunk.GetMaxY())
                continue;

            Require(
                ReferenceEquals(
                    chunk.GetBlockState(new BlockPos(x, expectedTop, z)).Block,
                    Blocks.STONE),
                "Eroded Badlands must extend each eligible pillar to its exact noise-derived top.");
            expectedPillars++;
        }

        Require(expectedPillars > 0,
            "The fixed-seed Eroded Badlands fixture must contain at least one in-range pillar.");

        NoiseGeneratorSettings oceanSettings = CreateSettings(
            terrainDensity,
            seaLevel: 63,
            noiseHeight: 256);
        var oceanGenerator = new NoiseBasedChunkGenerator(
            oceanSettings,
            CreateRandomState(oceanSettings));
        ProtoChunk oceanChunk = CreateChunk(new ChunkPos(0, 0), -64, 256);
        oceanGenerator.BuildTerrain(
            oceanChunk,
            _ => Holder<Biome>.Direct(Biomes.ERODED_BADLANDS));

        Require(
            ReferenceEquals(
                oceanChunk.GetBlockState(new BlockPos(0, 62, 0)),
                Blocks.WATER.DefaultState),
            "The Eroded Badlands water guard fixture must contain a water column above the stone floor.");
        for (int y = 41; y <= oceanChunk.GetMaxY(); y++)
        for (int x = 0; x < 16; x++)
        for (int z = 0; z < 16; z++)
        {
            Require(
                !ReferenceEquals(oceanChunk.GetBlockState(new BlockPos(x, y, z)).Block, Blocks.STONE),
                "Eroded Badlands must not extend a pillar through an intervening water column.");
        }
    }

    private static void CheckFrozenOceanExtension()
    {
        const int seaLevel = 63;
        const long seed = 132456789L;
        DensityFunction terrainDensity =
            DensityFunctions.YClampedGradient(40, 41, 1.0F, -1.0F);
        NoiseGeneratorSettings settings = CreateSettings(
            terrainDensity,
            seaLevel,
            noiseHeight: 256);
        RandomState randomState = CreateRandomState(settings);

        var icebergSurface = randomState.GetOrCreateNoise(Noises.ICEBERG_SURFACE);
        var icebergPillar = randomState.GetOrCreateNoise(Noises.ICEBERG_PILLAR);
        var icebergRoof = randomState.GetOrCreateNoise(Noises.ICEBERG_PILLAR_ROOF);
        PositionalRandomFactory positionalRandom = new XoroshiroRandomSource(seed).ForkPositional();

        int snowX = -1;
        int snowZ = -1;
        int snowY = int.MinValue;

        for (int x = 0; x < 256 && snowX < 0; x++)
        {
            for (int z = 0; z < 256; z++)
            {
                double iceberg = Math.Min(
                    Math.Abs(icebergSurface.Get(x, 0.0, z) * 8.25),
                    icebergPillar.Get(x * 1.28, 0.0, z * 1.28) * 15.0);
                if (iceberg <= 1.8)
                    continue;

                double roof = Math.Abs(icebergRoof.Get(x * 1.17, 0.0, z * 1.17) * 1.5);
                double top = Math.Min(
                    iceberg * iceberg * 1.2,
                    Math.Ceiling(roof * 40.0) + 14.0);
                if (Biomes.FROZEN_OCEAN.ShouldMeltFrozenOceanIcebergSlightly(
                        new BlockPos(x, seaLevel, z),
                        seaLevel))
                {
                    top -= 2.0;
                }

                if (top <= 2.0)
                    continue;

                int topY = (int)(seaLevel + top);
                var columnRandom = positionalRandom.At(x, 0, z);
                _ = columnRandom.NextInt(4);
                int minSnowHeight = seaLevel + 18 + columnRandom.NextInt(10);

                for (int y = topY - 1; y > minSnowHeight; y--)
                {
                    if (columnRandom.NextDouble() <= 0.01)
                        continue;

                    snowX = x;
                    snowZ = z;
                    snowY = y;
                    break;
                }

                if (snowX >= 0)
                    break;
            }
        }

        Require(snowX >= 0,
            "The fixed seed must contain at least one Frozen Ocean column whose vanilla iceberg random stream produces a snow cap.");

        ChunkPos chunkPos = new(snowX >> 4, snowZ >> 4);
        var generator = new NoiseBasedChunkGenerator(settings, randomState);
        ProtoChunk chunk = CreateChunk(chunkPos, -64, 256);
        generator.BuildTerrain(
            chunk,
            _ => Holder<Biome>.Direct(Biomes.FROZEN_OCEAN));

        Require(
            ReferenceEquals(
                chunk.GetBlockState(new BlockPos(snowX, snowY, snowZ)).Block,
                Blocks.SNOW_BLOCK),
            "Frozen Ocean must place a snow block at the first seeded eligible snow-cap position.");

        int packedIceAboveSea = 0;
        int snowBlocksAboveSea = 0;
        int minBlockX = chunkPos.GetMinBlockX();
        int minBlockZ = chunkPos.GetMinBlockZ();
        for (int y = seaLevel + 1; y <= chunk.GetMaxY(); y++)
        for (int x = 0; x < 16; x++)
        for (int z = 0; z < 16; z++)
        {
            BlockState state = chunk.GetBlockState(new BlockPos(minBlockX + x, y, minBlockZ + z));
            if (ReferenceEquals(state.Block, Blocks.PACKED_ICE))
                packedIceAboveSea++;
            else if (ReferenceEquals(state.Block, Blocks.SNOW_BLOCK))
                snowBlocksAboveSea++;
        }

        Require(packedIceAboveSea > 0,
            "Frozen Ocean must generate packed-ice iceberg columns above sea level for the fixed seed.");
        Require(snowBlocksAboveSea > 0,
            "Frozen Ocean must cap sufficiently tall iceberg columns with the seeded snow-block layers.");
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
        float climateTemperature = 0.0f,
        int noiseHeight = 96)
    {
        DensityFunction climate = new ConstantFunction(climateTemperature);
        var router = new NoiseRouter(
            climate, climate, climate, climate,
            climate, climate, climate, finalDensity);

        return new NoiseGeneratorSettings(
            NoiseSettings.Create(-64, noiseHeight),
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
