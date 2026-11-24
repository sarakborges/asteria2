using Asteria.Content.Chunks;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Level;

internal static class LightChunkContractChecks
{
    private sealed class TestLightChunkGetter : LightChunkGetter
    {
        private readonly LightChunk _chunk;

        public TestLightChunkGetter(LightChunk chunk) => _chunk = chunk;

        public LightChunk? GetChunkForLighting(int chunkX, int chunkZ) =>
            chunkX == _chunk.GetPos().X && chunkZ == _chunk.GetPos().Z
                ? _chunk
                : null;

        public BlockGetter GetLevel() => _chunk;
    }

    public static void Run()
    {
        RegistryBootstrap.Initialize();

        ProtoChunk chunk = new(
            new ChunkPos(3, -2),
            LevelHeightAccessor.Create(-16, 48),
            PalettedContainerFactory.CreateBuiltIn());

        LightChunk lightChunk = chunk;
        Require(ReferenceEquals(lightChunk.GetSkyLightSources(), chunk.GetSkyLightSources()),
            "LightChunk must expose the ChunkAccess-owned skylight source map.");
        int sourceCount = 0;
        lightChunk.FindBlockLightSources((_, _) => sourceCount++);
        Require(sourceCount == 0, "An empty chunk must enumerate no block-light sources.");

        var getter = new TestLightChunkGetter(lightChunk);
        Require(ReferenceEquals(getter.GetChunkForLighting(3, -2), chunk),
            "LightChunkGetter must resolve an already available chunk at its coordinates.");
        Require(getter.GetChunkForLighting(3, -1) is null,
            "LightChunkGetter must return null for a missing chunk.");
        Require(ReferenceEquals(getter.GetLevel(), chunk),
            "LightChunkGetter must expose the associated block level.");
        getter.OnLightUpdate(LightLayer.SKY, SectionPos.Of(3, 0, -2));

        Require(Enum.GetNames<LightLayer>().SequenceEqual(new[] { "SKY", "BLOCK" }),
            "LightLayer must retain the vanilla SKY, BLOCK enum order.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
