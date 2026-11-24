using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Content.Biomes;
using Asteria.World;
using Asteria.World.Level;
using Asteria.World.LevelGen;

internal static class BiomeDecorationChecks
{
    public static void Run()
    {
        CheckEmptyDecorationStagesRunWithoutChangingTerrain();
    }

    private static void CheckEmptyDecorationStagesRunWithoutChangingTerrain()
    {
        const long seed = 0x4445434F52415445L;
        const long decorationRandomSeed = 0x123456789ABCDEF0L;

        LevelHeightAccessor heightAccessor = LevelHeightAccessor.Create(-64, 384);
        var world = new OverworldWorld(seed, heightAccessor);
        NoiseBasedChunkGenerator generator = NoiseBasedChunkGenerator.CreateOverworld(seed);
        PalettedContainerFactory factory = PalettedContainerFactory.CreateBuiltIn();
        ChunkStep featuresStep = ChunkPyramid.GenerationPyramid.GetStepTo(ChunkStatus.FEATURES);

        var chunks = new List<ProtoChunk>();
        ProtoChunk? center = null;
        for (int x = -1; x <= 1; x++)
        for (int z = -1; z <= 1; z++)
        {
            var chunk = new ProtoChunk(
                new Asteria.World.ChunkPos(x, z),
                heightAccessor,
                factory);
            chunk.SetPersistedStatus(ChunkStatus.TERRAIN);
            chunks.Add(chunk);
            if (x == 0 && z == 0)
                center = chunk;
        }

        ProtoChunk centerChunk = center ??
            throw new InvalidOperationException("The center test chunk was not created.");
        WorldGenRegion region = world.CreateWorldGenRegion(
            featuresStep,
            centerChunk,
            chunks.Cast<ChunkAccess>());

        IReadOnlyList<FeatureSorter.StepFeatureData> featuresPerStep =
            generator.GetFeaturesPerStep();
        Require(
            featuresPerStep.Count == 0,
            "The current built-in Overworld biomes must not gain synthetic placed features.");

        generator.ApplyBiomeDecoration(region, centerChunk, decorationRandomSeed);

        Require(
            chunks.All(static chunk => chunk.GetPersistedStatus() == ChunkStatus.TERRAIN),
            "Biome decoration must not promote chunk status before the chunk scheduler commits FEATURES.");
        Require(
            chunks.All(chunk => chunk.GetBlockState(new Asteria.Core.BlockPos(
                chunk.GetPos().GetMinBlockX(),
                0,
                chunk.GetPos().GetMinBlockZ())).IsAir),
            "An empty placed-feature configuration must leave the 3×3 terrain window unchanged.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
