using Asteria.Content.Chunks.Status;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.World;

internal static class ChunkStatusChecks
{
    public static void Run()
    {
        IReadOnlyList<ChunkStatus> statuses = ChunkStatus.GetStatusList();
        string[] expectedNames =
        {
            "asteria:empty",
            "asteria:structure_starts",
            "asteria:structure_references",
            "asteria:biomes",
            "asteria:terrain",
            "asteria:features",
            "asteria:initialize_light",
            "asteria:light",
            "asteria:full"
        };

        Require(statuses.Count == expectedNames.Length,
            "The mob-free chunk lifecycle must expose exactly nine statuses.");
        Require(BuiltInRegistries.ChunkStatus.Count == expectedNames.Length,
            "All nine mob-free chunk statuses must be registered in the built-in registry.");
        Require(BuiltInRegistries.Root.Count == 19,
            "The built-in root registry must include normal-noise, density-function, density-function-type, material-rule, material-rule-type, material-condition, material-condition-type, noise-generator-settings and chunk-status registries.");

        for (int index = 0; index < expectedNames.Length; index++)
        {
            ChunkStatus status = statuses[index];
            Require(status.GetIndex() == index,
                "Chunk status indices must follow registration order.");
            Require(status.GetName() == expectedNames[index],
                "Chunk status identifiers must match their registered names and order.");
            Require(ReferenceEquals(ChunkStatus.ByName(expectedNames[index]), status),
                "ChunkStatus.byName must resolve the registered status instance.");
        }

        Require(ReferenceEquals(ChunkStatus.EMPTY.GetParent(), ChunkStatus.EMPTY),
            "The first status must be its own parent.");
        for (int index = 1; index < statuses.Count; index++)
        {
            Require(ReferenceEquals(statuses[index].GetParent(), statuses[index - 1]),
                "Each chunk status must point to the immediately preceding status.");
        }

        Require(statuses.Take(8).All(status => status.GetChunkType() == ChunkType.PROTOCHUNK),
            "The first eight mob-free chunk statuses must produce ProtoChunks.");
        Require(ChunkStatus.FULL.GetChunkType() == ChunkType.LEVELCHUNK,
            "Only the full status must produce a LevelChunk.");

        Require(ChunkStatus.MAX_STRUCTURE_DISTANCE == 8,
            "The maximum structure dependency distance must be eight chunks.");
        Require(ReferenceEquals(
                ChunkStatus.EMPTY.GetHeightmapsAfter(),
                ChunkStatus.STRUCTURE_STARTS.GetHeightmapsAfter()) &&
            ReferenceEquals(
                ChunkStatus.STRUCTURE_REFERENCES.GetHeightmapsAfter(),
                ChunkStatus.BIOMES.GetHeightmapsAfter()),
            "The first four statuses must share the world-generation heightmap set.");
        Require(ChunkStatus.EMPTY.GetHeightmapsAfter().SetEquals(new[]
            {
                Heightmap.Types.OCEAN_FLOOR_WG,
                Heightmap.Types.WORLD_SURFACE_WG
            }),
            "Statuses through biomes must retain only the two world-generation heightmaps.");
        Require(ReferenceEquals(ChunkStatus.TERRAIN.GetHeightmapsAfter(), ChunkStatus.FULL.GetHeightmapsAfter()),
            "Terrain and later statuses must share the final heightmap set.");
        Require(ChunkStatus.FULL.GetHeightmapsAfter().SetEquals(new[]
            {
                Heightmap.Types.OCEAN_FLOOR,
                Heightmap.Types.WORLD_SURFACE,
                Heightmap.Types.MOTION_BLOCKING,
                Heightmap.Types.MOTION_BLOCKING_NO_LEAVES
            }),
            "Terrain and later statuses must retain all four final heightmaps.");

        Require(ChunkStatus.EMPTY.IsBefore(ChunkStatus.FULL) &&
            ChunkStatus.FULL.IsAfter(ChunkStatus.EMPTY) &&
            ChunkStatus.TERRAIN.IsOrAfter(ChunkStatus.BIOMES) &&
            ChunkStatus.BIOMES.IsOrBefore(ChunkStatus.FEATURES),
            "Chunk status ordering predicates must compare their linear indices.");
        Require(ReferenceEquals(ChunkStatus.Max(ChunkStatus.TERRAIN, ChunkStatus.LIGHT), ChunkStatus.LIGHT) &&
            ReferenceEquals(ChunkStatus.Max(ChunkStatus.LIGHT, ChunkStatus.LIGHT), ChunkStatus.LIGHT),
            "ChunkStatus.max must return the later status and prefer the second argument on ties.");
        Require(ChunkStatus.ByName("asteria:spawn") is null &&
            ChunkStatus.ByName("asteria:not_registered") is null &&
            ChunkStatus.ByName("not a resource location") is null,
            "Unknown or malformed chunk-status names must resolve to null.");
        Require(!ReferenceEquals(statuses, ChunkStatus.GetStatusList()),
            "getStatusList must return a new ordered list on each call.");
        Require(ReferenceEquals(ChunkStatus.EMPTY.GetHeightmapsAfter(), ChunkStatus.STRUCTURE_REFERENCES.GetHeightmapsAfter()),
            "The shared world-generation heightmap set must retain identity across the early statuses.");
        Require(ChunkStatus.EMPTY.GetName() == ChunkStatus.EMPTY.ToString(),
            "ChunkStatus.toString must delegate to the registered name.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
