using System.Text.Json;
using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World;
using Asteria.World.Level;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Features;
using Asteria.World.LevelGen.Features.StateProviders;
using Asteria.World.LevelGen.Placement;
using Asteria.World.Ticks;

internal static class SimpleBlockFeatureChecks
{
    private sealed class RejectingBlock : Block
    {
        public RejectingBlock()
            : base(BlockProperties.Of())
        {
        }

        public override bool CanSurvive(
            BlockState state,
            BlockGetter level,
            BlockPos position) => false;
    }

    private sealed record NullStateProvider : BlockStateProvider
    {
        public override BlockState GetState(
            BlockGetter level,
            IRandomSource random,
            BlockPos position) =>
            throw new InvalidOperationException("The optional state provider must return null.");

        public override BlockState? GetOptionalState(
            BlockGetter level,
            IRandomSource random,
            BlockPos position) => null;

        public override MapCodec<BlockStateProvider> Codec() =>
            throw new NotSupportedException("NullStateProvider is not serialized.");
    }

    public static void Run()
    {
        CheckFeatureTypeRegistrationAndCodec();
        CheckSimplePlacementAndSurvival();
        CheckScheduledTickUsesWorldTimeAndRegionOrder();
    }

    private static void CheckFeatureTypeRegistrationAndCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        Require(
            BuiltInRegistries.FeatureType.Count == 1 &&
            BuiltInRegistries.FeatureType.ContainsKey(ResourceLocation.Parse("asteria:simple_block")) &&
            !BuiltInRegistries.FeatureType.ContainsKey(ResourceLocation.Parse("minecraft:simple_block")),
            "The Asteria feature registry must register SimpleBlockFeature under its own namespace.");

        Feature decoded = FeatureCodecs.DIRECT_CODEC
            .Parse(
                ops,
                ParseJson(
                    """{"type":"asteria:simple_block","to_place":{"type":"asteria:simple","state":{"id":"minecraft:stone"}}}"""))
            .GetOrThrow();

        Require(
            decoded is SimpleBlockFeature feature &&
            feature.ToPlace.Value is SimpleStateProvider { State: var state } &&
            ReferenceEquals(state, Blocks.STONE.DefaultState) &&
            !feature.ScheduleTick,
            "SimpleBlockFeature must decode its required provider and default schedule_tick=false.");

        JsonElement encoded = FeatureCodecs.DIRECT_CODEC
            .EncodeStart(ops, decoded)
            .GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:simple_block" &&
            ops.Get(encoded, "to_place").GetOrThrow().ValueKind == JsonValueKind.Object &&
            !ops.Get(encoded, "schedule_tick").TryGetResult(out _),
            "SimpleBlockFeature must encode the provider and omit the default schedule_tick field.");

        Feature scheduled = FeatureCodecs.DIRECT_CODEC
            .Parse(
                ops,
                ParseJson(
                    """{"type":"asteria:simple_block","to_place":{"type":"asteria:simple","state":{"id":"minecraft:stone"}},"schedule_tick":true}"""))
            .GetOrThrow();
        Require(
            scheduled is SimpleBlockFeature { ScheduleTick: true },
            "SimpleBlockFeature must preserve an explicit schedule_tick=true field.");
    }

    private static void CheckSimplePlacementAndSurvival()
    {
        const long seed = 0x53494D504C45424CL;
        LevelHeightAccessor heightAccessor = LevelHeightAccessor.Create(-64, 96);
        var world = new OverworldWorld(seed, heightAccessor);
        NoiseBasedChunkGenerator generator = NoiseBasedChunkGenerator.CreateOverworld(seed);
        (WorldGenRegion region, ProtoChunk center) = CreateRegion(world, generator, heightAccessor);
        var origin = new BlockPos(5, 40, 7);

        var simple = new SimpleBlockFeature(BlockStateProvider.Of(Blocks.STONE));
        Require(
            simple.Place(region, generator, new LegacyRandomSource(1L), origin) &&
            ReferenceEquals(center.GetBlockState(origin), Blocks.STONE.DefaultState),
            "SimpleBlockFeature must place the provider's state at a surviving origin.");

        var rejectingBlock = new RejectingBlock();
        var rejectingFeature = new SimpleBlockFeature(
            BlockStateProvider.Of(rejectingBlock.DefaultState));
        var rejectedPosition = new BlockPos(6, 40, 7);

        Require(
            !rejectingFeature.Place(region, generator, new LegacyRandomSource(1L), rejectedPosition) &&
            center.GetBlockState(rejectedPosition).IsAir,
            "SimpleBlockFeature must not write a state whose block survival predicate rejects the origin.");

        var nullProviderFeature = new SimpleBlockFeature(
            Holder<BlockStateProvider>.Direct(new NullStateProvider()));
        var nullPosition = new BlockPos(7, 40, 7);
        Require(
            !nullProviderFeature.Place(region, generator, new LegacyRandomSource(1L), nullPosition) &&
            center.GetBlockState(nullPosition).IsAir,
            "SimpleBlockFeature must return false without writing when the provider returns no optional state.");
    }

    private static void CheckScheduledTickUsesWorldTimeAndRegionOrder()
    {
        const long seed = 0x5449434B53494D50L;
        LevelHeightAccessor heightAccessor = LevelHeightAccessor.Create(-64, 96);
        var world = new OverworldWorld(seed, heightAccessor);
        world.SetGameTime(500L);
        NoiseBasedChunkGenerator generator = NoiseBasedChunkGenerator.CreateOverworld(seed);
        (WorldGenRegion region, ProtoChunk center) = CreateRegion(world, generator, heightAccessor);
        var position = new BlockPos(9, 42, 11);
        var feature = new SimpleBlockFeature(
            BlockStateProvider.Of(Blocks.GRASS_BLOCK),
            scheduleTick: true);

        Require(
            feature.Place(region, generator, new LegacyRandomSource(123L), position),
            "SimpleBlockFeature with schedule_tick must still report a successful block placement.");

        var ticks = (ProtoChunkTicks<Block>)center.GetBlockTicks();
        SavedTick<Block> tick = ticks.Pack(world.GetGameTime()).Single();
        Require(
            ReferenceEquals(tick.Type, Blocks.GRASS_BLOCK) &&
            tick.Pos.Equals(position) &&
            tick.Delay == 1,
            "SimpleBlockFeature must schedule the block tick one tick after current world time.");

        ScheduledTick<Block> scheduled = ticks.ScheduledTicks().Single();
        Require(
            scheduled.TriggerTick == 501 &&
            scheduled.SubTickOrder == 0 &&
            ReferenceEquals(scheduled.Priority, TickPriority.NORMAL),
            "WorldGenRegion must create the scheduled tick with world time, normal priority and its monotonic local sub-tick order.");
    }

    private static (WorldGenRegion Region, ProtoChunk Center) CreateRegion(
        OverworldWorld world,
        NoiseBasedChunkGenerator generator,
        LevelHeightAccessor heightAccessor)
    {
        PalettedContainerFactory factory = PalettedContainerFactory.CreateBuiltIn();
        ChunkStep step = ChunkPyramid.GenerationPyramid.GetStepTo(ChunkStatus.FEATURES);
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
            step,
            centerChunk,
            chunks.Cast<ChunkAccess>());

        return (region, centerChunk);
    }

    private static JsonElement ParseJson(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
