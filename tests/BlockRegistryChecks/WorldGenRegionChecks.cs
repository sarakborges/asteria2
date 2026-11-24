using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Level;
using Asteria.World.LevelGen;

internal static class WorldGenRegionChecks
{
    public static void Run()
    {
        CheckDependencyStatusAndReadRadius();
        CheckWriteRadiusAndPostProcessing();
    }

    private static void CheckDependencyStatusAndReadRadius()
    {
        const long seed = 0x574F524C4447454EL;
        LevelHeightAccessor heightAccessor = LevelHeightAccessor.Create(-64, 384);
        var world = new OverworldWorld(seed, heightAccessor);
        NoiseBasedChunkGenerator generator = NoiseBasedChunkGenerator.CreateOverworld(seed);
        PalettedContainerFactory factory = PalettedContainerFactory.CreateBuiltIn();
        ChunkStep featuresStep = ChunkPyramid.GenerationPyramid.GetStepTo(ChunkStatus.FEATURES);
        Require(
            featuresStep.DirectDependencies.Size() == 9 &&
            ReferenceEquals(featuresStep.DirectDependencies.Get(0), ChunkStatus.TERRAIN) &&
            ReferenceEquals(featuresStep.DirectDependencies.Get(1), ChunkStatus.TERRAIN) &&
            ReferenceEquals(featuresStep.DirectDependencies.Get(2), ChunkStatus.STRUCTURE_STARTS),
            "The FEATURES step must retain its 9-ring direct dependency status profile.");

        ProtoChunk center = CreateChunk(
            new ChunkPos(0, 0),
            heightAccessor,
            factory,
            ChunkStatus.TERRAIN);
        ProtoChunk writeNeighbor = CreateChunk(
            new ChunkPos(1, 0),
            heightAccessor,
            factory,
            ChunkStatus.TERRAIN);
        ProtoChunk readNeighbor = CreateChunk(
            new ChunkPos(2, 0),
            heightAccessor,
            factory,
            ChunkStatus.STRUCTURE_STARTS);
        ProtoChunk wrongStatusNeighbor = CreateChunk(
            new ChunkPos(3, 0),
            heightAccessor,
            factory,
            ChunkStatus.EMPTY);
        ProtoChunk outOfRangeNeighbor = CreateChunk(
            new ChunkPos(9, 0),
            heightAccessor,
            factory,
            ChunkStatus.STRUCTURE_STARTS);

        var region = world.CreateWorldGenRegion(
            featuresStep,
            center,
            new ChunkAccess[]
            {
                writeNeighbor,
                readNeighbor,
                wrongStatusNeighbor,
                outOfRangeNeighbor
            });

        Require(
            region.GetChunk(0, 0) == center &&
            region.GetChunk(1, 0) == writeNeighbor &&
            region.GetChunk(2, 0) == readNeighbor,
            "WorldGenRegion must expose chunks whose status meets the direct dependency for their distance ring.");

        Require(
            region.HasChunk(8, 0) && !region.HasChunk(9, 0),
            "WorldGenRegion.hasChunk must use the direct dependency radius, not the write radius.");

        Require(
            region.GetBlockState(new BlockPos(32, 0, 0)).IsAir,
            "WorldGenRegion must permit reads from an allowed dependency chunk outside the write radius.");

        bool insufficientStatusRejected = false;
        try
        {
            _ = region.GetChunk(3, 0);
        }
        catch (InvalidOperationException exception)
        {
            insufficientStatusRejected = exception.Message.Contains(
                "outside the world-generation dependencies",
                StringComparison.Ordinal);
        }

        Require(
            insufficientStatusRejected,
            "WorldGenRegion must not expose a chunk that has not reached its direct dependency status.");

        bool outOfRangeRejected = false;
        try
        {
            _ = region.GetChunk(9, 0);
        }
        catch (InvalidOperationException)
        {
            outOfRangeRejected = true;
        }

        Require(
            outOfRangeRejected,
            "WorldGenRegion must reject requests outside the direct dependency window.");

        bool laterStatusRejected = false;
        try
        {
            _ = region.GetChunk(2, 0, ChunkStatus.TERRAIN);
        }
        catch (InvalidOperationException)
        {
            laterStatusRejected = true;
        }

        Require(
            laterStatusRejected,
            "WorldGenRegion must reject a requested status later than the maximum allowed status for that ring.");

        Require(
            region.GetBiome(new BlockPos(32, 32, 0)).IsBound,
            "WorldGenRegion biome queries must use the uncached climate resolver rather than requiring a BIOMES chunk at the queried ring.");
    }

    private static void CheckWriteRadiusAndPostProcessing()
    {
        const long seed = 0x57524954455A4F4EL;
        LevelHeightAccessor heightAccessor = LevelHeightAccessor.Create(-64, 384);
        var world = new OverworldWorld(seed, heightAccessor);
        NoiseBasedChunkGenerator generator = NoiseBasedChunkGenerator.CreateOverworld(seed);
        PalettedContainerFactory factory = PalettedContainerFactory.CreateBuiltIn();
        ChunkStep featuresStep = ChunkPyramid.GenerationPyramid.GetStepTo(ChunkStatus.FEATURES);

        ProtoChunk center = CreateChunk(
            new ChunkPos(0, 0),
            heightAccessor,
            factory,
            ChunkStatus.TERRAIN);
        ProtoChunk neighbor = CreateChunk(
            new ChunkPos(1, 0),
            heightAccessor,
            factory,
            ChunkStatus.TERRAIN);
        ProtoChunk readOnlyNeighbor = CreateChunk(
            new ChunkPos(2, 0),
            heightAccessor,
            factory,
            ChunkStatus.STRUCTURE_STARTS);

        var region = world.CreateWorldGenRegion(
            featuresStep,
            center,
            new ChunkAccess[] { neighbor, readOnlyNeighbor });

        var writablePosition = new BlockPos(17, 12, 4);
        var readOnlyPosition = new BlockPos(32, 12, 4);

        Require(
            region.IsWithinWriteZone(writablePosition) &&
            region.EnsureCanWrite(writablePosition) &&
            !region.IsWithinWriteZone(readOnlyPosition) &&
            !region.EnsureCanWrite(readOnlyPosition),
            "The FEATURES stage must allow writes only through Chebyshev radius one.");

        Require(
            region.SetBlock(writablePosition, Blocks.STONE.DefaultState) &&
            ReferenceEquals(
                neighbor.GetBlockState(writablePosition),
                Blocks.STONE.DefaultState),
            "WorldGenRegion.setBlock must write to a permitted neighboring chunk.");

        Require(
            !region.SetBlock(readOnlyPosition, Blocks.STONE.DefaultState) &&
            readOnlyNeighbor.GetBlockState(readOnlyPosition).IsAir,
            "WorldGenRegion.setBlock must refuse a write outside the step's write radius.");

        Require(
            region.SetBlock(new BlockPos(18, 12, 4), Blocks.GRASS_BLOCK.DefaultState, updateFlags: 0),
            "WorldGenRegion must preserve the block update flags for proto-chunk writes.");
    }

    private static ProtoChunk CreateChunk(
        ChunkPos position,
        LevelHeightAccessor heightAccessor,
        PalettedContainerFactory factory,
        ChunkStatus status)
    {
        var chunk = new ProtoChunk(position, heightAccessor, factory);
        chunk.SetPersistedStatus(status);
        return chunk;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
