using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Lighting;

internal static class BlockLightEngineChecks
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
        RegistryBootstrap.Initialize();
        CheckBlockLightPropagationAndRemoval();
        CheckMissingChunkUsesOpaqueBoundary();
        CheckNoLightPropagationWhenColumnIsDisabled();
    }

    private static void CheckBlockLightPropagationAndRemoval()
    {
        var chunk = CreateChunk();
        BlockState emitter = new EmissiveBlock().DefaultState;
        var sourcePosition = new BlockPos(8, 8, 8);
        chunk.GetSection(chunk.GetSectionIndex(8)).SetBlockState(8, 8, 8, emitter);

        var getter = new TestChunkGetter(chunk);
        var engine = new BlockLightEngine(getter);
        engine.UpdateSectionStatus(SectionPos.Of(sourcePosition), sectionEmpty: false);
        engine.PropagateLightSources(chunk.GetPos());

        Require(engine.HasLightWork(), "Enqueued block light sources must report pending work.");
        engine.RunLightUpdates();
        engine.RunLightUpdates();

        Require(engine.GetLightValue(sourcePosition) == 14,
            "The emitting block must retain its registered light emission.");
        Require(engine.GetLightValue(sourcePosition.East()) == 13,
            "Block light must lose one level when propagating into air.");
        Require(engine.GetLightValue(sourcePosition.East(2)) == 12,
            "Block light must continue propagating through adjacent air blocks.");

        chunk.GetSection(chunk.GetSectionIndex(8)).SetBlockState(8, 8, 8, Blocks.AIR.DefaultState);
        engine.CheckBlock(sourcePosition);
        Require(engine.HasLightWork(), "Changing an emitter must enqueue light removal work.");
        engine.RunLightUpdates();
        engine.RunLightUpdates();

        Require(engine.GetLightValue(sourcePosition) == 0,
            "Removing the only emitter must clear its stored block light.");
        Require(engine.GetLightValue(sourcePosition.East()) == 0,
            "Removing the only emitter must propagate decreases to neighboring blocks.");
    }

    private static void CheckMissingChunkUsesOpaqueBoundary()
    {
        var chunk = CreateChunk();
        var getter = new TestChunkGetter(chunk);
        var engine = new BlockLightEngine(getter);

        BlockState air = Blocks.AIR.DefaultState;
        BlockState missing = Blocks.BEDROCK.DefaultState;
        Require(
            LightEngine.GetLightDampeningInto(air, missing, Direction.EAST, 1) >= 1,
            "A missing chunk boundary must not behave as an empty light-transparent region.");
        engine.UpdateSectionStatus(SectionPos.Of(new BlockPos(8, 8, 8)), sectionEmpty: false);
        engine.PropagateLightSources(chunk.GetPos());
        engine.RunLightUpdates();
    }

    private static void CheckNoLightPropagationWhenColumnIsDisabled()
    {
        var chunk = CreateChunk();
        var sourcePosition = new BlockPos(4, 4, 4);
        chunk.GetSection(chunk.GetSectionIndex(4)).SetBlockState(
            4,
            4,
            4,
            new EmissiveBlock().DefaultState);
        var getter = new TestChunkGetter(chunk);
        var storage = new BlockLightSectionStorage(getter);
        var engine = new BlockLightEngine(getter, storage);

        engine.UpdateSectionStatus(SectionPos.Of(sourcePosition), sectionEmpty: false);
        engine.RunLightUpdates();
        engine.CheckBlock(sourcePosition);
        engine.RunLightUpdates();

        Require(engine.GetLightValue(sourcePosition) == 0,
            "A column without block-light enablement must not accept block emission.");
    }

    private static ProtoChunk CreateChunk() =>
        new(
            new ChunkPos(0, 0),
            LevelHeightAccessor.Create(-16, 48),
            PalettedContainerFactory.CreateBuiltIn());

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
