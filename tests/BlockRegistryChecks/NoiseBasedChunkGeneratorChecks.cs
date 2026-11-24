using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.Materials.Rules;

internal static class NoiseBasedChunkGeneratorChecks
{
    public static void Run()
    {
        CheckGlobalFluidPickerBoundaries();
        CheckPositiveDensityUsesDefaultBlock();
        CheckNegativeDensityMaterializesLavaWaterAndAir();
        CheckChunkCoordinateAndHeightmapMaterialization();
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

    private static ProtoChunk CreateChunk(ChunkPos position, int minY, int height) =>
        new(position, LevelHeightAccessor.Create(minY, height), PalettedContainerFactory.CreateBuiltIn());

    private static NoiseGeneratorSettings CreateSettings(DensityFunction finalDensity, int seaLevel)
    {
        DensityFunction climate = new ConstantFunction(0.0f);
        var router = new NoiseRouter(
            climate, climate, climate, climate,
            climate, climate, climate, finalDensity);

        return new NoiseGeneratorSettings(
            NoiseSettings.Create(-64, 96),
            Blocks.STONE.DefaultState,
            Blocks.WATER.DefaultState,
            router,
            Holder<MaterialRule>.Direct(new BlockRule(Blocks.STONE.DefaultState)),
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
