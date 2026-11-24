using Asteria.Content.Chunks;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Lighting;

internal static class SkyLightEngineChecks
{
    private sealed class TestChunkGetter : LightChunkGetter
    {
        public ProtoChunk Chunk { get; }

        public TestChunkGetter(ProtoChunk chunk) => Chunk = chunk;

        public LightChunk? GetChunkForLighting(int chunkX, int chunkZ) =>
            chunkX == Chunk.GetPos().X && chunkZ == Chunk.GetPos().Z
                ? Chunk
                : null;

        public BlockGetter GetLevel() => Chunk;
    }

    public static void Run()
    {
        RegistryBootstrap.Initialize();
        CheckOpenSkySourcesFillStoredSections();
        CheckMissingColumnsUseEmptySkySourceMap();
    }

    private static void CheckOpenSkySourcesFillStoredSections()
    {
        var chunk = CreateChunk();
        chunk.InitializeLightSources();
        var getter = new TestChunkGetter(chunk);
        var engine = new SkyLightEngine(getter);
        var sectionPosition = SectionPos.Of(new BlockPos(8, 0, 8));

        engine.UpdateSectionStatus(sectionPosition, sectionEmpty: false);
        engine.PropagateLightSources(chunk.GetPos());
        engine.RunLightUpdates();

        Require(engine.GetLightValue(new BlockPos(8, 8, 8)) == 15,
            "An unobstructed sky column must receive maximum skylight.");
        Require(engine.GetLightValue(new BlockPos(8, -15, 8)) == 15,
            "Skylight must be stored through empty lower sections.");
        Require(engine.GetLightValue(new BlockPos(8, 16, 8)) == 15,
            "Skylight above the highest stored section must default to maximum.");
        Require(!engine.HasLightWork(),
            "An unobstructed source column must finish with empty light queues.");
    }

    private static void CheckMissingColumnsUseEmptySkySourceMap()
    {
        var chunk = CreateChunk(-2, 3);
        chunk.InitializeLightSources();
        var getter = new TestChunkGetter(chunk);
        var engine = new SkyLightEngine(getter);
        engine.UpdateSectionStatus(
            SectionPos.Of(new BlockPos(chunk.GetPos().GetMinBlockX() + 8, 0, chunk.GetPos().GetMinBlockZ() + 8)),
            sectionEmpty: false);

        engine.PropagateLightSources(chunk.GetPos());
        engine.RunLightUpdates();

        var position = new BlockPos(
            chunk.GetPos().GetMinBlockX() + 8,
            4,
            chunk.GetPos().GetMinBlockZ() + 8);
        Require(engine.GetLightValue(position) == 15,
            "A loaded open-sky chunk must retain full skylight with missing neighbors treated as empty columns.");
    }

    private static ProtoChunk CreateChunk(int chunkX = 0, int chunkZ = 0) =>
        new(
            new ChunkPos(chunkX, chunkZ),
            LevelHeightAccessor.Create(-16, 48),
            PalettedContainerFactory.CreateBuiltIn());

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
