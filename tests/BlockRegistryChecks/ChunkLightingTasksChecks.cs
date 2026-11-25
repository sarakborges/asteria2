using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Level;
using Asteria.World.LevelGen;
using Asteria.Util;
using Asteria.World.Lighting;

internal static class ChunkLightingTasksChecks
{
    private sealed class EmissiveBlock : Block
    {
        public EmissiveBlock()
            : base(
                BlockProperties.Of()
                    .NoCollision()
                    .LightLevel(static _ => 14),
                createIntrusiveRegistryHolder: false)
        {
        }
    }

    private sealed class TestChunkGetter : LightChunkGetter
    {
        private readonly Dictionary<long, ProtoChunk> _chunks;

        public TestChunkGetter(Dictionary<long, ProtoChunk> chunks) =>
            _chunks = chunks;

        public LightChunk? GetChunkForLighting(int chunkX, int chunkZ) =>
            _chunks.TryGetValue(ChunkPos.Pack(chunkX, chunkZ), out ProtoChunk? chunk)
                ? chunk
                : null;

        public BlockGetter GetLevel() => _chunks[ChunkPos.Pack(0, 0)];
    }

    public static void Run()
    {
        RegistryBootstrap.Initialize();
        CheckLightingRequiresPreviousChunkStages();
        CheckInitializeAndLightLifecycle();
        CheckPyramidLightingTasks();
    }

    private static void CheckLightingRequiresPreviousChunkStages()
    {
        ProtoChunk chunk = CreateChunk(0, 0);
        var getter = new TestChunkGetter(new Dictionary<long, ProtoChunk>
        {
            [chunk.GetPos().Pack()] = chunk
        });
        var engine = new LevelLightEngine(getter, hasBlockLight: true, hasSkyLight: true);

        RequireThrows<InvalidOperationException>(
            () => ChunkLightingTasks.InitializeLight(chunk, engine, lighted: false),
            "Lighting initialization must reject chunks before FEATURES.");

        chunk.SetPersistedStatus(ChunkStatus.TERRAIN);
        RequireThrows<InvalidOperationException>(
            () => ChunkLightingTasks.InitializeLight(chunk, engine, lighted: false),
            "Lighting initialization must not skip the FEATURES stage.");

        RequireThrows<InvalidOperationException>(
            () => ChunkLightingTasks.LightChunk(chunk, engine, lighted: false),
            "The lighting task must reject chunks before INITIALIZE_LIGHT.");
    }

    private static void CheckInitializeAndLightLifecycle()
    {
        var chunks = new Dictionary<long, ProtoChunk>();
        foreach (ChunkPos position in ChunkPos.RangeClosed(ChunkPos.Zero, 1))
        {
            ProtoChunk chunk = CreateChunk(position.X, position.Z);
            chunk.SetPersistedStatus(ChunkStatus.FEATURES);
            chunks.Add(position.Pack(), chunk);
        }

        ProtoChunk center = chunks[ChunkPos.Zero.Pack()];
        BlockPos sourcePosition = new(8, 8, 8);
        BlockState emitter = new EmissiveBlock().DefaultState;
        center.GetSection(center.GetSectionIndex(sourcePosition.GetY()))
            .SetBlockState(8, 8, 8, emitter);

        var getter = new TestChunkGetter(chunks);
        var engine = new LevelLightEngine(getter, hasBlockLight: true, hasSkyLight: true);

        foreach (ProtoChunk chunk in chunks.Values)
        {
            ChunkLightingTasks.InitializeLight(chunk, engine, lighted: false);
            Require(!chunk.IsLightCorrect(),
                "Initializing lighting must not mark a chunk's computed light as correct.");
            chunk.SetPersistedStatus(ChunkStatus.INITIALIZE_LIGHT);
        }

        SectionPos sourceSection = SectionPos.Of(sourcePosition);
        Require(
            engine.GetDebugSectionType(LightLayer.BLOCK, sourceSection) == LightSectionType.LightAndData &&
            engine.GetDebugSectionType(LightLayer.SKY, sourceSection) == LightSectionType.LightAndData,
            "InitializeLight must register each non-empty section in both light channels.");

        long centerColumn = SectionPos.GetZeroNode(center.GetPos().X, center.GetPos().Z);
        Require(!engine.LightOnInColumn(centerColumn),
            "A newly generated chunk's light column must remain disabled after initialization.");

        BlockPos changedPosition = new(2, 8, 2);
        center.SetBlockState(changedPosition, Blocks.STONE.DefaultState);
        Require(engine.HasLightWork(),
            "A ProtoChunk at INITIALIZE_LIGHT must send post-initialization block changes to its attached light engine.");

        ChunkLightingTasks.LightChunk(center, engine, lighted: false);

        Require(center.IsLightCorrect(),
            "The lighting task must set light-correct only after the propagation batch.");
        Require(ReferenceEquals(center.GetPersistedStatus(), ChunkStatus.INITIALIZE_LIGHT),
            "The lighting task must leave status promotion to the chunk scheduler.");
        Require(engine.LightOnInColumn(centerColumn),
            "Propagating a new chunk must enable its light column.");
        Require(
            engine.GetLayerListener(LightLayer.BLOCK).GetLightValue(sourcePosition) == 14 &&
            engine.GetLayerListener(LightLayer.BLOCK).GetLightValue(sourcePosition.East()) == 13,
            "The lifecycle must seed and propagate block emission through the registered light engine.");
        Require(engine.GetLayerListener(LightLayer.SKY).GetLightValue(sourcePosition) == 15,
            "The lifecycle must seed maximum skylight in an unobstructed column.");
    }

