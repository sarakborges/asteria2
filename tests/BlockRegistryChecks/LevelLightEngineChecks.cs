using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Lighting;

internal static class LevelLightEngineChecks
{
    private sealed class EmissiveBlock : Block
    {
        public EmissiveBlock()
            : base(
                BlockProperties.Of()
                    .NoCollision()
                    .LightLevel(_ => 14),
                createIntrusiveRegistryHolder: false)
        {
        }
    }

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
        CheckEmptyEngineAndDisabledChannels();
        CheckCombinedBlockAndSkyLighting();
        CheckChannelSpecificDebugAndDataQueue();
    }

    private static void CheckEmptyEngineAndDisabledChannels()
    {
        LevelLightEngine empty = LevelLightEngine.EMPTY;
        var chunk = CreateChunk();
        var noChannels = new LevelLightEngine(new TestChunkGetter(chunk), false, false);
        var position = new BlockPos(0, 0, 0);
        var section = SectionPos.Of(position);

        Require(ReferenceEquals(empty.GetLayerListener(LightLayer.BLOCK), LayerLightEventListener.Dummy.Instance),
            "The static empty level engine must return the dummy block listener.");
        Require(ReferenceEquals(noChannels.GetLayerListener(LightLayer.SKY), LayerLightEventListener.Dummy.Instance),
            "A disabled sky channel must return the dummy listener.");
        Require(empty.GetDebugData(LightLayer.BLOCK, section) == "n/a",
            "A level without block light must report n/a debug data.");
        Require(noChannels.GetDebugSectionType(LightLayer.SKY, section) == LightSectionType.Empty,
            "A disabled light channel must report an empty debug section.");
        Require(empty.GetRawBrightness(position, 7) == 0,
            "The empty level engine must return zero raw brightness.");
        Require(noChannels.GetRawBrightness(position, 7) == 0,
            "A level with both channels disabled must return zero raw brightness.");
        Require(noChannels.GetLightSectionCount() == 5,
            "A three-section build height must expose three light sections plus two padding sections.");
        Require(noChannels.GetMinLightSection() == -2 && noChannels.GetMaxLightSection() == 3,
            "Light section bounds must include one section of padding at both ends.");
        Require(noChannels.LightOnInColumn(SectionPos.GetZeroNode(0, 0)),
            "A level without a block-light engine must report columns enabled.");
        Require(!noChannels.HasLightWork() && noChannels.RunLightUpdates() == 0,
            "Disabled engines must have no pending work and perform zero updates.");

        noChannels.CheckBlock(position);
        noChannels.UpdateSectionStatus(section, sectionEmpty: false);
        noChannels.SetLightEnabled(new ChunkPos(0, 0), enabled: true);
        noChannels.PropagateLightSources(new ChunkPos(0, 0));
        noChannels.QueueSectionData(LightLayer.BLOCK, section, new DataLayer(3));
        noChannels.RetainData(new ChunkPos(0, 0), retain: true);
        Require(!noChannels.HasLightWork(),
            "All lifecycle operations must remain no-ops when both engines are disabled.");
    }

    private static void CheckCombinedBlockAndSkyLighting()
    {
        ProtoChunk chunk = CreateChunk();
        chunk.InitializeLightSources();
        var position = new BlockPos(8, 8, 8);
        BlockState emitter = new EmissiveBlock().DefaultState;
        chunk.GetSection(chunk.GetSectionIndex(position.GetY()))
            .SetBlockState(8, 8, 8, emitter);

        var engine = new LevelLightEngine(new TestChunkGetter(chunk), hasBlockLight: true, hasSkyLight: true);
        var section = SectionPos.Of(position);
        engine.UpdateSectionStatus(section, sectionEmpty: false);
        engine.SetLightEnabled(chunk.GetPos(), enabled: true);
        engine.PropagateLightSources(chunk.GetPos());

        for (int i = 0; i < 8 && engine.HasLightWork(); i++)
            engine.RunLightUpdates();

        Require(!engine.HasLightWork(),
            "Both light engines must finish their queued work for an isolated open-sky chunk.");
        int blockLight = engine.GetLayerListener(LightLayer.BLOCK).GetLightValue(position);
        Require(
            blockLight == 14,
            $"The block channel listener must expose the registered block emission (actual {blockLight}, raw {engine.GetRawBrightness(position, 0)}, debug {engine.GetDebugData(LightLayer.BLOCK, section)}, type {engine.GetDebugSectionType(LightLayer.BLOCK, section)}).");
        Require(engine.GetLayerListener(LightLayer.SKY).GetLightValue(position) == 15,
            "The sky channel listener must expose full skylight in an unobstructed column.");
        Require(engine.GetRawBrightness(position, 0) == 15,
            "Raw brightness must return the maximum of block and sky light.");
        Require(engine.GetRawBrightness(position, 1) == 14,
            "Sky dampening must be applied before selecting the maximum light channel.");
        Require(engine.GetDebugSectionType(LightLayer.BLOCK, section) == LightSectionType.LightAndData,
            "A nonempty section with block light storage must report LIGHT_AND_DATA.");
        Require(engine.GetDebugSectionType(LightLayer.SKY, section) == LightSectionType.LightAndData,
            "A nonempty section with skylight storage must report LIGHT_AND_DATA.");

        long zeroNode = SectionPos.GetZeroNode(chunk.GetPos().X, chunk.GetPos().Z);
        Require(engine.LightOnInColumn(zeroNode),
            "The level column must report enabled only when both available channels are enabled.");
        engine.SetLightEnabled(chunk.GetPos(), enabled: false);
        Require(!engine.LightOnInColumn(zeroNode),
            "Disabling one or both available light channels must disable the level column.");
    }

    private static void CheckChannelSpecificDebugAndDataQueue()
    {
        ProtoChunk chunk = CreateChunk(2, -1);
        chunk.InitializeLightSources();
        var engine = new LevelLightEngine(new TestChunkGetter(chunk), hasBlockLight: false, hasSkyLight: true);
        var section = SectionPos.Of(new BlockPos(chunk.GetPos().GetMinBlockX() + 4, 0, chunk.GetPos().GetMinBlockZ() + 6));
        var data = new DataLayer(9);

        engine.QueueSectionData(LightLayer.SKY, section, data);
        Require(ReferenceEquals(engine.GetLayerListener(LightLayer.BLOCK), LayerLightEventListener.Dummy.Instance),
            "A disabled block channel must use the dummy listener while sky remains enabled.");
        Require(ReferenceEquals(engine.GetLayerListener(LightLayer.SKY).GetDataLayerData(section), data),
            "Queued sky section data must be exposed by its layer listener.");
        Require(engine.GetDebugData(LightLayer.BLOCK, section) == "n/a",
            "Disabled block light debug data must report n/a.");

        engine.RetainData(chunk.GetPos(), retain: true);
        engine.UpdateSectionStatus(section, sectionEmpty: false);
        engine.RunLightUpdates();
        Require(engine.GetLayerListener(LightLayer.SKY).GetDataLayerData(section) is not null,
            "Queued sky layer data must remain addressable after section initialization.");
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
