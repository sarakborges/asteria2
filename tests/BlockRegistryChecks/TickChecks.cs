using System.Text.Json;
using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Fluids;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World;
using Asteria.World.Ticks;

internal static class TickChecks
{
    public static void Run()
    {
        CheckTickPriority();
        CheckSavedTickCodec();
        CheckProtoChunkTicks();
        CheckLevelChunkTicks();
        CheckProtoChunkIntegration();
    }

    private static void CheckTickPriority()
    {
        TickPriority[] priorities =
        {
            TickPriority.EXTREMELY_HIGH, TickPriority.VERY_HIGH, TickPriority.HIGH,
            TickPriority.NORMAL, TickPriority.LOW, TickPriority.VERY_LOW, TickPriority.EXTREMELY_LOW
        };

        for (int index = 0; index < priorities.Length; index++)
        {
            TickPriority priority = priorities[index];
            int expectedValue = index - 3;
            Require(
                priority.GetValue() == expectedValue &&
                ReferenceEquals(TickPriority.ByValue(expectedValue), priority) &&
                ReferenceEquals(TickPriority.CODEC.Parse(
                    JsonOps.INSTANCE, JsonOps.INSTANCE.CreateInt(expectedValue)).GetOrThrow(), priority),
                "TickPriority must preserve its reference value ordering and integer codec.");
        }

        Require(
            ReferenceEquals(TickPriority.ByValue(-99), TickPriority.EXTREMELY_HIGH) &&
            ReferenceEquals(TickPriority.ByValue(99), TickPriority.EXTREMELY_LOW),
            "TickPriority.byValue must clamp values outside the declared range to its nearest extreme.");
    }

    private static void CheckSavedTickCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Codec<SavedTick<Block>> blockCodec =
            SavedTick<Block>.CreateCodec(BuiltInRegistries.Block.ByNameCodec());
        var tick = new SavedTick<Block>(
            Blocks.STONE, new BlockPos(-17, 64, 31), 120, TickPriority.VERY_HIGH);
        JsonElement encoded = blockCodec.EncodeStart(ops, tick).GetOrThrow();

        Require(
            ops.Get(encoded, "i").GetOrThrow().GetString() == "asteria:stone" &&
            ops.Get(encoded, "x").GetOrThrow().GetInt32() == -17 &&
            ops.Get(encoded, "y").GetOrThrow().GetInt32() == 64 &&
            ops.Get(encoded, "z").GetOrThrow().GetInt32() == 31 &&
            ops.Get(encoded, "t").GetOrThrow().GetInt32() == 120 &&
            ops.Get(encoded, "p").GetOrThrow().GetInt32() == -2 &&
            ops.Get(encoded, "x").IsError,
            "SavedTick codec must use the flat i, x, y, z, t and p fields from Minecraft Java 26.3.");

        SavedTick<Block> decoded = blockCodec.Parse(ops, encoded).GetOrThrow();
        Require(
            ReferenceEquals(decoded.Type, Blocks.STONE) &&
            decoded.Pos.Equals(tick.Pos) &&
            decoded.Delay == tick.Delay &&
            ReferenceEquals(decoded.Priority, TickPriority.VERY_HIGH),
            "SavedTick block codec must round-trip identity, position, delay and priority.");

        Codec<SavedTick<Fluid>> fluidCodec =
            SavedTick<Fluid>.CreateCodec(BuiltInRegistries.Fluid.ByNameCodec());
        var fluidTick = new SavedTick<Fluid>(
            Fluids.EMPTY, new BlockPos(1, 2, 3), 4, TickPriority.LOW);
        SavedTick<Fluid> fluidDecoded =
            fluidCodec.Parse(ops, fluidCodec.EncodeStart(ops, fluidTick).GetOrThrow()).GetOrThrow();
        Require(
            ReferenceEquals(fluidDecoded.Type, Fluids.EMPTY) &&
            fluidDecoded.Pos.Equals(fluidTick.Pos) &&
            fluidDecoded.Delay == 4 &&
            ReferenceEquals(fluidDecoded.Priority, TickPriority.LOW),
            "SavedTick fluid codec must use the fluid registry and the same record shape.");

