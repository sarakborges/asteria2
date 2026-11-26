using Asteria.Content.Chunks.Status;
using Asteria.Server.Level;

internal static class ChunkLevelChecks
{
    public static void Run()
    {
        CheckDependencyRadiusAndDistanceMapping();
        CheckGenerationStatusMapping();
        CheckChunkStatusLevels();
        CheckFullChunkStatusLevels();
        CheckTickingAndLoadingBoundaries();
    }

    private static void CheckDependencyRadiusAndDistanceMapping()
    {
        Require(
            ChunkLevel.RADIUS_AROUND_FULL_CHUNK == 11 &&
            ChunkLevel.MAX_LEVEL == 44,
            "ChunkLevel must derive the vanilla FULL dependency radius and maximum level from ChunkPyramid.");

        ChunkStatus[] expectedByDistance =
        [
            ChunkStatus.LIGHT,
            ChunkStatus.INITIALIZE_LIGHT,
            ChunkStatus.TERRAIN,
            ChunkStatus.BIOMES,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS
        ];

        Require(
            ChunkLevel.GetStatusAroundFullChunk(-1) == ChunkStatus.FULL &&
            ChunkLevel.GetStatusAroundFullChunk(0) == ChunkStatus.FULL,
            "The center and all positions closer than the full-chunk center must resolve to FULL.");

        for (int distance = 1; distance <= expectedByDistance.Length; distance++)
        {
            Require(
                ReferenceEquals(
                    ChunkLevel.GetStatusAroundFullChunk(distance),
                    expectedByDistance[distance - 1]),
                $"ChunkLevel must preserve the accumulated FULL dependency at distance {distance}.");
        }

        Require(
            ChunkLevel.GetStatusAroundFullChunk(12, null) is null &&
            ReferenceEquals(
                ChunkLevel.GetStatusAroundFullChunk(12, ChunkStatus.FEATURES),
                ChunkStatus.FEATURES) &&
            ReferenceEquals(ChunkLevel.GetStatusAroundFullChunk(12), ChunkStatus.EMPTY),
            "Distances beyond the FULL dependency radius must use the supplied fallback, including EMPTY in the default overload.");
    }

    private static void CheckGenerationStatusMapping()
    {
        Require(
            ReferenceEquals(ChunkLevel.GenerationStatus(31), ChunkStatus.FULL) &&
            ReferenceEquals(ChunkLevel.GenerationStatus(32), ChunkStatus.FULL) &&
            ReferenceEquals(ChunkLevel.GenerationStatus(33), ChunkStatus.FULL),
            "Levels at or below FULL_CHUNK_LEVEL must map to FULL generation status.");

        Require(
            ReferenceEquals(ChunkLevel.GenerationStatus(34), ChunkStatus.INITIALIZE_LIGHT) &&
            ReferenceEquals(ChunkLevel.GenerationStatus(35), ChunkStatus.TERRAIN) &&
            ReferenceEquals(ChunkLevel.GenerationStatus(36), ChunkStatus.BIOMES) &&
            ReferenceEquals(ChunkLevel.GenerationStatus(44), ChunkStatus.STRUCTURE_STARTS),
            "GenerationStatus must map increasing ticket levels through the exact FULL dependency rings.");

        Require(
            ChunkLevel.GenerationStatus(45) is null,
            "GenerationStatus must return null above the maximum generation dependency level.");
    }

    private static void CheckChunkStatusLevels()
    {
        (ChunkStatus Status, int Level)[] expected =
        [
            (ChunkStatus.EMPTY, 44),
            (ChunkStatus.STRUCTURE_STARTS, 44),
            (ChunkStatus.STRUCTURE_REFERENCES, 36),
            (ChunkStatus.BIOMES, 36),
            (ChunkStatus.TERRAIN, 35),
            (ChunkStatus.FEATURES, 34),
            (ChunkStatus.INITIALIZE_LIGHT, 34),
            (ChunkStatus.LIGHT, 33),
            (ChunkStatus.FULL, 33)
        ];

        foreach ((ChunkStatus status, int level) in expected)
        {
            Require(
                ChunkLevel.ByStatus(status) == level,
                $"ChunkLevel.ByStatus must map {status} to level {level}.");
        }
    }

    private static void CheckFullChunkStatusLevels()
    {
        (FullChunkStatus Status, int Level)[] expected =
        [
            (FullChunkStatus.INACCESSIBLE, 44),
            (FullChunkStatus.FULL, 33),
            (FullChunkStatus.BLOCK_TICKING, 32),
            (FullChunkStatus.ENTITY_TICKING, 31)
        ];

        foreach ((FullChunkStatus status, int level) in expected)
        {
            Require(
                ChunkLevel.ByStatus(status) == level,
                $"ChunkLevel.ByStatus must map {status} to level {level}.");
        }

        Require(
            ChunkLevel.FullStatus(30) == FullChunkStatus.ENTITY_TICKING &&
            ChunkLevel.FullStatus(31) == FullChunkStatus.ENTITY_TICKING &&
            ChunkLevel.FullStatus(32) == FullChunkStatus.BLOCK_TICKING &&
            ChunkLevel.FullStatus(33) == FullChunkStatus.FULL &&
            ChunkLevel.FullStatus(34) == FullChunkStatus.INACCESSIBLE,
            "FullStatus must preserve the four vanilla ticket-level boundaries.");
    }

    private static void CheckTickingAndLoadingBoundaries()
    {
        Require(
            ChunkLevel.IsEntityTicking(31) &&
            !ChunkLevel.IsEntityTicking(32) &&
            ChunkLevel.IsBlockTicking(32) &&
            !ChunkLevel.IsBlockTicking(33),
            "Entity and block ticking predicates must use the vanilla inclusive level boundaries.");

        Require(
            ChunkLevel.IsLoaded(44) &&
            !ChunkLevel.IsLoaded(45) &&
            ChunkLevel.IsLoaded(int.MinValue),
            "A chunk is loaded through MAX_LEVEL, inclusively.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