    private static void CheckPyramidLightingTasks()
    {
        var chunks = new Dictionary<long, ProtoChunk>();
        foreach (ChunkPos position in ChunkPos.RangeClosed(ChunkPos.Zero, 1))
        {
            ProtoChunk chunk = CreateChunk(position.X, position.Z);
            chunk.SetPersistedStatus(ChunkStatus.FEATURES);
            chunks.Add(position.Pack(), chunk);
        }

        ProtoChunk center = chunks[ChunkPos.Zero.Pack()];
        BlockPos sourcePosition = new(8, 8, 8);
        BlockState emitter = new EmissiveBlock().DefaultState;
        center.GetSection(center.GetSectionIndex(sourcePosition.GetY()))
            .SetBlockState(8, 8, 8, emitter);

        var getter = new TestChunkGetter(chunks);
        var engine = new LevelLightEngine(getter, hasBlockLight: true, hasSkyLight: true);
        var world = new OverworldWorld(
            0x4C49474854535447L,
            LevelHeightAccessor.Create(-16, 48));
        var context = new WorldGenContext(world, world.Generator, engine);
        ChunkStep initializeStep = ChunkPyramid.GenerationPyramid
            .GetStepTo(ChunkStatus.INITIALIZE_LIGHT);

        foreach (ProtoChunk chunk in chunks.Values)
        {
            StaticCache2D<ChunkAccess?> centerCache = StaticCache2D<ChunkAccess?>.Create(
                chunk.GetPos().X,
                chunk.GetPos().Z,
                0,
                (_, _) => chunk);
            ChunkAccess initialized = initializeStep.Apply(
                    context,
                    centerCache,
                    chunk)
                .AsTask()
                .GetAwaiter()
                .GetResult();

            Require(
                ReferenceEquals(initialized, chunk) &&
                ReferenceEquals(chunk.GetPersistedStatus(), ChunkStatus.INITIALIZE_LIGHT) &&
                !chunk.IsLightCorrect(),
                "The INITIALIZE_LIGHT pyramid task must initialize storage and promote only after registration.");
        }

        StaticCache2D<ChunkAccess?> lightCache = StaticCache2D<ChunkAccess?>.Create(
            center.GetPos().X,
            center.GetPos().Z,
            1,
            (chunkX, chunkZ) => chunks[ChunkPos.Pack(chunkX, chunkZ)]);
        ChunkAccess lit = ChunkPyramid.GenerationPyramid
            .GetStepTo(ChunkStatus.LIGHT)
            .Apply(context, lightCache, center)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        Require(
            ReferenceEquals(lit, center) &&
            ReferenceEquals(center.GetPersistedStatus(), ChunkStatus.LIGHT) &&
            center.IsLightCorrect(),
            "The LIGHT pyramid task must validate its initialized radius-one neighborhood, propagate light, and promote its center.");
        Require(
            engine.GetLayerListener(LightLayer.BLOCK).GetLightValue(sourcePosition) == 14,
            "The pyramid LIGHT task must propagate block emission in the center chunk.");

        ProtoChunk incompleteCenter = CreateChunk(32, 32);
        incompleteCenter.SetPersistedStatus(ChunkStatus.INITIALIZE_LIGHT);
        StaticCache2D<ChunkAccess?> incompleteCache = StaticCache2D<ChunkAccess?>.Create(
            incompleteCenter.GetPos().X,
            incompleteCenter.GetPos().Z,
            0,
            (_, _) => incompleteCenter);
        RequireThrows<InvalidOperationException>(
            () => ChunkStatusTasks.GenerateLight(
                context,
                ChunkPyramid.GenerationPyramid.GetStepTo(ChunkStatus.LIGHT),
                incompleteCache,
                incompleteCenter),
            "The LIGHT task must reject a cache without its complete INITIALIZE_LIGHT radius-one neighborhood.");
        Require(
            ReferenceEquals(incompleteCenter.GetPersistedStatus(), ChunkStatus.INITIALIZE_LIGHT) &&
            !incompleteCenter.IsLightCorrect(),
            "A LIGHT task with missing neighbors must not propagate or promote its center chunk.");
    }

    private static ProtoChunk CreateChunk(int chunkX, int chunkZ) =>
        new(
            new ChunkPos(chunkX, chunkZ),
            LevelHeightAccessor.Create(-16, 48),
            PalettedContainerFactory.CreateBuiltIn());

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