        Require(
            SavedTick<Block>.FilterTickListForChunk(
                new[] {
                    tick,
                    new SavedTick<Block>(Blocks.DIRT, new BlockPos(16, 0, 0), 0, TickPriority.NORMAL)
                },
                ChunkPos.Containing(tick.Pos)).SequenceEqual(new[] { tick }),
            "SavedTick chunk filtering must retain only positions belonging to the requested chunk.");
    }

    private static void CheckProtoChunkTicks()
    {
        var ticks = new ProtoChunkTicks<Block>();
        var mutablePosition = new BlockPos.MutableBlockPos(-2, 5, 7);
        var first = new ScheduledTick<Block>(
            Blocks.STONE, mutablePosition, 30, TickPriority.HIGH, 10);
        ticks.Schedule(first);
        mutablePosition.Set(8, 9, 10);

        Require(
            first.Pos.Equals(new BlockPos(-2, 5, 7)) &&
            ticks.HasScheduledTick(new BlockPos(-2, 5, 7), Blocks.STONE),
            "ScheduledTick must snapshot mutable positions and ProtoChunkTicks must index by type identity and position.");

        ticks.Schedule(new ScheduledTick<Block>(
            Blocks.STONE, new BlockPos(-2, 5, 7), 90, TickPriority.LOW, 11));
        ticks.Schedule(new ScheduledTick<Block>(
            Blocks.DIRT, new BlockPos(-2, 5, 7), 90, TickPriority.LOW, 12));
        Require(
            ticks.Count() == 2 &&
            !ticks.HasScheduledTick(new BlockPos(-2, 5, 7), Blocks.AIR),
            "ProtoChunkTicks must deduplicate equal type/position pairs while allowing a different type at the same position.");

        List<SavedTick<Block>> packed = ticks.Pack(500);
        Require(
            packed.Count == 2 &&
            packed[0].Delay == 0 &&
            ReferenceEquals(packed[0].Priority, TickPriority.HIGH) &&
            packed[1].Delay == 0 &&
            ReferenceEquals(packed[1].Type, Blocks.DIRT),
            "ProtoChunkTicks packing must preserve insertion order and store zero relative delay.");

        ProtoChunkTicks<Block> loaded = ProtoChunkTicks<Block>.Load(packed);
        Require(
            loaded.Count() == 2 &&
            loaded.ScheduledTicks().Count == 2 &&
            loaded.HasScheduledTick(new BlockPos(-2, 5, 7), Blocks.STONE),
            "ProtoChunkTicks.load must rebuild the uniqueness index from saved ticks.");
    }

    private static void CheckLevelChunkTicks()
    {
        var pending = new List<SavedTick<Block>>
        {
            new(Blocks.STONE, new BlockPos(1, 2, 3), 5, TickPriority.HIGH),
            new(Blocks.DIRT, new BlockPos(4, 5, 6), 2, TickPriority.LOW)
        };
        var ticks = new LevelChunkTicks<Block>(pending);
        int callbackCount = 0;
        ticks.SetOnTickAdded((_, _) => callbackCount++);

        Require(
            ticks.Count() == 2 &&
            ticks.HasScheduledTick(new BlockPos(1, 2, 3), Blocks.STONE),
            "LevelChunkTicks must count and index pending saved ticks before unpacking.");

        ticks.Unpack(100);
        Require(
            ticks.Count() == 2 &&
            callbackCount == 2 &&
            ticks.Peek() is { } first &&
            ReferenceEquals(first.Type, Blocks.DIRT) &&
            first.TriggerTick == 102 &&
            first.SubTickOrder == -1,
            "LevelChunkTicks.unpack must create absolute tick times and preserve the reference negative sub-tick base.");

        ticks.Schedule(new ScheduledTick<Block>(
            Blocks.STONE, new BlockPos(1, 2, 3), 110, TickPriority.NORMAL, 20));
        ticks.Schedule(new ScheduledTick<Block>(
            Blocks.GRASS_BLOCK, new BlockPos(7, 8, 9), 102, TickPriority.HIGH, 3));
        ticks.Schedule(new ScheduledTick<Block>(
            Blocks.DIRT, new BlockPos(9, 8, 9), 120, TickPriority.LOW, 21));

        Require(
            ticks.Count() == 4 && callbackCount == 4,
            "LevelChunkTicks must suppress duplicates and notify only newly added live ticks.");

        ScheduledTick<Block>? polled = ticks.Poll();
        Require(
            polled is not null &&
            ReferenceEquals(polled.Type, Blocks.GRASS_BLOCK) &&
            !ticks.HasScheduledTick(polled.Pos, polled.Type),
            "LevelChunkTicks.poll must use trigger tick, priority and sub-tick order, then remove the uniqueness entry.");

        ticks.RemoveIf(static tick => ReferenceEquals(tick.Type, Blocks.STONE));
        Require(
            !ticks.HasScheduledTick(new BlockPos(1, 2, 3), Blocks.STONE) &&
            ticks.Count() == 2,
            "LevelChunkTicks.removeIf must remove matching queued ticks from both queue and uniqueness index.");

        List<SavedTick<Block>> repacked = ticks.Pack(100);
        Require(
            repacked.Count == 2 &&
            repacked.Select(tick => tick.Delay).SequenceEqual(new[] { 2, 20 }),
            "LevelChunkTicks.pack must serialize pending queue entries in sub-tick order with delays relative to current time.");
    }

    private static void CheckProtoChunkIntegration()
    {
        var chunk = new ProtoChunk(
            new ChunkPos(-2, 3),
            LevelHeightAccessor.Create(-16, 48),
            PalettedContainerFactory.CreateBuiltIn());
        var position = new BlockPos(-30, 5, 49);

        chunk.GetBlockTicks().Schedule(new ScheduledTick<Block>(
            Blocks.STONE, position, 42, TickPriority.HIGH, 7));
        chunk.GetFluidTicks().Schedule(new ScheduledTick<Fluid>(
            Fluids.EMPTY, position, 43, TickPriority.NORMAL, 8));

        ChunkAccess.PackedTicks packed = chunk.GetTicksForSerialization(500);
        Require(
            packed.Blocks.Count == 1 &&
            packed.Fluids.Count == 1 &&
            ReferenceEquals(packed.Blocks[0].Type, Blocks.STONE) &&
            ReferenceEquals(packed.Fluids[0].Type, Fluids.EMPTY) &&
            packed.Blocks[0].Delay == 0 &&
            packed.Fluids[0].Delay == 0,
            "ProtoChunk must expose independent block/fluid tick containers and serialize their saved tick lists.");

        Require(
            chunk.UnpackBlockTicks().Count() == 1 &&
            chunk.UnpackFluidTicks().Count() == 1,
            "ProtoChunk must promote block and fluid tick lists to pending LevelChunkTicks containers.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
